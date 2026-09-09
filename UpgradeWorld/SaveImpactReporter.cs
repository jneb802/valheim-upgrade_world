using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using HarmonyLib;

namespace UpgradeWorld;

public sealed class SaveImpactSnapshot
{
  public DateTime CapturedAt { get; private set; }
  public int ZdoTotal { get; private set; }
  public int PersistentZdos { get; private set; }
  public int NonPersistentZdos { get; private set; }
  public int PendingDestroyZdos { get; private set; }
  public int LoadedZones { get; private set; }
  public int GeneratedZones { get; private set; }
  public int Peers { get; private set; }
  public float ManagedMemoryMb { get; private set; }

  public static SaveImpactSnapshot Capture()
  {
    ZDOMan? zdoMan = ZDOMan.instance;
    ZoneSystem? zoneSystem = ZoneSystem.instance;
    ZNet? znet = ZNet.instance;

    int totalZdos = zdoMan?.m_objectsByID.Count ?? 0;
    int persistentZdos = 0;
    if (zdoMan != null)
    {
      foreach (ZDO zdo in zdoMan.m_objectsByID.Values)
      {
        if (zdo.Persistent)
        {
          persistentZdos++;
        }
      }
    }

    return new SaveImpactSnapshot
    {
      CapturedAt = DateTime.UtcNow,
      ZdoTotal = totalZdos,
      PersistentZdos = persistentZdos,
      NonPersistentZdos = Math.Max(0, totalZdos - persistentZdos),
      PendingDestroyZdos = zdoMan?.m_destroySendList.Count ?? 0,
      LoadedZones = zoneSystem?.m_zones.Count ?? 0,
      GeneratedZones = zoneSystem?.m_generatedZones.Count ?? 0,
      Peers = znet?.m_peers.Count ?? 0,
      ManagedMemoryMb = GC.GetTotalMemory(false) / 1024f / 1024f
    };
  }

  public void AddTo(Dictionary<string, object?> values, string prefix)
  {
    values[$"{prefix}SnapshotCapturedAt"] = CapturedAt.ToString("o");
    values[$"{prefix}ZdoTotal"] = ZdoTotal;
    values[$"{prefix}PersistentZdos"] = PersistentZdos;
    values[$"{prefix}NonPersistentZdos"] = NonPersistentZdos;
    values[$"{prefix}PendingDestroyZdos"] = PendingDestroyZdos;
    values[$"{prefix}LoadedZones"] = LoadedZones;
    values[$"{prefix}GeneratedZones"] = GeneratedZones;
    values[$"{prefix}Peers"] = Peers;
    values[$"{prefix}ManagedMemoryMb"] = Math.Round(ManagedMemoryMb, 3);
  }
}

public static class SaveImpactReporter
{
  private const string LogPrefix = "[UpgradeWorldSaveImpact] ";
  private static SaveImpactSnapshot? PreparedSnapshot;

  internal static SaveImpactSnapshot? Capture()
  {
    if (!Settings.OperationEventsEnabled) return null;
    try
    {
      return SaveImpactSnapshot.Capture();
    }
    catch (Exception exception)
    {
      WarnFailure(exception);
      return null;
    }
  }

  internal static void SetPreparedSnapshot(SaveImpactSnapshot? snapshot) => Volatile.Write(ref PreparedSnapshot, snapshot);
  internal static SaveImpactSnapshot? GetPreparedSnapshot() => Volatile.Read(ref PreparedSnapshot);

  internal static void Emit(string phase, SaveImpactSnapshot? snapshot, long? elapsedMs = null,
    bool? success = null, Exception? failure = null)
  {
    if (!Settings.OperationEventsEnabled)
    {
      return;
    }

    try
    {
      Dictionary<string, object?> values = new()
      {
        ["schema"] = 1,
        ["event"] = "save-impact",
        ["phase"] = phase,
        ["timestamp"] = DateTime.UtcNow.ToString("o"),
        ["thread"] = Environment.CurrentManagedThreadId
      };
      if (elapsedMs.HasValue)
      {
        values["elapsedMs"] = elapsedMs.Value;
      }

      if (success.HasValue) values["success"] = success.Value;
      if (failure != null) values["exception"] = failure.GetType().Name;
      values["snapshotSource"] = snapshot == null ? null : "main-thread";
      snapshot?.AddTo(values, "world");
      string json = StructuredEventWriter.ToJson(values);
      if (Settings.OperationEventsToLog)
      {
        UpgradeWorld.Log.LogInfo(LogPrefix + json);
      }
      StructuredEventWriter.WriteFile(json);
    }
    catch (Exception exception)
    {
      WarnFailure(exception);
    }
  }

  private static void WarnFailure(Exception exception)
  {
    try
    {
      UpgradeWorld.Log.LogWarning($"Failed to emit Upgrade World save-impact event: {exception.Message}");
    }
    catch { }
  }
}

internal sealed class SaveImpactTiming(SaveImpactSnapshot? snapshot)
{
  internal readonly SaveImpactSnapshot? Snapshot = snapshot;
  internal readonly Stopwatch Timer = Stopwatch.StartNew();
}

[HarmonyPatch(typeof(ZNet), "SaveWorld")]
public static class SaveImpactZNetSaveWorldPatch
{
  private static void Prefix(bool sync, out SaveImpactTiming __state)
  {
    __state = new SaveImpactTiming(SaveImpactReporter.Capture());
    SaveImpactReporter.Emit(sync ? "ZNet.SaveWorld.sync.enter" : "ZNet.SaveWorld.async.enter", __state.Snapshot);
  }

  private static void Finalizer(bool sync, SaveImpactTiming? __state, Exception? __exception)
  {
    if (__state == null) return;
    __state.Timer.Stop();
    // An asynchronous exit means scheduling returned, not that disk writes finished.
    SaveImpactReporter.Emit(sync ? "ZNet.SaveWorld.sync.exit" : "ZNet.SaveWorld.async.exit",
      __state.Snapshot, __state.Timer.ElapsedMilliseconds, failure: __exception);
  }
}

[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.PrepareSave))]
public static class SaveImpactZdoManPrepareSavePatch
{
  private static void Prefix(out SaveImpactTiming __state)
  {
    __state = new SaveImpactTiming(SaveImpactReporter.Capture());
    SaveImpactReporter.SetPreparedSnapshot(__state.Snapshot);
    SaveImpactReporter.Emit("ZDOMan.PrepareSave.enter", __state.Snapshot);
  }

  private static void Finalizer(SaveImpactTiming? __state, Exception? __exception)
  {
    if (__state == null) return;
    __state.Timer.Stop();
    SaveImpactReporter.Emit("ZDOMan.PrepareSave.exit", __state.Snapshot, __state.Timer.ElapsedMilliseconds,
      success: __exception == null, failure: __exception);
  }
}

[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.SaveChunks))]
public static class SaveImpactZdoManSaveChunksPatch
{
  private static void Prefix(out SaveImpactTiming __state)
  {
    // This runs on the save worker. Only read the snapshot captured before saving.
    __state = new SaveImpactTiming(SaveImpactReporter.GetPreparedSnapshot());
    SaveImpactReporter.Emit("ZDOMan.SaveChunks.enter", __state.Snapshot);
  }

  private static void Finalizer(SaveImpactTiming? __state, bool __result, Exception? __exception)
  {
    if (__state == null) return;
    __state.Timer.Stop();
    SaveImpactReporter.Emit("ZDOMan.SaveChunks.exit", __state.Snapshot, __state.Timer.ElapsedMilliseconds,
      success: __exception == null && __result, failure: __exception);
  }
}

[HarmonyPatch(typeof(ZNet), "SaveWorldThread")]
public static class SaveImpactWorldThreadPatch
{
  private static void Prefix(out SaveImpactTiming __state)
  {
    __state = new SaveImpactTiming(SaveImpactReporter.GetPreparedSnapshot());
    SaveImpactReporter.Emit("ZNet.SaveWorldThread.enter", __state.Snapshot);
  }

  private static void Finalizer(SaveImpactTiming? __state, Exception? __exception)
  {
    if (__state == null) return;
    __state.Timer.Stop();
    // The game handles some failures internally, so this is a duration, not a success claim.
    SaveImpactReporter.Emit("ZNet.SaveWorldThread.exit", __state.Snapshot, __state.Timer.ElapsedMilliseconds,
      failure: __exception);
  }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;

namespace UpgradeWorld;

public sealed class SaveImpactSnapshot
{
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

  public static void Emit(string phase, long? elapsedMs = null)
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

      SaveImpactSnapshot.Capture().AddTo(values, "world");
      string json = StructuredEventWriter.ToJson(values);
      if (Settings.OperationEventsToLog)
      {
        UpgradeWorld.Log.LogInfo(LogPrefix + json);
      }
      StructuredEventWriter.WriteFile(json);
    }
    catch (Exception exception)
    {
      try
      {
        UpgradeWorld.Log.LogWarning($"Failed to emit Upgrade World save-impact event: {exception.Message}");
      }
      catch
      {
      }
    }
  }
}

[HarmonyPatch(typeof(ZNet), "SaveWorld")]
public static class SaveImpactZNetSaveWorldPatch
{
  private static readonly Stopwatch Stopwatch = new();

  private static void Prefix(bool sync)
  {
    Stopwatch.Restart();
    SaveImpactReporter.Emit(sync ? "ZNet.SaveWorld.sync.enter" : "ZNet.SaveWorld.async.enter");
  }

  private static void Finalizer(bool sync)
  {
    Stopwatch.Stop();
    SaveImpactReporter.Emit(sync ? "ZNet.SaveWorld.sync.exit" : "ZNet.SaveWorld.async.exit", Stopwatch.ElapsedMilliseconds);
  }
}

[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.PrepareSave))]
public static class SaveImpactZdoManPrepareSavePatch
{
  private static readonly Stopwatch Stopwatch = new();

  private static void Prefix()
  {
    Stopwatch.Restart();
    SaveImpactReporter.Emit("ZDOMan.PrepareSave.enter");
  }

  private static void Finalizer()
  {
    Stopwatch.Stop();
    SaveImpactReporter.Emit("ZDOMan.PrepareSave.exit", Stopwatch.ElapsedMilliseconds);
  }
}

[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.SaveAsync))]
public static class SaveImpactZdoManSaveAsyncPatch
{
  private static readonly Stopwatch Stopwatch = new();

  private static void Prefix()
  {
    Stopwatch.Restart();
    SaveImpactReporter.Emit("ZDOMan.SaveAsync.enter");
  }

  private static void Finalizer()
  {
    Stopwatch.Stop();
    SaveImpactReporter.Emit("ZDOMan.SaveAsync.exit", Stopwatch.ElapsedMilliseconds);
  }
}

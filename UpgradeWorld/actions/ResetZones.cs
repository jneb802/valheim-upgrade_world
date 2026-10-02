using System.Collections.Generic;
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
namespace UpgradeWorld;

/// <summary>Destroys everything in a zone so that the world generator can regenerate it.</summary>
public class ResetZones : ZoneOperation
{
  private Dictionary<Vector2s, Direction> BorderZones = [];
  private readonly string OperationId = Guid.NewGuid().ToString("N");
  private readonly string Command;
  private ResetZdoCounter? Counter;
  private int? Before;
  private DateTime? Started;
  private bool Reported;
  public ResetZones(Terminal context, FiltererParameters args, string command = "zones_reset") : base(context, args)
  {
    Command = command;
    Operation = "Reset";
    args.TargetZones = TargetZones.Generated;
    InitString = args.Print("Reset");
    Filterers = FiltererFactory.Create(args);
  }
  private int Reseted = 0;
  protected override void OnStart()
  {
    Started = DateTime.UtcNow;
    ZDOMan manager = ZDOMan.instance;
    Before = manager.m_objectsByID.Count;
    Counter = new(manager.m_destroySendList, manager.m_objectsByID.ContainsKey);
    ResetZdoCounter.Active = Counter;
  }
  protected override IEnumerator OnExecute(Stopwatch sw)
  {
    yield return base.OnExecute(sw);
    Stopwatch wait = Stopwatch.StartNew();
    long nextCheck = 0;
    while (Counter != null)
    {
      if (wait.ElapsedMilliseconds < nextCheck) { yield return null; continue; }
      Counter.Confirm();
      if (Counter.PendingCount == 0 || wait.Elapsed.TotalSeconds >= 10) break;
      nextCheck = wait.ElapsedMilliseconds + 100;
      yield return null;
    }
  }
  protected override bool ExecuteZone(Vector2s zone)
  {
    var zs = ZoneSystem.instance;
    var objs = Helper.GetZDOs(zone);

    if (objs != null)
    {
      foreach (var zdo in objs)
      {
        var position = zdo.GetPosition();
        if (ZoneSystem.GetZone(position) == zone)
          Helper.RemoveZDO(zdo, Counter);
      }
    }
    var locations = zs.m_locationInstances;
    if (locations.TryGetValue(zone, out var location))
    {
      location.m_placed = false;
      location.m_position = new(location.m_position.x, WorldGenerator.instance.GetHeight(location.m_position.x, location.m_position.z), location.m_position.z);
      zs.m_locationInstances[zone] = location;
    }
    zs.m_generatedZones.Remove(zone);
    if (zs.m_zones.TryGetValue(zone, out var z))
    {
      UnityEngine.Object.Destroy(z.m_root);
      zs.m_zones.Remove(zone);
    }
    Reseted++;
    AddBorder(zone, Direction.North);
    AddBorder(zone, Direction.East);
    AddBorder(zone, Direction.South);
    AddBorder(zone, Direction.West);
    AddBorder(zone, Direction.NorthWest);
    AddBorder(zone, Direction.NorthEast);
    AddBorder(zone, Direction.SouthWest);
    AddBorder(zone, Direction.SouthEast);
    return true;
  }
  private void AddBorder(Vector2s zone, Direction direction)
  {
    if (direction == Direction.North) zone.y -= 1;
    if (direction == Direction.East) zone.x -= 1;
    if (direction == Direction.South) zone.y += 1;
    if (direction == Direction.West) zone.x += 1;
    if (direction == Direction.NorthWest)
    {
      zone.y -= 1;
      zone.x += 1;
    }
    if (direction == Direction.NorthEast)
    {
      zone.y -= 1;
      zone.x -= 1;
    }
    if (direction == Direction.SouthWest)
    {
      zone.y += 1;
      zone.x += 1;
    }
    if (direction == Direction.SouthEast)
    {
      zone.y += 1;
      zone.x -= 1;
    }
    if (BorderZones.ContainsKey(zone)) direction |= BorderZones[zone];
    BorderZones[zone] = direction;
  }

  protected override void OnEnd()
  {
    var text = $"{Operation} completed. {Reseted} zones reseted.";
    if (Failed > 0) text += " " + Failed + " errors.";
    Print(text);
    BorderZones = BorderZones.Where(kvp => ZoneSystem.instance.IsZoneGenerated(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    TerrainModificationFilterer? terrainFilterer = Filterers.OfType<TerrainModificationFilterer>().FirstOrDefault();
    HashSet<Vector2s> terrainProtectedZones = terrainFilterer?.ExcludedZones ?? [];
    new ResetBorder(Context, BorderZones, terrainProtectedZones);
    ClutterSystem.instance?.ClearAll();
    Helper.RecalculateTerrain();
    Minimap.instance?.UpdateLocationPins(1000);
    Report(Failed == 0 ? "completed" : "failed", Failed > 0 ? $"{Failed} zone errors" : null);
  }

  protected override void OnFailure(Exception failure) => Report("failed", failure.Message);
  public override void Cancel() => Report("cancelled", "Operation stopped before completion.");

  private void Report(string status, string? error)
  {
    if (Reported) return;
    Reported = true;
    try
    {
      Counter?.Confirm();
      int? after = Started.HasValue && ZDOMan.instance != null ? ZDOMan.instance.m_objectsByID.Count : null;
      bool complete = Counter != null && Counter.PendingCount == 0;
      Dictionary<string, object?> result = new()
      {
        ["schema"] = 1,
        ["event"] = "zone_reset_result",
        ["operationId"] = OperationId,
        ["world"] = ZNet.instance?.GetWorldName(),
        ["command"] = Command,
        ["quadrants"] = Args.Quadrants.Select(q => q.ToString().ToLowerInvariant()).ToArray(),
        ["startedUtc"] = Started?.ToString("o"),
        ["endedUtc"] = DateTime.UtcNow.ToString("o"),
        ["status"] = status,
        ["zonesReset"] = Reseted,
        ["zdosRemoved"] = Counter?.Removed,
        ["zdosRequested"] = Counter?.RequestedCount,
        ["zdosPending"] = Counter?.PendingCount,
        ["worldZdosBefore"] = Before,
        ["worldZdosAfter"] = after,
        ["worldZdoNetDecrease"] = Before.HasValue && after.HasValue ? Before.Value - after.Value : null,
        ["countComplete"] = complete,
        ["error"] = error ?? (Counter?.PendingCount > 0 ? "Deletion confirmation did not finish before the operation ended." : null)
      };
      string count = Counter == null ? "unavailable" : Counter.Removed.ToString();
      Print($"Reset ZDO result: {count} removed; count {(complete ? "confirmed" : "incomplete")}; status {status}; operation {OperationId}.");
      ZoneResetResultWriter.Write(result);
    }
    catch (Exception exception)
    {
      UpgradeWorld.Log.LogWarning($"Unable to report reset ZDO count: {exception.Message}");
    }
    finally
    {
      if (ResetZdoCounter.Active == Counter) ResetZdoCounter.Active = null;
      Counter = null;
    }
  }
}

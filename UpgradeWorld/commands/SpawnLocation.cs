using System;
using System.Collections.Generic;
using Service;
using UnityEngine;

namespace UpgradeWorld;

public class SpawnLocationCommand
{
  public SpawnLocationCommand()
  {
    Dictionary<string, Func<int, List<string>?>> named = new()
    {
      { "pos", subIndex => CommandWrapper.XZY("pos", "Coordinates for the spawn position. If y is omitted, ground height is used.", subIndex) },
      { "rotation", subIndex => CommandWrapper.Info("Rotation as y,x,z degrees.") },
      { "rot", subIndex => CommandWrapper.Info("Rotation as y,x,z degrees.") },
      { "seed", subIndex => CommandWrapper.Info("Location random seed.") },
      { "dungeonSeed", subIndex => CommandWrapper.Info("Dungeon random seed.") },
      { "register", subIndex => CommandWrapper.Info("Whether to register the spawned location instance.") },
      { "replace", subIndex => CommandWrapper.Info("Whether to replace an existing location instance in the target zone.") }
    };
    CommandWrapper.Register("spawn_location", (index, subIndex) =>
    {
      if (index == 0) return LocationOperation.AllIds();
      return null;
    }, named);
    Helper.Command("spawn_location", "[id] [pos=x,z,y] [...args] - Spawns a location without disabling world saving.", (args) =>
    {
      if (args.Length < 2)
      {
        Helper.Print(args.Context, "Error: Missing location id.");
        return;
      }
      if (Helper.IsClient(args)) return;

      var id = args[1];
      var zs = ZoneSystem.instance;
      var location = zs.GetLocation(id.GetStableHashCode()) ?? throw new InvalidOperationException($"Location {id} not found.");
      if (!location.m_prefab.IsValid)
        throw new InvalidOperationException($"Prefab for location {id} not found.");

      var position = Helper.GetPlayerPosition();
      var rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 16) * 22.5f, 0f);
      var seed = UnityEngine.Random.Range(0, 99999);
      var dungeonSeed = int.MinValue;
      var snapToGround = false;
      var hasPosition = false;
      var register = false;
      var replace = false;

      foreach (var arg in args.Args)
      {
        var split = Parse.SplitWithEmpty(arg, '=');
        var name = split[0].ToLower();
        if (split.Length < 2)
        {
          if (name == "register") register = true;
          else if (name == "replace") replace = true;
          continue;
        }
        var value = split[1];
        if (name == "pos" || name == "position")
        {
          var pieces = Parse.SplitWithEmpty(value);
          position = Parse.VectorXZY(pieces, position);
          snapToGround = pieces.Length < 3;
          hasPosition = true;
        }
        else if (name == "rot" || name == "rotation")
        {
          rotation = Parse.AngleYXZ(value);
        }
        else if (name == "seed")
        {
          seed = Parse.Int(value, seed);
        }
        else if (name == "dungeonseed")
        {
          dungeonSeed = Parse.Int(value, dungeonSeed);
        }
        else if (name == "register")
        {
          register = Parse.Boolean(value) ?? register;
        }
        else if (name == "replace")
        {
          replace = Parse.Boolean(value) ?? replace;
        }
      }

      if (!hasPosition && !Player.m_localPlayer && ServerExecution.User == null)
        throw new InvalidOperationException("Missing pos=x,z,y. Dedicated server commands without a player must provide a position.");

      if (snapToGround)
      {
        if (zs.FindFloor(position, out var floor))
          position.y = floor;
        else if (WorldGenerator.instance != null)
          position.y = WorldGenerator.instance.GetHeight(position.x, position.z, out _);
      }

      DungeonGenerator.m_forceSeed = dungeonSeed;
      zs.SpawnLocation(location, seed, position, rotation, ZoneSystem.SpawnMode.Full, []);
      if (register)
        RegisterLocationInstance(zs, location, position, replace);
      Helper.Print(args.Context, $"Spawned location {id} at {Helper.PrintVectorXZY(position)}.");
    }, LocationOperation.AllIds);
  }

  private static void RegisterLocationInstance(ZoneSystem zs, ZoneSystem.ZoneLocation location, Vector3 position, bool replace)
  {
    var zone = ZoneSystem.GetZone(position);
    if (zs.m_locationInstances.TryGetValue(zone, out var existing) && !replace)
      throw new InvalidOperationException($"Zone {zone} already has location {existing.m_location.m_prefab.Name}. Use replace=true to overwrite it.");

    zs.m_locationInstances[zone] = new ZoneSystem.LocationInstance
    {
      m_location = location,
      m_position = position,
      m_placed = true
    };
  }
}

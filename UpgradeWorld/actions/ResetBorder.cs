using System;
using System.Collections.Generic;
namespace UpgradeWorld;

[Flags]
public enum Direction { None, North, East, South = 4, West = 8, NorthEast = 16, SouthEast = 32, SouthWest = 64, NorthWest = 128 }

/// <summary>Destroys everything in a zone so that the world generator can regenerate it.</summary>
public class ResetBorder : EntityOperation
{
  public ResetBorder(Terminal context, Dictionary<Vector2s, Direction> zones, HashSet<Vector2s> terrainProtectedZones) : base(context, false)
  {
    Execute(zones, terrainProtectedZones);
  }
  private void Execute(Dictionary<Vector2s, Direction> zones, HashSet<Vector2s> terrainProtectedZones)
  {
    ZDO[] zdos = GetZDOs(Settings.TerrainCompilerHash);
    int reseted = 0;
    int protectedZones = 0;
    foreach (ZDO zdo in zdos)
    {
      Vector2s zone = ZoneSystem.GetZone(zdo.GetPosition());
      if (!zones.TryGetValue(zone, out Direction direction)) continue;
      if (terrainProtectedZones.Contains(zone))
      {
        protectedZones++;
        continue;
      }
      Update(zdo, direction);
      reseted += 1;
    }
    string message = $"{reseted} border zones reseted";
    if (protectedZones > 0) message += $". {protectedZones} protected by terrain modifications";
    Print(message);
  }

  private void Update(ZDO zdo, Direction direction)
  {
    var byteArray = zdo.GetByteArray("TCData");
    if (byteArray == null)
      return;
    var change = false;
    var from = new ZPackage(Utils.Decompress(byteArray));
    var to = new ZPackage();
    to.Write(from.ReadInt());
    to.Write(from.ReadInt() + 1);
    to.Write(from.ReadVector3());
    to.Write(from.ReadSingle());
    var size = from.ReadInt();
    to.Write(size);
    var width = (int)Math.Sqrt(size);
    for (int index = 0; index < size; index++)
    {
      var wasModified = from.ReadBool();
      var modified = wasModified;
      var j = index / width;
      var i = index % width;
      if (direction.HasFlag(Direction.North) && j == width - 1)
        modified = false;
      if (direction.HasFlag(Direction.East) && i == width - 1)
        modified = false;
      if (direction.HasFlag(Direction.South) && j == 0)
        modified = false;
      if (direction.HasFlag(Direction.West) && i == 0)
        modified = false;
      if (direction.HasFlag(Direction.NorthEast) && j == width - 1 && i == width - 1)
        modified = false;
      if (direction.HasFlag(Direction.NorthWest) && j == width - 1 && i == 0)
        modified = false;
      if (direction.HasFlag(Direction.SouthWest) && j == 0 && i == 0)
        modified = false;
      if (direction.HasFlag(Direction.SouthEast) && j == 0 && i == width - 1)
        modified = false;
      to.Write(modified);
      if (modified)
      {
        to.Write(from.ReadSingle());
        to.Write(from.ReadSingle());
      }
      if (wasModified && !modified)
      {
        change = true;
        from.ReadSingle();
        from.ReadSingle();
      }
    }
    size = from.ReadInt();
    to.Write(size);
    for (int index = 0; index < size; index++)
    {
      var modified = from.ReadBool();
      to.Write(modified);
      if (modified)
      {
        to.Write(from.ReadSingle());
        to.Write(from.ReadSingle());
        to.Write(from.ReadSingle());
        to.Write(from.ReadSingle());
      }
    }
    var bytes = Utils.Compress(to.GetArray());
    if (!change) return;
    zdo.Set("TCData", bytes);
    zdo.DataRevision += 100;
  }

}

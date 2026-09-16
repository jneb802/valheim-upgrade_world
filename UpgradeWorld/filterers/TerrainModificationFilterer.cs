using System.Collections.Generic;
using System.Linq;
namespace UpgradeWorld;

///<summary>Filters zones based on persisted terrain modifications.</summary>
public class TerrainModificationFilterer(int size) : IZoneFilterer
{
  public HashSet<Vector2s> ExcludedZones { get; private set; } = [];
  public int Size = size;

  private void CalculateExcluded()
  {
    HashSet<Vector2s> excludedZones = [];
    int adjacent = Size - 1;
    IEnumerable<ZDO> terrainCompilers = ZDOMan.instance.m_objectsByID.Values.Where(zdo =>
      zdo.m_prefab == Settings.TerrainCompilerHash &&
      zdo.GetByteArray(ZDOVars.s_TCData) is byte[] data && data.Length > 0);

    foreach (ZDO zdo in terrainCompilers)
    {
      Vector2s zone = ZoneSystem.GetZone(zdo.GetPosition());
      for (int i = -adjacent; i <= adjacent; i++)
      {
        for (int j = -adjacent; j <= adjacent; j++)
        {
          excludedZones.Add(new(zone.x + i, zone.y + j));
        }
      }
    }
    ExcludedZones = excludedZones;
  }

  public Vector2s[] FilterZones(Vector2s[] zones, ref List<string> messages)
  {
    CalculateExcluded();
    int amount = zones.Length;
    zones = [.. zones.Where(zone => !ExcludedZones.Contains(zone))];
    int skipped = amount - zones.Length;
    if (skipped > 0) messages.Add(skipped + " skipped by terrain modifications");
    return zones;
  }
}

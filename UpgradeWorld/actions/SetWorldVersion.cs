using System.Collections;
using System.Diagnostics;
namespace UpgradeWorld;

public class WorldVersion(Terminal context, int version) : ExecutedOperation(context)
{
  readonly int Version = version;

  protected override IEnumerator OnExecute(Stopwatch sw)
  {
    World world = WorldGenerator.instance.m_world;
    // The cache does not record the world-generation version. Remove it so an
    // unsaved version change cannot leave a cache for the wrong world on disk.
    AltBiomeWorldData.RemoveCache(world.m_name);
    world.m_worldGenVersion = Version;
    WorldGenerator.Initialize(world);
    AltBiomeWorldData.GenerateBiomePoints(world);
    world.m_biomeData.GenerateSectors();
    Zones.ResetAllZones();
    Helper.RecalculateTerrain();
    Minimap.instance?.ForceRegen();
    Print($"Updated world version to {world.m_worldGenVersion}.");
    yield break;
  }

  protected override string OnInit()
  {
    return $"Updating world version to {Version}. This may change biome distribution.";
  }
}

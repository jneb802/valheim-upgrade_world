using System.Collections.Generic;
using System.Linq;

namespace UpgradeWorld;

// Keep the game's distance lookup tables consistent with the location database.
internal static class LocationRegistry
{
  public static void Remove(Vector2s zone)
  {
    ZoneSystem system = ZoneSystem.instance;
    if (!system.m_locationInstances.TryGetValue(zone, out ZoneSystem.LocationInstance instance)) return;
    system.m_locationInstances.Remove(zone);
    if (instance.m_location == null) return;
    Remove(system.m_locationIDCache, instance.m_location.m_prefab.m_assetID, instance);
    Remove(system.m_locationGroupCache, instance.m_location.m_group, instance);
    Remove(system.m_locationMaxGroupCache, instance.m_location.m_groupMax, instance);
  }

  private static void Remove<TKey>(Dictionary<TKey, List<ZoneSystem.LocationInstance>> cache,
    TKey key, ZoneSystem.LocationInstance instance)
  {
    if (!cache.TryGetValue(key, out List<ZoneSystem.LocationInstance> entries)) return;
    entries.RemoveAll(entry => entry.m_position.x == instance.m_position.x && entry.m_position.z == instance.m_position.z);
    if (entries.Count == 0) cache.Remove(key);
  }

  public static void Set(Vector2s zone, ZoneSystem.LocationInstance instance)
  {
    Remove(zone);
    ZoneSystem system = ZoneSystem.instance;
    system.m_locationInstances[zone] = instance;
    AddToCaches(system, instance);
  }

  public static void RebuildCaches()
  {
    ZoneSystem system = ZoneSystem.instance;
    system.m_locationIDCache.Clear();
    system.m_locationGroupCache.Clear();
    system.m_locationMaxGroupCache.Clear();
    foreach (ZoneSystem.LocationInstance instance in system.m_locationInstances.Values)
      AddToCaches(system, instance);
  }

  private static void AddToCaches(ZoneSystem system, ZoneSystem.LocationInstance instance)
  {
    if (instance.m_location == null) return;
    system.AddToCache(system.m_locationIDCache, instance.m_location.m_prefab.m_assetID, instance);
    system.AddToCache(system.m_locationGroupCache, instance.m_location.m_group, instance);
    system.AddToCache(system.m_locationMaxGroupCache, instance.m_location.m_groupMax, instance);
  }

  public static bool Allows(ZoneSystem.ZoneLocation location, BiomeSector sector) =>
    (location.AltBiomeParent == null || sector.AltBiomes.Any(biome => biome.m_name == location.AltBiomeParent))
    && !sector.AltBiomes.Any(biome => biome.m_blockLocationNames.Contains(location.m_name));
}

using System.Collections.Generic;
using System.Linq;

namespace UpgradeWorld;

public class ExcludedLocationFilterer(FiltererParameters args) : IZoneFilterer
{
  public Vector2s[] FilterZones(Vector2s[] zones, ref List<string> messages)
  {
    Vector2s[] remaining = [.. zones.Where(zone => !args.IsLocationExcluded(zone))];
    int skipped = zones.Length - remaining.Length;
    if (skipped > 0) messages.Add(skipped + " skipped by excludeLocations");
    return remaining;
  }
}

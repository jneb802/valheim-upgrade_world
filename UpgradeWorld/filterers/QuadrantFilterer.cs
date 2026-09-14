using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UpgradeWorld;

public enum WorldQuadrant
{
  NorthEast,
  NorthWest,
  SouthEast,
  SouthWest
}

/// <summary>Filters positions and zones by their direction from a center point.</summary>
public class QuadrantFilterer(HashSet<WorldQuadrant> quadrants, Vector2 center) : IZoneFilterer
{
  private readonly Vector2 Center = center;
  private readonly HashSet<WorldQuadrant> Quadrants = quadrants;

  public Vector2s[] FilterZones(Vector2s[] zones, ref List<string> messages)
  {
    int amount = zones.Length;
    zones = [.. zones.Where(zone =>
    {
      Vector3 position = ZoneSystem.GetZonePos(zone);
      return Includes(new Vector2(position.x, position.z));
    })];
    int skipped = amount - zones.Length;
    if (skipped > 0) messages.Add(skipped + " skipped by being outside the selected quadrants");
    return zones;
  }

  public bool Includes(Vector2 position) => Quadrants.Contains(GetQuadrant(position, Center));

  /// <summary>
  /// Returns one quadrant for every position. Positions on the center axes belong to
  /// the north or east side so that the four quadrants never overlap.
  /// </summary>
  public static WorldQuadrant GetQuadrant(Vector2 position, Vector2 center)
  {
    bool east = position.x >= center.x;
    bool north = position.y >= center.y;
    if (north) return east ? WorldQuadrant.NorthEast : WorldQuadrant.NorthWest;
    return east ? WorldQuadrant.SouthEast : WorldQuadrant.SouthWest;
  }

  public static bool TryParse(string value, out WorldQuadrant quadrant)
  {
    string normalized = value.Replace("-", "").Replace("_", "");
    if (normalized.Length == 0 || normalized.Any(character => !char.IsLetter(character)))
    {
      quadrant = default;
      return false;
    }
    if (string.Equals(normalized, "ne", StringComparison.OrdinalIgnoreCase)) normalized = "NorthEast";
    else if (string.Equals(normalized, "nw", StringComparison.OrdinalIgnoreCase)) normalized = "NorthWest";
    else if (string.Equals(normalized, "se", StringComparison.OrdinalIgnoreCase)) normalized = "SouthEast";
    else if (string.Equals(normalized, "sw", StringComparison.OrdinalIgnoreCase)) normalized = "SouthWest";
    return Enum.TryParse(normalized, true, out quadrant) && Enum.IsDefined(typeof(WorldQuadrant), quadrant);
  }
}

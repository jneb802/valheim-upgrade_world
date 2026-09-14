using System;
using System.Collections.Generic;
using UpgradeWorld;
using UnityEngine;

internal static class Program
{
  private static int Assertions;

  private static void Main()
  {
    Vector2 center = new(10f, 20f);
    Check(QuadrantFilterer.GetQuadrant(new Vector2(11f, 21f), center) == WorldQuadrant.NorthEast, "northeast");
    Check(QuadrantFilterer.GetQuadrant(new Vector2(9f, 21f), center) == WorldQuadrant.NorthWest, "northwest");
    Check(QuadrantFilterer.GetQuadrant(new Vector2(11f, 19f), center) == WorldQuadrant.SouthEast, "southeast");
    Check(QuadrantFilterer.GetQuadrant(new Vector2(9f, 19f), center) == WorldQuadrant.SouthWest, "southwest");
    Check(QuadrantFilterer.GetQuadrant(center, center) == WorldQuadrant.NorthEast, "center has one owner");
    Check(QuadrantFilterer.GetQuadrant(new Vector2(10f, 19f), center) == WorldQuadrant.SouthEast, "south axis has one owner");
    Check(QuadrantFilterer.GetQuadrant(new Vector2(9f, 20f), center) == WorldQuadrant.NorthWest, "west axis has one owner");
    Check(Parse("northeast") == WorldQuadrant.NorthEast, "full northeast name");
    Check(Parse("north-west") == WorldQuadrant.NorthWest, "hyphenated northwest name");
    Check(Parse("SE") == WorldQuadrant.SouthEast, "southeast short name");
    Check(Parse("sw") == WorldQuadrant.SouthWest, "southwest short name");
    Check(!QuadrantFilterer.TryParse("north", out _), "invalid quadrant rejected");
    Check(!QuadrantFilterer.TryParse("0", out _), "numeric quadrant rejected");
    HashSet<WorldQuadrant> selected = [WorldQuadrant.NorthEast, WorldQuadrant.SouthWest];
    QuadrantFilterer filterer = new(selected, Vector2.zero);
    Check(filterer.Includes(new Vector2(1f, 1f)), "position in selected quadrant included");
    Check(!filterer.Includes(new Vector2(-1f, 1f)), "position outside selected quadrant excluded");
    Vector2s[] zones = [new(1, 1), new(-1, 1), new(1, -1), new(-1, -1)];
    List<string> messages = [];
    Vector2s[] filtered = filterer.FilterZones(zones, ref messages);
    Check(filtered.Length == 2, "multiple quadrants select the expected zone count");
    Check(Array.Exists(filtered, zone => zone == new Vector2s(1, 1)), "northeast zone selected");
    Check(Array.Exists(filtered, zone => zone == new Vector2s(-1, -1)), "southwest zone selected");
    Check(messages.Count == 1, "filtered zone count reported");
    System.Console.WriteLine($"PASS: {Assertions} quadrant assertions.");
  }

  private static WorldQuadrant Parse(string value)
  {
    if (!QuadrantFilterer.TryParse(value, out WorldQuadrant quadrant))
      throw new Exception("Failed to parse " + value);
    return quadrant;
  }

  private static void Check(bool result, string name)
  {
    if (!result) throw new Exception(name);
    Assertions++;
  }
}

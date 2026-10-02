using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;

namespace UpgradeWorld;

// JSON serialization follows the fork's existing StructuredEventWriter.
public static class ZoneResetResultWriter
{
  public static void Write(Dictionary<string, object?> values)
  {
    string json = "{" + string.Join(",", values.Select(value => Quote(value.Key) + ":" + Json(value.Value))) + "}";
    UpgradeWorld.Log.LogInfo("[UpgradeWorldZoneReset] " + json);
    try
    {
      File.AppendAllText(Path.Combine(Paths.ConfigPath, "upgrade_world_zone_reset_results.jsonl"), json + Environment.NewLine, new UTF8Encoding(false));
    }
    catch (Exception exception)
    {
      UpgradeWorld.Log.LogWarning($"Unable to write reset result file; result remains in server log: {exception.Message}");
    }
  }

  private static string Json(object? value)
  {
    if (value == null) return "null";
    if (value is string text) return Quote(text);
    if (value is bool boolean) return boolean ? "true" : "false";
    if (value is int or long or short or byte or double or float or decimal)
      return Convert.ToString(value, CultureInfo.InvariantCulture)!;
    if (value is IEnumerable items)
    {
      List<string> values = [];
      foreach (object? item in items) values.Add(Json(item));
      return "[" + string.Join(",", values) + "]";
    }
    return Quote(value.ToString() ?? "");
  }

  private static string Quote(string value)
  {
    StringBuilder result = new("\"");
    foreach (char character in value)
    {
      if (character == '\\') result.Append("\\\\");
      else if (character == '"') result.Append("\\\"");
      else if (char.IsControl(character)) result.Append("\\u").Append(((int)character).ToString("x4"));
      else result.Append(character);
    }
    return result.Append('"').ToString();
  }
}

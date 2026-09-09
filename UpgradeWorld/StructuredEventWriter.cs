using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;

namespace UpgradeWorld;

public static class StructuredEventWriter
{
  private static readonly object FileLock = new();
  private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

  public static string ToJson(Dictionary<string, object?> values)
  {
    return "{" + string.Join(",", values.Select(KeyValueToJson)) + "}";
  }

  public static void WriteFile(string json)
  {
    string configuredPath = Settings.OperationEventsFile;
    if (string.IsNullOrWhiteSpace(configuredPath))
    {
      return;
    }

    string path = Path.IsPathRooted(configuredPath)
      ? configuredPath
      : Path.Combine(Paths.ConfigPath, configuredPath);
    string? directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    lock (FileLock)
    {
      File.AppendAllText(path, json + Environment.NewLine, Utf8NoBom);
    }
  }

  private static string KeyValueToJson(KeyValuePair<string, object?> value)
  {
    return JsonString(value.Key) + ":" + JsonValue(value.Value);
  }

  private static string JsonValue(object? value)
  {
    if (value == null)
    {
      return "null";
    }
    if (value is string text)
    {
      return JsonString(text);
    }
    if (value is bool boolean)
    {
      return boolean ? "true" : "false";
    }
    if (value is int or long or short or byte)
    {
      return Convert.ToString(value, CultureInfo.InvariantCulture)!;
    }
    if (value is float or double or decimal)
    {
      return Convert.ToString(value, CultureInfo.InvariantCulture)!;
    }
    if (value is IEnumerable enumerable)
    {
      List<string> items = new();
      foreach (object? item in enumerable)
      {
        items.Add(JsonValue(item));
      }
      return "[" + string.Join(",", items) + "]";
    }
    return JsonString(value.ToString() ?? "");
  }

  private static string JsonString(string value)
  {
    StringBuilder builder = new(value.Length + 2);
    builder.Append('"');
    foreach (char character in value)
    {
      switch (character)
      {
        case '\\':
          builder.Append("\\\\");
          break;
        case '"':
          builder.Append("\\\"");
          break;
        case '\n':
          builder.Append("\\n");
          break;
        case '\r':
          builder.Append("\\r");
          break;
        case '\t':
          builder.Append("\\t");
          break;
        default:
          if (char.IsControl(character))
          {
            builder.Append("\\u").Append(((int)character).ToString("x4"));
          }
          else
          {
            builder.Append(character);
          }
          break;
      }
    }
    builder.Append('"');
    return builder.ToString();
  }
}

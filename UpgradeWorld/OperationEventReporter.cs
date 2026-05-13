using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;

namespace UpgradeWorld;

public static class OperationEventReporter
{
  private const string LogPrefix = "[UpgradeWorldEvent] ";
  private static long NextId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  private static readonly object FileLock = new();
  private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

  public static long GetNextId() => ++NextId;

  public static void Emit(ExecutedOperation operation, string eventName, Dictionary<string, object?>? extra = null)
  {
    if (!Settings.OperationEventsEnabled) return;

    var values = new Dictionary<string, object?>
    {
      ["schema"] = 1,
      ["event"] = eventName,
      ["timestamp"] = DateTime.UtcNow.ToString("o"),
      ["operationId"] = operation.OperationId,
      ["operationType"] = operation.GetType().Name,
      ["command"] = operation.CommandText,
      ["info"] = operation.Info,
      ["state"] = operation.State,
      ["success"] = operation.Success,
      ["queuedAt"] = FormatTime(operation.QueuedAt),
      ["startedAt"] = FormatTime(operation.StartedAt),
      ["endedAt"] = FormatTime(operation.EndedAt),
      ["durationMs"] = operation.DurationMs,
      ["exception"] = operation.FailureException?.GetType().Name,
      ["exceptionMessage"] = operation.FailureException?.Message
    };

    foreach (var detail in operation.GetEventDetails())
      values[detail.Key] = detail.Value;
    if (extra != null)
      foreach (var detail in extra)
        values[detail.Key] = detail.Value;

    var json = ToJson(values);
    if (Settings.OperationEventsToLog)
      UpgradeWorld.Log.LogInfo(LogPrefix + json);
    WriteFile(json);
  }

  private static string? FormatTime(DateTime? time) => time?.ToUniversalTime().ToString("o");

  private static void WriteFile(string json)
  {
    var configuredPath = Settings.OperationEventsFile;
    if (string.IsNullOrWhiteSpace(configuredPath)) return;

    var path = Path.IsPathRooted(configuredPath)
      ? configuredPath
      : Path.Combine(Paths.ConfigPath, configuredPath);

    try
    {
      var directory = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(directory))
        Directory.CreateDirectory(directory);
      lock (FileLock)
      {
        File.AppendAllText(path, json + Environment.NewLine, Utf8NoBom);
      }
    }
    catch (Exception e)
    {
      UpgradeWorld.Log.LogWarning($"Failed to write Upgrade World operation event: {e.Message}");
    }
  }

  private static string ToJson(Dictionary<string, object?> values)
  {
    return "{" + string.Join(",", values.Select(kvp => JsonString(kvp.Key) + ":" + JsonValue(kvp.Value))) + "}";
  }

  private static string JsonValue(object? value)
  {
    if (value == null) return "null";
    if (value is string str) return JsonString(str);
    if (value is bool boolean) return boolean ? "true" : "false";
    if (value is int or long or short or byte) return Convert.ToString(value, CultureInfo.InvariantCulture)!;
    if (value is float or double or decimal) return Convert.ToString(value, CultureInfo.InvariantCulture)!;
    if (value is IEnumerable enumerable)
    {
      var values = new List<string>();
      foreach (var item in enumerable)
        values.Add(JsonValue(item));
      return "[" + string.Join(",", values) + "]";
    }
    return JsonString(value.ToString() ?? "");
  }

  private static string JsonString(string value)
  {
    var builder = new StringBuilder(value.Length + 2);
    builder.Append('"');
    foreach (var c in value)
    {
      switch (c)
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
          if (char.IsControl(c))
            builder.Append("\\u").Append(((int)c).ToString("x4"));
          else
            builder.Append(c);
          break;
      }
    }
    builder.Append('"');
    return builder.ToString();
  }
}

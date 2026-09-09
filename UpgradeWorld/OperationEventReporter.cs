using System;
using System.Collections.Generic;

namespace UpgradeWorld;

public static class OperationEventReporter
{
  private const string LogPrefix = "[UpgradeWorldEvent] ";
  private static long NextId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

  public static long GetNextId() => ++NextId;

  public static void Emit(ExecutedOperation operation, string eventName, Dictionary<string, object?>? extra = null)
  {
    try
    {
      EmitUnsafe(operation, eventName, extra);
    }
    catch (Exception e)
    {
      WarnFailure(e);
    }
  }

  private static void EmitUnsafe(ExecutedOperation operation, string eventName, Dictionary<string, object?>? extra)
  {
    if (!Settings.OperationEventsEnabled) return;

    Dictionary<string, object?> values = new()
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
      ["exceptionMessage"] = operation.FailureException?.Message,
      ["skippedReason"] = operation.SkippedReason
    };
    SaveImpactSnapshot.Capture().AddTo(values, "world");

    foreach (var detail in operation.GetEventDetails())
      values[detail.Key] = detail.Value;
    if (extra != null)
      foreach (var detail in extra)
        values[detail.Key] = detail.Value;

    string json = StructuredEventWriter.ToJson(values);
    if (Settings.OperationEventsToLog)
      UpgradeWorld.Log.LogInfo(LogPrefix + json);
    WriteFile(json);
  }

  private static string? FormatTime(DateTime? time) => time?.ToUniversalTime().ToString("o");

  private static void WarnFailure(Exception e)
  {
    try
    {
      UpgradeWorld.Log.LogWarning($"Failed to emit Upgrade World operation event: {e.Message}");
    }
    catch
    {
      // Event reporting must never interrupt operation execution.
    }
  }

  private static void WriteFile(string json)
  {
    try
    {
      StructuredEventWriter.WriteFile(json);
    }
    catch (Exception e)
    {
      WarnFailure(e);
    }
  }
}

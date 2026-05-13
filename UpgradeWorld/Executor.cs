using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
namespace UpgradeWorld;

public static class Executor
{
  private static readonly List<ExecutedOperation> operations = [];
  private static Coroutine? executionCoroutine;
  private static MonoBehaviour? context;
  public static void SetUser(ZRpc? user)
  {
    foreach (var operation in operations) operation.User = user;
  }
  public static void SetContext(MonoBehaviour context)
  {
    Executor.context = context;
  }

  public static void StartExecution()
  {
    if (context == null) throw new Exception("Executor context is not set. Call Executor.SetContext from a MonoBehaviour before starting execution.");
    if (executionCoroutine != null) return;
    executionCoroutine = context.StartCoroutine(ExecuteCoroutine());
  }

  public static void StopExecution()
  {
    if (context == null) throw new Exception("Executor context is not set. Call Executor.SetContext from a MonoBehaviour before stopping execution.");
    for (var i = 0; i < operations.Count; ++i)
    {
      var operation = operations[i];
      if (operation.State is "completed" or "failed" or "cancelled") continue;
      operation.MarkCancelled();
      OperationEventReporter.Emit(operation, "cancelled", new()
      {
        ["queueLength"] = operations.Count - i - 1
      });
    }
    operations.Clear();
    // Needed to indicate end of generation for some mods.
    if (Hud.instance)
      Hud.instance.m_loadingIndicator.SetShowProgress(false);

    if (executionCoroutine == null) return;
    context.StopCoroutine(executionCoroutine);
    executionCoroutine = null;
  }
  public static void AddOperation(ExecutedOperation operation, bool autoStart)
  {
    bool start = Settings.AutoStart || autoStart;
    operation.SetCommand(OperationCommandContext.Current);
    try
    {
      if (!operation.Init(start))
      {
        operation.MarkSkipped("Operation initialization produced no queued work.");
        OperationEventReporter.Emit(operation, "skipped", new()
        {
          ["queueLength"] = operations.Count,
          ["autoStart"] = start,
          ["phase"] = "init"
        });
        return;
      }
    }
    catch (Exception e)
    {
      operation.PrintError(e.Message);
      operation.MarkFailed(e);
      OperationEventReporter.Emit(operation, "failed", new()
      {
        ["queueLength"] = operations.Count,
        ["autoStart"] = start,
        ["phase"] = "init"
      });
      return;
    }
    operation.MarkQueued();
    operations.Add(operation);
    OperationEventReporter.Emit(operation, "queued", new()
    {
      ["queueLength"] = operations.Count,
      ["autoStart"] = start
    });

    if (executionCoroutine == null && start)
      StartExecution();
  }

  public static List<ExecutedOperation> GetOperations()
  {
    return operations;
  }

  private static IEnumerator ExecuteCoroutine()
  {
    var sw = Stopwatch.StartNew();
    while (operations.Count > 0)
    {
      sw.Restart();
      var operation = operations[0];
      operation.MarkRunning();
      OperationEventReporter.Emit(operation, "running", new()
      {
        ["queueLength"] = operations.Count
      });
      yield return operation.Execute(sw);
      operations.RemoveAt(0);
      OperationEventReporter.Emit(operation, operation.Success ? "completed" : "failed", new()
      {
        ["queueLength"] = operations.Count
      });
    }
    sw.Stop();
    StopExecution();
  }
  public const long ProgressMin = 100; // 0.1 seconds
  public const int ZdoMaxUpdates = 10000;
}

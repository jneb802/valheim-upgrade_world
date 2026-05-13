using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
namespace UpgradeWorld;
///<summary>Base class for all operations that need execution. Provides the execution logic.</summary>
public abstract class ExecutedOperation(Terminal context, bool pin = false) : BaseOperation(context, pin)
{
  protected int Failed = 0;
  public long OperationId { get; private set; } = OperationEventReporter.GetNextId();
  public string CommandText { get; private set; } = "";
  public string Info { get; private set; } = "";
  public string State { get; private set; } = "created";
  public DateTime? QueuedAt { get; private set; }
  public DateTime? StartedAt { get; private set; }
  public DateTime? EndedAt { get; private set; }
  public Exception? FailureException { get; private set; }
  public bool Finished { get; private set; }
  public bool Success => Finished && FailureException == null && Failed == 0 && State == "completed";
  public long? DurationMs => StartedAt.HasValue && EndedAt.HasValue ? (long)EndedAt.Value.Subtract(StartedAt.Value).TotalMilliseconds : null;

  public void SetCommand(string command) => CommandText = command;
  public void MarkQueued()
  {
    State = "queued";
    QueuedAt = DateTime.UtcNow;
  }
  public void MarkRunning()
  {
    State = "running";
    StartedAt = DateTime.UtcNow;
  }
  public void MarkCompleted()
  {
    State = "completed";
    Finished = true;
    EndedAt = DateTime.UtcNow;
  }
  public void MarkFailed(Exception exception)
  {
    State = "failed";
    Finished = true;
    FailureException = exception;
    EndedAt = DateTime.UtcNow;
  }
  public void MarkCancelled()
  {
    State = "cancelled";
    Finished = false;
    EndedAt = DateTime.UtcNow;
  }

  public IEnumerator Execute(Stopwatch sw)
  {
    IEnumerator executeEnumerator;
    try
    {
      OnStart();
      executeEnumerator = OnExecute(sw);
    }
    catch (Exception e)
    {
      Helper.Print(Context, User, e.Message);
      MarkFailed(e);
      TryEnd();
      yield break;
    }

    while (true)
    {
      object current;
      try
      {
        if (!executeEnumerator.MoveNext()) break;
        current = executeEnumerator.Current;
      }
      catch (Exception e)
      {
        Helper.Print(Context, User, e.Message);
        MarkFailed(e);
        TryEnd();
        yield break;
      }
      yield return current;
    }

    try
    {
      PrintPins();
      OnEnd();
      MarkCompleted();
    }
    catch (Exception e)
    {
      Helper.Print(Context, User, e.Message);
      MarkFailed(e);
    }
  }

  protected abstract IEnumerator OnExecute(Stopwatch sw);
  public bool Init(bool autoStart)
  {
    var output = OnInit();
    if (output == "") return false;
    Info = output;
    if (!autoStart)
      output += Helper.GetStartMessage();
    Print(output);
    return true;
  }
  protected abstract string OnInit();
  public string GetInfo()
  {
    var info = Info != "" ? Info : OnInit();
    return info != "" ? info : GetType().Name;
  }
  public virtual Dictionary<string, object?> GetEventDetails() => new()
  {
    ["failedErrors"] = Failed
  };
  private void TryEnd()
  {
    try
    {
      OnEnd();
    }
    catch (Exception e)
    {
      UpgradeWorld.Log.LogWarning($"Operation end handler failed: {e.Message}");
    }
  }
  protected virtual void OnStart()
  {
  }
  protected virtual void OnEnd()
  {
  }
}

using System;
using System.Linq;

namespace UpgradeWorld;

public static class OperationCommandContext
{
  [ThreadStatic]
  private static string? CurrentValue;

  public static string Current => CurrentValue ?? "";

  public static void Run(Terminal.ConsoleEventArgs args, Action action)
  {
    var previous = CurrentValue;
    CurrentValue = string.Join(" ", args.Args.Select(arg => arg.ToString()));
    try
    {
      action();
    }
    finally
    {
      CurrentValue = previous;
    }
  }
}

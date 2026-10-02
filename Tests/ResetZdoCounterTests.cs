using System;
using System.Collections.Generic;
using UpgradeWorld;

internal static class ResetZdoCounterTests
{
  private static void Main()
  {
    ZDOID first = new(1, 1);
    ZDOID second = new(1, 2);
    ZDOID unrelated = new(1, 3);
    ZDOID queued = new(1, 4);
    HashSet<ZDOID> world = [first, second, unrelated, queued];
    ResetZdoCounter counter = new([queued], world.Contains);
    Check(!counter.Request(queued), "pre-existing deletion excluded");
    Check(!counter.Request(new ZDOID(1, 99)), "missing object excluded");
    Check(counter.Request(first), "first request accepted");
    Check(!counter.Request(first), "duplicate excluded");
    counter.Confirm();
    Check(counter.Removed == 0 && counter.PendingCount == 1, "request is not a confirmed deletion");
    world.Remove(unrelated);
    counter.Confirm();
    Check(counter.Removed == 0, "unrelated deletion excluded");
    world.Remove(first);
    counter.Confirm();
    counter.Confirm();
    Check(counter.Removed == 1 && counter.PendingCount == 0, "confirmed once");
    Check(counter.Request(second), "linked second object tracked");
    Check(counter.Removed == 1 && counter.PendingCount == 1, "partial result retains pending request");
    ResetZdoCounter next = new([], world.Contains);
    Check(next.Removed == 0 && next.RequestedCount == 0, "next run starts empty");
    world.Remove(second);
    counter.Confirm();
    Check(counter.Removed == 2 && counter.PendingCount == 0, "deferred second deletion confirmed");
    ZDOID external = new(1, 5);
    world.Add(external);
    next.ObserveRequest(external);
    Check(!next.Request(external), "unrelated request queued during reset excluded");
    System.Console.WriteLine("PASS: 12 deletion attribution and confirmation checks.");
  }
  private static void Check(bool value, string name)
  {
    if (!value) throw new Exception(name);
  }
}

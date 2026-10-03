using System;
using System.Collections.Generic;
using HarmonyLib;

namespace UpgradeWorld;

/// <summary>Tracks only deletion requests issued by one reset, then confirms removal by ID.</summary>
public sealed class ResetZdoCounter
{
  internal static ResetZdoCounter? Active { get; set; }
  private readonly HashSet<ZDOID> Requested = [];
  private readonly HashSet<ZDOID> Visited = [];
  private readonly HashSet<ZDOID> Pending = [];
  private readonly HashSet<ZDOID> AlreadyPending;
  private readonly Func<ZDOID, bool> Exists;
  public int Removed { get; private set; }
  public int PendingCount => Pending.Count;
  public int RequestedCount => Requested.Count;
  public bool Visit(ZDOID id) => Visited.Add(id);
  public void ObserveRequest(ZDOID id)
  {
    if (!Requested.Contains(id)) AlreadyPending.Add(id);
  }

  public ResetZdoCounter(IEnumerable<ZDOID> alreadyPending, Func<ZDOID, bool> exists)
  {
    AlreadyPending = new(alreadyPending);
    Exists = exists;
  }

  public bool Request(ZDOID id)
  {
    if (AlreadyPending.Contains(id) || !Exists(id) || !Requested.Add(id)) return false;
    Pending.Add(id);
    return true;
  }

  public void Confirm()
  {
    // O(requested objects), never a scan of every world object.
    Removed += Pending.RemoveWhere(id => !Exists(id));
  }
}

// Other mods can queue removals while the reset yields. Exclude those requests too.
[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.DestroyZDO))]
internal static class ObserveResetExternalDeletions
{
  private static void Prefix(ZDOMan __instance, ZDO zdo, out (ZDOID Id, int Count) __state)
  {
    __state = (zdo.m_uid, __instance.m_destroySendList.Count);
  }
  private static void Postfix(ZDOMan __instance, (ZDOID Id, int Count) __state)
  {
    if (__instance.m_destroySendList.Count > __state.Count)
      ResetZdoCounter.Active?.ObserveRequest(__state.Id);
  }
}

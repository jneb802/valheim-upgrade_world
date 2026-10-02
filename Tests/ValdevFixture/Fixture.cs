using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using UnityEngine;
using HarmonyLib;

[BepInPlugin("odin.warp89.fixture", "WARP-89 Test Fixture", "1.0.0")]
public sealed class Fixture : BaseUnityPlugin
{
  public static bool Hold;
  public static bool Fault;
  public static bool Foreign;
  public static ZDOID ForeignId;
  private readonly List<ZDOID> Targets = new();
  private readonly List<ZDOID> Protected = new();
  private void Start()
  {
    new Harmony("odin.warp89.fixture").PatchAll();
    new Terminal.ConsoleCommand("uw89_fixture", "Create WARP-89 controlled ZDOs in a test world", args => Setup());
    new Terminal.ConsoleCommand("uw89_proof", "Inspect WARP-89 controlled ZDO IDs", args => Proof());
    new Terminal.ConsoleCommand("uw89_empty", "Mark an empty test zone generated", args => ZoneSystem.instance.m_generatedZones.Add(new Vector2s(-202, -202)));
    new Terminal.ConsoleCommand("uw89_hold", "Hold queued destruction during this test", args => Hold = true);
    new Terminal.ConsoleCommand("uw89_release", "Release queued destruction", args => Hold = false);
    new Terminal.ConsoleCommand("uw89_fault", "Fail the next zone reset after it runs one zone", args => Fault = true);
    new Terminal.ConsoleCommand("uw89_again", "Prepare another batch", args => { Targets.Clear(); Protected.Clear(); Setup(); });
    new Terminal.ConsoleCommand("uw89_playercheck", "Check the real player-skip path", args => PlayerCheck());
    new Terminal.ConsoleCommand("uw89_foreign", "Queue one external deletion at the start of the next reset", args => Foreign = true);
  }
  private ZDO Create(int x, int z, int prefab)
  {
    ZDO item = ZDOMan.instance.CreateNewZDO(new Vector3(x * 64, 50, z * 64), prefab);
    item.SetPrefab(prefab);
    item.Persistent = true;
    item.SetOwnerInternal(ZDOMan.GetSessionID());
    return item;
  }
  private void Setup()
  {
    if (Targets.Count > 0) throw new InvalidOperationException("Fixture already created.");
    for (int i = 0; i < 10; i++) Targets.Add(Create(-200, -200, "Wood".GetStableHashCode()).m_uid);
    ZDO root = ZDOMan.instance.GetZDO(Targets[0]);
    ForeignId = root.m_uid;
    ZDO linked = Create(-210, -210, "Stone".GetStableHashCode());
    root.SetConnection(ZDOExtraData.ConnectionType.Spawned, linked.m_uid);
    Targets.Add(linked.m_uid);
    ZDO baseItem = Create(-205, -205, "piece_workbench".GetStableHashCode());
    baseItem.Set(ZDOVars.s_creator, 1234L);
    Protected.Add(baseItem.m_uid);
    ZDO terrain = Create(-207, -207, "_TerrainCompiler".GetStableHashCode());
    terrain.Set(ZDOVars.s_TCData, new byte[] {1});
    Protected.Add(terrain.m_uid);
    Protected.Add(Create(200, 200, "Wood".GetStableHashCode()).m_uid);
    foreach (Vector2s zone in new[] {new Vector2s(-200,-200),new Vector2s(-205,-205),new Vector2s(-207,-207),new Vector2s(200,200)})
      ZoneSystem.instance.m_generatedZones.Add(zone);
    Proof();
  }
  private void Proof()
  {
    int present = Targets.Count(id => ZDOMan.instance.m_objectsByID.ContainsKey(id));
    int protectedPresent = Protected.Count(id => ZDOMan.instance.m_objectsByID.ContainsKey(id));
    string text = $"WARP89_PROOF targets={Targets.Count} present={present} removed={Targets.Count-present} protected={protectedPresent}/{Protected.Count} world={ZDOMan.instance.m_objectsByID.Count}";
    Logger.LogInfo(text);
    Console.instance.Print(text);
  }
  private void PlayerCheck()
  {
    ZDO item = Create(-212, -212, "Player".GetStableHashCode());
    ZNetPeer peer = (ZNetPeer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ZNetPeer));
    peer.m_characterID = item.m_uid;
    Type type = AccessTools.TypeByName("UpgradeWorld.ResetZdoCounter");
    Func<ZDOID, bool> exists = ZDOMan.instance.m_objectsByID.ContainsKey;
    object counter = Activator.CreateInstance(type, new object[] {ZDOMan.instance.m_destroySendList, exists});
    ZNet.instance.m_peers.Add(peer);
    try { AccessTools.Method(AccessTools.TypeByName("UpgradeWorld.Helper"), "RemoveZDO", new[] {typeof(ZDO), type}).Invoke(null, new object[] {item, counter}); }
    finally { ZNet.instance.m_peers.Remove(peer); }
    string text = $"WARP89_PLAYER_SKIP present={exists(item.m_uid)} requested={type.GetProperty("RequestedCount").GetValue(counter)}";
    Logger.LogInfo(text); Console.instance.Print(text);
    ZDOMan.instance.DestroyZDO(item);
  }
}

[HarmonyPatch(typeof(ZDOMan), "SendDestroyed")]
internal static class HoldDestruction
{
  private static bool Prefix() => !Fixture.Hold;
}

[HarmonyPatch]
internal static class FailReset
{
  private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("UpgradeWorld.ResetZones"), "ExecuteZone");
  private static void Prefix()
  {
    if (!Fixture.Foreign) return;
    Fixture.Foreign = false;
    ZDOMan.instance.DestroyZDO(ZDOMan.instance.GetZDO(Fixture.ForeignId));
    Console.instance.Print("WARP89_FOREIGN_REQUEST one target queued outside the reset helper");
  }
  private static void Postfix()
  {
    if (!Fixture.Fault) return;
    Fixture.Fault = false;
    throw new InvalidOperationException("WARP89_EXPECTED_FAILURE after one reset zone");
  }
}

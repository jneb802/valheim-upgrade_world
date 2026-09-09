using Service;
using System.Collections.Generic;
using System.Linq;

namespace UpgradeWorld;
/// <summary>Searchs objects from chests.</summary>
public class SearchChests : EntityOperation
{
  public SearchChests(Terminal context, IEnumerable<string> ids, DataParameters args) : base(context, args.Pin)
  {
    Search(ids, args);
  }
  private string SearchStand(ZDO zdo, string prefix, HashSet<int> ids)
  {
    var item = zdo.GetString(prefix + "item", "");
    if (item == "") return "";
    if (!ids.Contains(item.GetStableHashCode())) return "";
    var variant = zdo.GetInt(prefix + "variant");
    var quality = zdo.GetInt(prefix + "quality");
    if (variant > 1) item += ", style " + variant + "";
    if (quality > 1) item += ", level " + quality + "";
    return item;
  }
  private void Search(IEnumerable<string> ids, DataParameters args)
  {
    var prefabs = GetPrefabs(ids, args.Types);
    var zdos = GetZDOs(args);

    var zs = ZNetScene.instance;
    string[] prefixes = ["", "0_", "1_", "2_", "3_", "4_", "5_", "6_", "7_", "8_", "9_"];
    var standContents = zdos.Select(zdo =>
    {
      var content = prefixes.Select(prefix => SearchStand(zdo, prefix, prefabs)).Where(x => x != "").ToList();
      if (content.Count == 0) return "";
      var name = zs.m_namedPrefabs[zdo.m_prefab].name;
      var id = name + " " + zdo.m_uid.ID + " " + Helper.PrintVectorXZY(zdo.GetPosition());
      return id + "\n" + string.Join("\n", content);
    }).Where(x => x != "").ToList();

    if (args.Log) Log(standContents);
    else Print(standContents, false);

    var chestContents = zdos.Select(zdo =>
    {
      var items = zdo.GetString(ZDOVars.s_items);
      if (items == "") return "";
      if (!InventoryData.TryRead(items, out InventoryData? inventory, out string error))
      {
        if (error.Length > 0) Print($"Skipped chest {zdo.m_uid}: {error}");
        return "";
      }
      var content = SearchChest(inventory!, prefabs);
      if (content.Count == 0) return "";
      AddPin(zdo.GetPosition());
      var name = zs.m_namedPrefabs[zdo.m_prefab].name;
      var id = name + " " + zdo.m_uid.ID + " " + Helper.PrintVectorXZY(zdo.GetPosition());
      return id + "\n" + string.Join("\n", content.Select(x => x.Key + ": " + x.Value));
    }).Where(x => x != "").ToList();


    if (args.Log) Log(chestContents);
    else Print(chestContents, false);
    PrintPins();
  }

  private Dictionary<string, int> SearchChest(InventoryData inventory, HashSet<int> ids)
  {
    Dictionary<string, int> amounts = [];
    foreach (InventoryData.Entry item in inventory.Items)
    {
      if (!ids.Contains(item.Prefab)) continue;
      if (!ZNetScene.instance.m_namedPrefabs.TryGetValue(item.Prefab, out UnityEngine.GameObject prefab)) continue;
      string variant = item.Variant > 0 ? $", style {item.Variant}" : "";
      string quality = item.Quality > 1 ? $", level {item.Quality}" : "";
      string key = prefab.name + variant + quality;
      amounts.TryGetValue(key, out int count);
      amounts[key] = count + item.Stack;
    }
    return amounts;
  }
}

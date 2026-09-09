using System.Linq;
using Service;

namespace UpgradeWorld;
/// <summary>Removes missing objects from chests.</summary>
public class CleanChests : EntityOperation
{
  public CleanChests(Terminal context, ZDO[] zdos, bool pin, bool alwaysPrint) : base(context, pin)
  {
    Clean(zdos, alwaysPrint);
  }

  private void Clean(ZDO[] zdos, bool alwaysPrint)
  {
    var removed = 0;
    foreach (var zdo in zdos)
    {
      var items = zdo.GetString(ZDOVars.s_items);
      if (items == "") continue;
      if (!InventoryData.TryRead(items, out InventoryData? inventory, out string error))
      {
        if (error.Length > 0) Print($"Skipped chest {zdo.m_uid}: {error}");
        continue;
      }
      InventoryData.Entry[] retained = inventory!.Items
        .Where(item => ZNetScene.instance.m_namedPrefabs.ContainsKey(item.Prefab)).ToArray();
      int result = inventory.Items.Count - retained.Length;
      if (result == 0) continue;
      AddPin(zdo.m_position);
      removed += result;
      if (!zdo.IsOwner())
        zdo.SetOwner(ZDOMan.GetSessionID());
      zdo.Set(ZDOVars.s_items, inventory.Write(retained));
    }
    if (alwaysPrint || removed > 0)
      Print($"Removed {removed} missing object{S(removed)} from chests");
  }

}

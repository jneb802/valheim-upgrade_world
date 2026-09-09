using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Service;
using SoftReferenceableAssets;
using UpgradeWorld;
using UnityEngine;

internal static class Program
{
  private static int assertions;

  private static void Main()
  {
    foreach (int version in Enumerable.Range(100, 10))
    {
      byte[] first = Item(version, "Wood", 12, 3, 2);
      byte[] second = Item(version, "MissingModItem", 7, 1, 0);
      string original = Inventory(version, first, second);
      InventoryData inventory = InventoryData.Read(original);
      Check(inventory.Items.Count == 2, $"v{version} count");
      Check(inventory.Items[0].Prefab == "Wood".GetStableHashCode(), $"v{version} prefab");
      Check(inventory.Items[0].Stack == 12, $"v{version} stack");
      Check(inventory.Items[0].Quality == (version >= 101 ? 3 : 1), $"v{version} quality");
      Check(inventory.Items[0].Variant == (version >= 102 ? 2 : 0), $"v{version} variant");
      Check(inventory.Write(inventory.Items) == original, $"v{version} exact round trip");
      string cleaned = inventory.Write(inventory.Items.Take(1));
      Check(cleaned == Inventory(version, first), $"v{version} retains exact item bytes");
      Check(InventoryData.Read(cleaned).Items.Count == 1, $"v{version} corrected count");
      byte[] bytes = Convert.FromBase64String(original);
      foreach (int length in Enumerable.Range(0, bytes.Length))
        Check(!InventoryData.TryRead(Convert.ToBase64String(bytes.Take(length).ToArray()), out _, out _), $"v{version} truncation {length}");
      Check(!InventoryData.TryRead(Convert.ToBase64String(bytes.Concat(new byte[] { 0 }).ToArray()), out _, out _), $"v{version} trailing bytes");
    }
    Check(InventoryData.Empty() == Inventory(109), "current empty inventory uses ushort count");
    Check(InventoryData.Read(InventoryData.Empty()).Items.Count == 0, "empty inventory loads");
    Check(!InventoryData.TryRead("invalid base64", out _, out _), "malformed base64 rejected");
    Check(!InventoryData.TryRead(Inventory(110), out _, out _), "future version rejected");
    Check(!InventoryData.TryRead(Inventory(0), out _, out string error) && error == "", "Item Drawers skipped");
    Check(Service.Parse.Zone("32767,-32768") == new Vector2s(32767, -32768), "zone limits accepted");
    Check(!Zones.TryCreate(32768, 0, out _) && !Zones.TryCreate(0, -32769, out _), "adjacent zones do not wrap");
    try
    {
      Service.Parse.Zone("32768,0");
      throw new Exception("Overflowing zone accepted");
    }
    catch (InvalidOperationException) { assertions++; }
    TestLocations();
    System.Console.WriteLine($"PASS: {assertions} assertions; inventories, malformed data, zone bounds, location caches and biome rules.");
  }

  private static void TestLocations()
  {
    // Allocate managed state without invoking any Unity object constructor.
    ZoneSystem system = (ZoneSystem)FormatterServices.GetUninitializedObject(typeof(ZoneSystem));
    ZoneSystem.s_instance = system;
    system.m_locationInstances = [];
    system.m_locationIDCache = [];
    system.m_locationGroupCache = [];
    system.m_locationMaxGroupCache = [];
    ZoneSystem.ZoneLocation first = new() { m_name = "first", m_group = "minimum", m_groupMax = "maximum" };
    ZoneSystem.ZoneLocation second = new() { m_name = "second", m_group = "new minimum", m_groupMax = "new maximum" };
    Vector2s zone = new(1, 2);
    ZoneSystem.LocationInstance instance = new() { m_location = first, m_position = new Vector3(64, 10, 128), m_placed = true };
    LocationRegistry.Set(zone, instance);
    Check(system.HaveLocationInRange(first.m_prefab.m_assetID, "", instance.m_position, 1), "registered location affects distance checks");
    Check(system.m_locationGroupCache["minimum"].Count == 1 && system.m_locationMaxGroupCache["maximum"].Count == 1, "both group caches populated");
    instance.m_location = second;
    LocationRegistry.Set(zone, instance);
    Check(!system.m_locationGroupCache.ContainsKey("minimum") && !system.m_locationMaxGroupCache.ContainsKey("maximum"), "swap clears old groups");
    Check(system.m_locationIDCache[first.m_prefab.m_assetID].Count == 1, "swap does not duplicate same asset");
    LocationRegistry.Remove(zone);
    Check(system.m_locationInstances.Count == 0 && system.m_locationIDCache.Count == 0 && system.m_locationGroupCache.Count == 0 && system.m_locationMaxGroupCache.Count == 0, "remove clears all caches");
    Check(!system.HaveLocationInRange(first.m_prefab.m_assetID, "", instance.m_position, 1), "removed location no longer blocks placement");
    system.m_locationInstances[zone] = instance;
    LocationRegistry.RebuildCaches();
    Check(system.m_locationGroupCache["new minimum"].Count == 1, "rebuild includes direct registrations");
    instance.m_placed = false;
    LocationRegistry.Set(zone, instance);
    Check(!system.m_locationGroupCache["new minimum"][0].m_placed, "placed flag updates in caches");
    BiomeSector sector = new(null!, Heightmap.Biome.Meadows);
    Check(LocationRegistry.Allows(first, sector), "ordinary location allowed");
    first.AltBiomeParent = "required";
    Check(!LocationRegistry.Allows(first, sector), "missing alternate biome rejected");
    sector.AltBiomes.Add(new AltBiome { m_name = "required" });
    Check(LocationRegistry.Allows(first, sector), "required alternate biome accepted");
    sector.AltBiomes[0].m_blockLocationNames.Add("first");
    Check(!LocationRegistry.Allows(first, sector), "blocked location rejected");
    ZoneSystem.s_instance = null!;
  }

  private static void Check(bool result, string name)
  {
    if (!result) throw new Exception(name);
    assertions++;
  }

  private static string Inventory(int version, params byte[][] items)
  {
    using MemoryStream stream = new();
    using BinaryWriter writer = new(stream);
    writer.Write(version);
    if (version >= 108) writer.Write((ushort)items.Length);
    else writer.Write(items.Length);
    foreach (byte[] item in items) writer.Write(item);
    return Convert.ToBase64String(stream.ToArray());
  }

  // Fixtures follow the game's legacy and compact schemas independently of the parser.
  private static byte[] Item(int version, string name, int stack, int quality, int variant)
  {
    using MemoryStream stream = new();
    using BinaryWriter writer = new(stream);
    if (version >= 108)
    {
      writer.Write(12345); // Durability hundredths.
      writer.Write((byte)2); writer.Write((byte)3); writer.Write((byte)1);
      writer.Write((byte)255); // Every optional field, picked-up and equipped.
      writer.Write((ushort)quality); writer.Write((ushort)stack); writer.Write(variant);
      writer.Write(123456789L); writer.Write("Crafter"); writer.Write(name.GetStableHashCode());
      writer.Write((byte)1); // Compact custom-data count.
      writer.Write("mod:field"); writer.Write("retain this exact value");
      if (version >= 109) writer.Write((byte)1); // Cheated flag.
    }
    else
    {
      writer.Write(name); writer.Write(stack); writer.Write(123.456f);
      writer.Write(2); writer.Write(3); writer.Write(true);
      if (version >= 101) writer.Write(quality);
      if (version >= 102) writer.Write(variant);
      if (version >= 103) { writer.Write(123456789L); writer.Write("Crafter"); }
      if (version >= 104) { writer.Write(1); writer.Write("mod:field"); writer.Write("retain this exact value"); }
      if (version >= 105) writer.Write(1);
      if (version >= 106) writer.Write(true);
      if (version >= 107) writer.Write(true);
    }
    return stream.ToArray();
  }
}

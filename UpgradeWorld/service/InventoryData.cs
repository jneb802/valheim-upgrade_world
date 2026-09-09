using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Service;

// Preserve each surviving item's original bytes, including data from other mods.
internal sealed class InventoryData
{
  internal sealed class Entry(int prefab, int stack, int quality, int variant, byte[] bytes)
  {
    public readonly int Prefab = prefab;
    public readonly int Stack = stack;
    public readonly int Quality = quality;
    public readonly int Variant = variant;
    public readonly byte[] Bytes = bytes;
  }

  public readonly List<Entry> Items = [];
  private readonly int version;

  private InventoryData(int version) => this.version = version;

  public static InventoryData Read(string encoded)
  {
    ZPackage package = new(encoded);
    int version = package.ReadInt();
    if (version < 100 || version > (int)Version.c_ItemDataVersion)
      throw new InvalidDataException($"Unsupported inventory version {version}.");
    InventoryData inventory = new(version);
    int count = version >= (int)Version.Item.Smaller ? package.ReadUShort() : package.ReadInt();
    if (count < 0 || count > ushort.MaxValue || count > package.Size() - package.GetPos())
      throw new InvalidDataException("Invalid inventory item count.");
    byte[] bytes = package.GetArray();
    for (int index = 0; index < count; index++)
    {
      int start = package.GetPos();
      int prefab;
      ItemDrop.ItemData item;
      if (version >= (int)Version.Item.Smaller)
        (prefab, item) = ItemDrop.ItemData.Load(package, (Version.Item)version);
      else
        (prefab, item) = ReadLegacyItem(package, (Version.Item)version);
      int length = package.GetPos() - start;
      byte[] original = new byte[length];
      Array.Copy(bytes, start, original, 0, length);
      inventory.Items.Add(new Entry(prefab, item.m_stack, item.m_quality, item.m_variant, original));
    }
    if (package.GetPos() != package.Size())
      throw new InvalidDataException("Unexpected trailing inventory data.");
    return inventory;
  }

  public static bool TryRead(string encoded, out InventoryData? inventory, out string error)
  {
    inventory = null;
    error = "";
    try
    {
      // Item Drawers uses the same object field with version zero.
      if (new ZPackage(encoded).ReadInt() == 0) return false;
      inventory = Read(encoded);
      return true;
    }
    catch (Exception exception) when (exception is InvalidDataException || exception is IOException || exception is FormatException || exception is ArgumentException)
    {
      error = exception.Message;
      return false;
    }
  }

  private static (int, ItemDrop.ItemData) ReadLegacyItem(ZPackage package, Version.Item version)
  {
    string name = package.ReadString();
    ItemDrop.ItemData item = new();
    item.m_stack = package.ReadInt();
    item.m_durability = package.ReadSingle();
    item.m_gridPos = package.ReadVector2i();
    item.m_equipped = package.ReadBool();
    if (version >= Version.Item.Quality) item.m_quality = package.ReadInt();
    if (version >= Version.Item.Variant) item.m_variant = package.ReadInt();
    if (version >= Version.Item.CrafterID)
    {
      item.m_crafterID = package.ReadLong();
      item.m_crafterName = package.ReadString();
    }
    if (version >= Version.Item.CustomData)
    {
      int count = package.ReadInt();
      if (count < 0 || count > (package.Size() - package.GetPos()) / 2)
        throw new InvalidDataException("Invalid custom item data count.");
      for (int index = 0; index < count; index++)
        item.m_customData[package.ReadString()] = package.ReadString();
    }
    if (version >= Version.Item.WorldLevel) item.m_worldLevel = package.ReadInt();
    if (version >= Version.Item.PickedUp) item.m_pickedUp = package.ReadBool();
    if (version == Version.Item.AbandonedDN) item.m_cheated = package.ReadBool();
    return (name.Length == 0 ? 0 : name.GetStableHashCode(), item);
  }

  public string Write(IEnumerable<Entry> items)
  {
    Entry[] entries = items.ToArray();
    using MemoryStream stream = new();
    using BinaryWriter writer = new(stream);
    writer.Write(version);
    if (version >= (int)Version.Item.Smaller) writer.Write(checked((ushort)entries.Length));
    else writer.Write(entries.Length);
    foreach (Entry entry in entries) writer.Write(entry.Bytes);
    return Convert.ToBase64String(stream.ToArray());
  }

  public static string Empty() => new InventoryData((int)Version.c_ItemDataVersion).Write([]);
}

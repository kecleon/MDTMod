using System.Collections.Generic;
using System.Xml.Serialization;

namespace MDTadusMod.Data
{
    public class AccountData
    {
        public Guid AccountId { get; set; }
        public bool PasswordError { get; set; }

        // Account-wide info
        public string Name { get; set; }
        public int Credits { get; set; }
        public int Fame { get; set; }
        public int MaxNumChars { get; set; }
        public string GuildName { get; set; }
        public int GuildRank { get; set; }
        public int Star { get; set; }

        public List<Character> Characters { get; set; } = new List<Character>();
        public VaultData Vault { get; set; } = new VaultData();
        public VaultData MaterialStorage { get; set; } = new();
        public List<string> Potions { get; set; } = new();
        public List<Item> Gifts { get; set; } = new();
        public List<Item> TemporaryGifts { get; set; } = new();

        [XmlIgnore]
        public Dictionary<string, List<string>> UniqueItemData { get; set; } = new();
        [XmlIgnore]
        public Dictionary<string, List<string>> UniqueGiftItemData { get; set; } = new();
        [XmlIgnore]
        public Dictionary<string, List<string>> UniqueTemporaryGiftItemData { get; set; } = new();
        [XmlIgnore]
        public Dictionary<string, List<string>> MaterialStorageItemData { get; set; } = new();

        [XmlArray("UniqueItemData")]
        [XmlArrayItem("ItemData")]
        public ItemDataEntry[] SerializableUniqueItemData
        {
            get => UniqueItemData?.Select(kvp => new ItemDataEntry { Key = kvp.Key, Value = string.Join(",", kvp.Value) }).ToArray();
            set => UniqueItemData = value?.ToDictionary(i => i.Key, i => (i.Value ?? string.Empty).Split(',').ToList()) ?? new();
        }

        [XmlArray("UniqueGiftItemData")]
        [XmlArrayItem("ItemData")]
        public ItemDataEntry[] SerializableUniqueGiftItemData
        {
            get => UniqueGiftItemData?.Select(kvp => new ItemDataEntry { Key = kvp.Key, Value = string.Join(",", kvp.Value) }).ToArray();
            set => UniqueGiftItemData = value?.ToDictionary(i => i.Key, i => (i.Value ?? string.Empty).Split(',').ToList()) ?? new();
        }

        [XmlArray("UniqueTemporaryGiftItemData")]
        [XmlArrayItem("ItemData")]
        public ItemDataEntry[] SerializableUniqueTemporaryGiftItemData
        {
            get => UniqueTemporaryGiftItemData?.Select(kvp => new ItemDataEntry { Key = kvp.Key, Value = string.Join(",", kvp.Value) }).ToArray();
            set => UniqueTemporaryGiftItemData = value?.ToDictionary(i => i.Key, i => (i.Value ?? string.Empty).Split(',').ToList()) ?? new();
        }

        [XmlArray("MaterialStorageItemData")]
        [XmlArrayItem("ItemData")]
        public ItemDataEntry[] SerializableMaterialStorageItemData
        {
            get => MaterialStorageItemData?.Select(kvp => new ItemDataEntry { Key = kvp.Key, Value = string.Join(",", kvp.Value) }).ToArray();
            set => MaterialStorageItemData = value?.ToDictionary(i => i.Key, i => (i.Value ?? string.Empty).Split(',').ToList()) ?? new();
        }
    }

    public class VaultData
    {
        public List<ChestData> Chests { get; set; } = new();
    }

    public class ChestData
    {
        public List<Item> Items { get; set; } = new();
    }
}
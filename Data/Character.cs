using System.Collections.Generic;
using System.Xml.Serialization;
using MDTadusMod.Services;

namespace MDTadusMod.Data
{
    public class Character
    {
        public int Id { get; set; }
        public int ObjectType { get; set; }
        public int Skin { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public int CurrentFame { get; set; }
        public string Equipment { get; set; }
        public string EquipQS { get; set; }
        public int MaxHitPoints { get; set; }
        public int MaxMagicPoints { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Speed { get; set; }
        public int Dexterity { get; set; }
        public int Vitality { get; set; }
        public int Wisdom { get; set; }
        public bool HasBackpack { get; set; }
        [XmlIgnore] // Ignore the raw string, we will save the parsed data instead
        public string PCStats { get; set; }
        public bool Seasonal { get; set; }

        [XmlIgnore]
        public Dictionary<string, List<string>> UniqueItemData { get; set; } = new();

        [XmlArray("UniqueItemData")]
        [XmlArrayItem("ItemData")]
        public ItemDataEntry[] SerializableUniqueItemData
        {
            get
            {
                if (UniqueItemData == null) return null;
                return UniqueItemData.Select(kvp => new ItemDataEntry { Key = kvp.Key, Value = string.Join(",", kvp.Value) }).ToArray();
            }
            set
            {
                if (value == null)
                {
                    UniqueItemData = new Dictionary<string, List<string>>();
                }
                else
                {
                    UniqueItemData = value.ToDictionary(i => i.Key, i => (i.Value ?? string.Empty).Split(',').ToList());
                }
            }
        }

        [XmlIgnore]
        public Dictionary<int, long> ParsedPCStats { get; private set; } = new();

        [XmlArray("PCStats")]
        [XmlArrayItem("Stat")]
        public PCStatEntry[] SerializablePCStats {
            get {
                return ParsedPCStats?.Select(kvp => new PCStatEntry { Id = kvp.Key, Value = kvp.Value }).ToArray();
            }
            set {
                ParsedPCStats = value?.ToDictionary(e => e.Id, e => e.Value) ?? new Dictionary<int, long>();
            }
        }

        public void ParsePCStats()
        {
            if (!string.IsNullOrEmpty(PCStats))
            {
                ParsedPCStats = MDTadusMod.Services.PCStatsParser.Parse(PCStats);
            }
        }
    }

    public class ItemDataEntry
    {
        [XmlAttribute("type")]
        public string Key { get; set; }

        [XmlText]
        public string Value { get; set; }
    }

    public class PCStatEntry
    {
        [XmlAttribute("index")]
        public int Id { get; set; }

        [XmlText]
        public long Value { get; set; }
    }
}
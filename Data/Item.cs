using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace MDTadusMod.Data
{
    public class Item
    {
        public int Id { get; set; }
        public string RawEnchantData { get; set; }

        [XmlIgnore]
        public List<KeyValuePair<int, int>> ParsedEnchantments { get; private set; } = new();

        [XmlArray("ParsedEnchantments")]
        [XmlArrayItem("Enchantment")]
        public EnchantmentEntry[] SerializableParsedEnchantments
        {
            get => ParsedEnchantments?.Select(kvp => new EnchantmentEntry { Key = kvp.Key, Value = kvp.Value }).ToArray();
            set => ParsedEnchantments = value?.Select(e => new KeyValuePair<int, int>(e.Key, e.Value)).ToList() ?? new();
        }

        public Item() { }
        public Item(int id, string rawEnchantData = null)
        {
            Id = id;
            RawEnchantData = rawEnchantData;
            if (!string.IsNullOrEmpty(RawEnchantData))
            {
                ParseEnchantments();
            }
        }

        public void ParseEnchantments()
        {
            if (string.IsNullOrEmpty(RawEnchantData)) return;

            string standardBase64 = RawEnchantData.Replace('_', '/').Replace('-', '+');
            int padding = standardBase64.Length % 4;
            if (padding != 0)
            {
                standardBase64 += new string('=', 4 - padding);
            }

            try
            {
                byte[] decodedBytes = Convert.FromBase64String(standardBase64);
                using var memoryStream = new MemoryStream(decodedBytes);
                using var reader = new BinaryReader(memoryStream);

                if (reader.BaseStream.Length < 3) return;
                reader.BaseStream.Position = 3;

                while (reader.BaseStream.Position + 2 <= reader.BaseStream.Length)
                {
                    ushort enchantId = reader.ReadUInt16();

                    if (enchantId == 0xFFFD) break;

                    if (enchantId == 0xFFFE)
                    {
                        ParsedEnchantments.Add(new KeyValuePair<int, int>(-1, 0));
                    }
                    else
                    {
                        ParsedEnchantments.Add(new KeyValuePair<int, int>(0, enchantId));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to parse enchantment data for ID {Id}. Error: {ex.Message}");
            }
        }
    }

    public class EnchantmentEntry
    {
        [XmlAttribute]
        public int Key { get; set; }
        [XmlAttribute]
        public int Value { get; set; }
    }
}
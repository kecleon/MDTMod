using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace MDTadusMod.Data
{
    public class Item
    {
        [XmlAttribute]
        public int Id { get; set; }

        [XmlAttribute("Enchants")]
        [DefaultValue("")]
        public string RawEnchantData { get; set; }

        [XmlIgnore]
        public List<KeyValuePair<int, int>> ParsedEnchantments { get; private set; } = new();

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

            ParsedEnchantments.Clear();
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
}
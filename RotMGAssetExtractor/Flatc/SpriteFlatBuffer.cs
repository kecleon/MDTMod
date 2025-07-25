using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;

namespace RotMGAssetExtractor.Flatc
{
    public static class SpriteFlatBuffer
    {
        private static readonly Dictionary<string, Dictionary<int, (int AtlasId, int[] Coords)>> _spriteMap = new();

        public static void LoadFromDecompiled(DecompiledSpriteSheet sheet)
        {
            _spriteMap.Clear();
            foreach (var group in sheet.SpriteGroups)
            {
                var name = Clean(group.Name);
                var dict = new Dictionary<int, (int AtlasId, int[] Coords)>();
                foreach (var s in group.Sprites)
                    dict[s.Index] = (s.AtlasId, new[] { s.X, s.Y, s.W, s.H });
                _spriteMap[name] = dict;
            }

            foreach (var k in _spriteMap.Keys)
                Debug.WriteLine("Group: " + k);
        }

        public static Dictionary<string, Dictionary<int, (int AtlasId, int[] Coords)>> GetSprites() => _spriteMap;

        private static string Clean(string s) =>
            string.IsNullOrEmpty(s)
                ? "SpriteSheet"
                : new string(s.Where(c => c == '\t' || c == '\n' || c == '\r' || c >= 0x20).ToArray());
    }

    [Serializable]
    public class DecompiledSpriteSheet
    {
        public List<SpriteGroup> SpriteGroups { get; set; } = new();
    }

    [Serializable]
    public class SpriteGroup
    {
        [XmlAttribute] public string Name { get; set; }
        public List<SpriteInfo> Sprites { get; set; } = new();
    }

    [Serializable]
    public class SpriteInfo
    {
        [XmlAttribute] public int Index { get; set; }
        [XmlAttribute] public int AtlasId { get; set; }
        [XmlAttribute] public int X { get; set; }
        [XmlAttribute] public int Y { get; set; }
        [XmlAttribute] public int W { get; set; }
        [XmlAttribute] public int H { get; set; }
    }
}

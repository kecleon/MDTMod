using MDTadusMod.Data;
using RotMGAssetExtractor.Model;
using System.Buffers.Binary;
using System.Diagnostics;

namespace MDTadusMod.Services
{
    public static class PCStatsParser
    {
        public static Dictionary<int, long> Parse(string pcstats)
        {
            if (string.IsNullOrEmpty(pcstats)) return new();

            try
            {
                var b64 = pcstats.Replace('-', '+').Replace('_', '/');
                b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
                var bytes = Convert.FromBase64String(b64);

                int i = 4;                                  // skip 4 unknown bytes
                if (bytes.Length < 20) return new();

                /* ---------- flag vector (bytes 4‑19) ---------- */
                var flagged = new List<int>();
                int statsCount = 0, totalBits = 0;

                while (i < 20)
                {
                    uint chunk = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i, 4));
                    i += 4; totalBits += 32;

                    while (statsCount < totalBits)
                    {
                        if ((chunk & (1u << (statsCount % 32))) != 0)
                            flagged.Add(statsCount);
                        statsCount++;
                    }
                }

                /* ---------- values ---------- */
                var values = new List<long>();
                while (values.Count < flagged.Count && i < bytes.Length)
                    values.Add(ReadNextStat(bytes, ref i));

                var r = new Dictionary<int, long>();
                for (int k = 0; k < flagged.Count && k < values.Count; k++)
                    r[flagged[k]] = values[k];

                for (int idx = 0; idx < statsCount; idx++) 
                    r.TryAdd(idx, 0);

                return r                   
                   .Where(kv => kv.Value != 0)
                   .ToDictionary(kv => kv.Key, kv => kv.Value);

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PCStatsParser] {ex.Message}");
                return new();
            }
        }


        private static long ReadNextStat(ReadOnlySpan<byte> buf, ref int i)
        {
            byte first = buf[i++];
            if (first < 0x40) return first;                // 0‑63 literal

            if (first >= 0x80 && first <= 0xBF)
            {
                long result = first & 0x3F;                // lower 6 bits
                int shift = 6;

                while (i < buf.Length)
                {
                    byte b = buf[i++];
                    result |= (long)(b & 0x7F) << shift;   // <<6, <<13, <<20…
                    if ((b & 0x80) == 0) return result;
                    shift += 7;
                }
            }

            Debug.WriteLine($"[PCStatsParser] Bad first byte 0x{first:X2}");
            return first;
        }



        public static async Task<List<FameBonus>> EvaluateBonuses(Character character)
        {
            var achievedBonuses = new List<FameBonus>();
            var parsedStats = character.ParsedPCStats;
            var allBonuses = await AssetService.GetAllFameBonuses();
            var statNameToIdMap = await AssetService.GetPCStatNameToIdMap();

            var groupOrder = new List<string> { "Stats Bonuses", "Enemy Bonuses", "Dungeon Bonuses" };

            var orderedBonuses = allBonuses.OrderBy(b => {
                var index = groupOrder.IndexOf(b.DisplayGroup);
                return index == -1 ? int.MaxValue : index;
            }).ThenBy(b => b.DisplayGroup).ThenBy(b => b.DisplayCategory).ThenBy(b => b.code);

            foreach (var bonus in orderedBonuses)
            {
                if (bonus.Condition == null || bonus.Condition.Length == 0) continue;

                bool allConditionsMet = true;
                int repeatCount = 1;

                foreach (var condition in bonus.Condition)
                {
                    bool conditionMet;
                    if (bonus.Repeatable)
                    {
                        if (statNameToIdMap.TryGetValue(condition.stat, out int statId) &&
                            parsedStats.TryGetValue(statId, out long statValue) &&
                            condition.threshold > 0)
                        {
                            int currentRepeats = (int)(statValue / condition.threshold);
                            if (currentRepeats > 0)
                            {
                                repeatCount = (repeatCount == 1) ? currentRepeats : Math.Min(repeatCount, currentRepeats);
                                conditionMet = true;
                            }
                            else
                            {
                                conditionMet = false;
                            }
                        }
                        else
                        {
                            conditionMet = false;
                        }
                    }
                    else
                    {
                        conditionMet = condition.Value switch
                        {
                            "FirstCharacter" => IsFirstCharacter(character),
                            "MaxedStat" => await AssetService.IsStatMaxed(character, condition.stat),
                            "StatValue" => CheckStatValue(condition, parsedStats, statNameToIdMap),
                            _ => false
                        };
                    }


                    if (!conditionMet)
                    {
                        allConditionsMet = false;
                        break;
                    }
                }

                if (allConditionsMet)
                {
                    if (bonus.Repeatable)
                    {
                        int maxRepeats = bonus.MaxRepeatCount > 0 ? bonus.MaxRepeatCount : int.MaxValue;
                        int actualRepeats = Math.Min(repeatCount, maxRepeats);
                        for (int i = 0; i < actualRepeats; i++)
                        {
                            achievedBonuses.Add(bonus);
                        }
                    }
                    else
                    {
                        achievedBonuses.Add(bonus);
                    }
                }
            }
            return achievedBonuses;
        }

        public static int CalculateTotalFame(int baseFame, List<FameBonus> bonuses)
        {
            double totalBonusFame = 0;

            foreach (var bonus in bonuses)
            {
                double relativeIncrement = Math.Ceiling(baseFame * (bonus.RelativeBonus / 100f));
                totalBonusFame += relativeIncrement + bonus.AbsoluteBonus;
            }

            return baseFame + (int)totalBonusFame;
        }

        private static bool CheckStatValue(RotMGAssetExtractor.ModelHelpers.Condition condition, Dictionary<int, long> parsedStats, Dictionary<string, int> statNameToIdMap)
        {
            if (!statNameToIdMap.TryGetValue(condition.stat, out int statId) || !parsedStats.TryGetValue(statId, out long statValue))
            {
                return false;
            }

            return condition.Value switch
            {
                "gte" => statValue >= condition.threshold,
                "lte" => statValue <= condition.threshold,
                "eq" => statValue == condition.threshold,
                "StatValue" => statValue >= condition.threshold,
                _ => false,
            };
        }

        private static bool IsFirstCharacter(Character character)
        {
            return character.Id == 0;
        }
    }
}
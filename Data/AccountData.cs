using System.Collections.Generic;

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
        public List<string> Gifts { get; set; } = new();
        public List<string> TemporaryGifts { get; set; } = new();
    }

    public class VaultData
    {
        public List<ChestData> Chests { get; set; } = new();
    }

    public class ChestData
    {
        public List<string> Items { get; set; } = new();
    }
}
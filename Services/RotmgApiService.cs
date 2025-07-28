using MDTadusMod.Data;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;
using System.Linq;

namespace MDTadusMod.Services
{
    public class RotmgApiService
    {
        private readonly HttpClient _httpClient;
        private const string ClientToken = "0";

        public RotmgApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<AccountData> GetAccountDataAsync(Account account)
        {
            var accountData = new AccountData { AccountId = account.Id };
            var accessToken = await VerifyAccountAndGetToken(account, accountData);

            if (accountData.PasswordError)
            {
                // If there's a password error, we can't proceed.
                // Return the data we have so the UI can be updated.
                return accountData;
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                // If there's no access token and no specific error, treat it as a general failure.
                throw new Exception("Failed to get access token for an unknown reason.");
            }

            var charListResponse = await _httpClient.PostAsync("https://www.realmofthemadgod.com/char/list", new FormUrlEncodedContent(new []
            {
                new KeyValuePair<string, string>("accessToken", accessToken),
                new KeyValuePair<string, string>("muleDump", "true")
            }));

            if (charListResponse.IsSuccessStatusCode)
            {
                var content = await charListResponse.Content.ReadAsStringAsync();
                ParseCharListXml(content, accountData);
            }
            else
            {
                throw new Exception($"Failed to fetch character list: {charListResponse.ReasonPhrase}");
            }
    
            return accountData;
        }

        private void ParseCharListXml(string xml, AccountData accountData)
        {
            var xDoc = XDocument.Parse(xml);
            var charsElement = xDoc.Root;
            var accountElement = charsElement.Element("Account");

            // --- Account Info & Account-Level Enchantment Data ---
            if (accountElement != null)
            {
                accountData.Name = accountElement.Element("Name")?.Value;
                accountData.Credits = (int?)accountElement.Element("Credits") ?? 0;
                accountData.Fame = (int?)accountElement.Element("Fame") ?? 0;
                accountData.MaxNumChars = (int?)charsElement.Attribute("maxNumChars") ?? 0;
                var guildElement = accountElement.Element("Guild");
                if (guildElement != null)
                {
                    accountData.GuildName = guildElement.Element("Name")?.Value;
                    accountData.GuildRank = (int?)guildElement.Element("Rank") ?? 0;
                }

                // Use the correct parser for account-level enchantments (which use the 'id' attribute)
                accountData.UniqueItemData = ParseAccountEnchantmentMap(accountElement.Element("UniqueItemInfo"));
                accountData.UniqueGiftItemData = ParseAccountEnchantmentMap(accountElement.Element("UniqueGiftItemInfo"));
                accountData.UniqueTemporaryGiftItemData = ParseAccountEnchantmentMap(accountElement.Element("UniqueTemporaryGiftItemInfo"));
                accountData.MaterialStorageItemData = ParseAccountEnchantmentMap(accountElement.Element("MaterialStorageData"));
            }

            // --- Characters ---
            accountData.Characters = charsElement.Elements("Char")
                .Select(c =>
                {
                    var character = new Character
                    {
                        Id = (int)c.Attribute("id"),
                        ObjectType = (int?)c.Element("ObjectType") ?? 0,
                        Skin = (int?)c.Element("Texture") ?? 0,
                        Level = (int?)c.Element("Level") ?? 0,
                        Exp = (int?)c.Element("Exp") ?? 0,
                        CurrentFame = (int?)c.Element("CurrentFame") ?? 0,
                        EquipQS = c.Element("EquipQS")?.Value,
                        MaxHitPoints = (int?)c.Element("MaxHitPoints") ?? 0,
                        MaxMagicPoints = (int?)c.Element("MaxMagicPoints") ?? 0,
                        Attack = (int?)c.Element("Attack") ?? 0,
                        Defense = (int?)c.Element("Defense") ?? 0,
                        Speed = (int?)c.Element("Speed") ?? 0,
                        Dexterity = (int?)c.Element("Dexterity") ?? 0,
                        Vitality = (int?)c.Element("HpRegen") ?? 0,
                        Wisdom = (int?)c.Element("MpRegen") ?? 0,
                        PCStats = c.Element("PCStats")?.Value,
                        Seasonal = c.Element("Seasonal") != null,
                        HasBackpack = c.Element("HasBackpack")?.Value == "1",
                        // Use the character-specific parser (which uses the 'type' attribute)
                        UniqueItemData = ParseCharacterEnchantmentMap(c.Element("UniqueItemInfo"))
                    };

                    var equipmentString = c.Element("Equipment")?.Value;
                    if (!string.IsNullOrEmpty(equipmentString))
                    {
                        var itemStrings = equipmentString.Split(',').Where(s => !string.IsNullOrWhiteSpace(s));
                        foreach (var itemStr in itemStrings)
                        {
                            var itemId = int.Parse(itemStr.Split('#')[0]);
                            character.UniqueItemData.TryGetValue(itemId.ToString(), out var enchantList);
                            character.EquipmentList.Add(new Item(itemId, enchantList?.FirstOrDefault()));
                        }
                    }

                    character.ParsePCStats();
                    return character;
                }).ToList();

            // --- Account-Level Containers (from within the <Account> element) ---
            if (accountElement != null)
            {
                // --- Vault ---
                var vaultElement = accountElement.Element("Vault");
                if (vaultElement != null)
                {
                    var allVaultItems = vaultElement.Elements("Chest")
                        .SelectMany(chest => chest.Value.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)));
                    
                    foreach (var itemStr in allVaultItems)
                    {
                        var parts = itemStr.Split('#');
                        var itemId = int.Parse(parts[0]);
                        string enchantData = null;
                        if (parts.Length > 1)
                        {
                            accountData.UniqueItemData.TryGetValue(parts[1], out enchantData);
                        }
                        accountData.Vault.Items.Add(new Item(itemId, enchantData));
                    }
                }

                // --- Material Storage ---
                var materialStorageElement = accountElement.Element("MaterialStorage");
                if (materialStorageElement != null)
                {
                    var allMaterialItems = materialStorageElement.Elements("Chest")
                        .SelectMany(chest => chest.Value.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)));

                    foreach (var itemStr in allMaterialItems)
                    {
                        var parts = itemStr.Split('#');
                        var itemId = int.Parse(parts[0]);
                        string enchantData = null;
                        if (parts.Length > 1)
                        {
                            accountData.MaterialStorageItemData.TryGetValue(parts[1], out enchantData);
                        }
                        accountData.MaterialStorage.Items.Add(new Item(itemId, enchantData));
                    }
                }

                // --- Gifts ---
                var giftsElement = accountElement.Element("Gifts");
                if (giftsElement != null)
                {
                    var itemStrings = giftsElement.Value.Split(',').Where(s => !string.IsNullOrWhiteSpace(s));
                    foreach (var itemStr in itemStrings)
                    {
                        var parts = itemStr.Split('#');
                        var itemId = int.Parse(parts[0]);
                        string enchantData = null;
                        if (parts.Length > 1)
                        {
                            accountData.UniqueGiftItemData.TryGetValue(parts[1], out enchantData);
                        }
                        accountData.Gifts.Add(new Item(itemId, enchantData));
                    }
                }

                // --- Temporary Gifts ---
                var tempGiftsElement = accountElement.Element("TemporaryGifts");
                if (tempGiftsElement != null)
                {
                    var itemStrings = tempGiftsElement.Value.Split(',').Where(s => !string.IsNullOrWhiteSpace(s));
                    foreach (var itemStr in itemStrings)
                    {
                        var parts = itemStr.Split('#');
                        var itemId = int.Parse(parts[0]);
                        string enchantData = null;
                        if (parts.Length > 1)
                        {
                            accountData.UniqueTemporaryGiftItemData.TryGetValue(parts[1], out enchantData);
                        }
                        accountData.TemporaryGifts.Add(new Item(itemId, enchantData));
                    }
                }

                // --- Potions (no enchantments) ---
                var potionsElement = accountElement.Element("Potions");
                if (potionsElement != null)
                {
                    var itemIds = potionsElement.Value.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).Select(int.Parse);
                    foreach (var itemId in itemIds)
                    {
                        // Potions don't have enchantments, so the second parameter is null.
                        accountData.Potions.Add(new Item(itemId, null));
                    }
                }
            }
        }

        // New parser for Account-level enchantments (keyed by instance ID)
        private Dictionary<string, string> ParseAccountEnchantmentMap(XElement uniqueItemInfoElement)
        {
            if (uniqueItemInfoElement == null) return new Dictionary<string, string>();
            
            return uniqueItemInfoElement.Elements("ItemData")
                .Where(el => el.Attribute("id") != null)
                .ToDictionary(
                    item => (string)item.Attribute("id"),
                    item => item.Value
                );
        }

        // Renamed original parser for Character-level enchantments (keyed by item type)
        private Dictionary<string, List<string>> ParseCharacterEnchantmentMap(XElement uniqueItemInfoElement)
        {
            var uniqueItemData = new Dictionary<string, List<string>>();
            if (uniqueItemInfoElement == null)
            {
                return uniqueItemData;
            }

            foreach (var itemElement in uniqueItemInfoElement.Elements("ItemData"))
            {
                var key = (string)itemElement.Attribute("type");
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!uniqueItemData.ContainsKey(key))
                {
                    uniqueItemData[key] = new List<string>();
                }
                
                uniqueItemData[key].Add(itemElement.Value);
            }
            return uniqueItemData;
        }

        private async Task<string> VerifyAccountAndGetToken(Account account, AccountData accountData)
        {
            var uriBuilder = new UriBuilder("https://www.realmofthemadgod.com/account/verify");
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["clientToken"] = ClientToken;
            query["game_net"] = "Unity";
            query["play_platform"] = "Unity";
            uriBuilder.Query = query.ToString();

            HttpContent content;

            if (account.Email.Contains(':') && !account.Email.Contains('@'))
            {
                content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("guid", account.Email),
                    new KeyValuePair<string, string>("secret", account.Password)
                });
            }
            else
            {
                content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("guid", account.Email),
                    new KeyValuePair<string, string>("password", account.Password)
                });
            }

            var response = await _httpClient.PostAsync(uriBuilder.ToString(), content);
            var responseString = await response.Content.ReadAsStringAsync();

            try
            {
                var xml = XDocument.Parse(responseString);
                if (xml.Root?.Name == "Error" && xml.Root.Value == "WebChangePasswordDialog.passwordError")
                {
                    accountData.PasswordError = true;
                    return null;
                }

                if (response.IsSuccessStatusCode)
                {
                    return xml.Root?.Element("AccessToken")?.Value;
                }
            }
            catch (System.Xml.XmlException)
            {
                // Response was not valid XML, proceed to throw general exception
            }

            // If we've reached here, the request failed for a non-specific reason.
            if (!response.IsSuccessStatusCode)
            {
                 throw new Exception($"Verification failed: {response.ReasonPhrase} - {responseString}");
            }

            return null; // Should not be reached, but covers all paths
        }

        private async Task<string> FetchApiData(string url, string accessToken, bool useMuledump = false)
        {
            var parameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("accessToken", accessToken)
            };

            if (useMuledump)
            {
                parameters.Add(new KeyValuePair<string, string>("muleDump", "true"));
            }

            var content = new FormUrlEncodedContent(parameters);

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
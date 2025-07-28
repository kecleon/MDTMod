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

                // Parse all enchantment containers from WITHIN the <Account> element.
                accountData.UniqueItemData = ParseUniqueItemData(accountElement.Element("UniqueItemInfo"));
                accountData.UniqueGiftItemData = ParseUniqueItemData(accountElement.Element("UniqueGiftItemInfo"));
                accountData.UniqueTemporaryGiftItemData = ParseUniqueItemData(accountElement.Element("UniqueTemporaryGiftItemInfo"));
                accountData.MaterialStorageItemData = ParseUniqueItemData(accountElement.Element("MaterialStorageData"));
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
                        // This parses the UniqueItemInfo specific to THIS character
                        UniqueItemData = ParseUniqueItemData(c.Element("UniqueItemInfo"))
                    };

                    var equipmentString = c.Element("Equipment")?.Value;
                    if (!string.IsNullOrEmpty(equipmentString))
                    {
                        var itemStrings = equipmentString.Split(',').Where(s => !string.IsNullOrWhiteSpace(s));
                        foreach (var itemStr in itemStrings)
                        {
                            // Handle potential "id#ref" format for equipment as well
                            var itemId = int.Parse(itemStr.Split('#')[0]);
                            // Use the CHARACTER's UniqueItemData for its own equipment
                            character.UniqueItemData.TryGetValue(itemStr, out var enchantList);
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
                    foreach (var chestElement in vaultElement.Elements("Chest"))
                    {
                        var chestData = new ChestData();
                        var itemStrings = chestElement.Value.Split(',').Where(s => !string.IsNullOrWhiteSpace(s));
                        foreach (var itemStr in itemStrings)
                        {
                            var itemId = int.Parse(itemStr.Split('#')[0]);
                            accountData.UniqueItemData.TryGetValue(itemStr, out var enchantList);
                            chestData.Items.Add(new Item(itemId, enchantList?.FirstOrDefault()));
                        }
                        accountData.Vault.Chests.Add(chestData);
                    }
                }

                // --- Material Storage ---
                var materialStorageElement = accountElement.Element("MaterialStorage");
                if (materialStorageElement != null)
                {
                    foreach (var chestElement in materialStorageElement.Elements("Chest"))
                    {
                        var chestData = new ChestData();
                        var itemStrings = chestElement.Value.Split(',').Where(s => !string.IsNullOrWhiteSpace(s));
                        foreach (var itemStr in itemStrings)
                        {
                            var itemId = int.Parse(itemStr.Split('#')[0]);
                            accountData.MaterialStorageItemData.TryGetValue(itemStr, out var enchantList);
                            chestData.Items.Add(new Item(itemId, enchantList?.FirstOrDefault()));
                        }
                        accountData.MaterialStorage.Chests.Add(chestData);
                    }
                }

                // --- Gifts ---
                var giftsElement = accountElement.Element("Gifts");
                if (giftsElement != null)
                {
                    var itemStrings = giftsElement.Value.Split(',').Where(s => !string.IsNullOrWhiteSpace(s));
                    foreach (var itemStr in itemStrings)
                    {
                        var itemId = int.Parse(itemStr.Split('#')[0]);
                        accountData.UniqueGiftItemData.TryGetValue(itemStr, out var enchantList);
                        accountData.Gifts.Add(new Item(itemId, enchantList?.FirstOrDefault()));
                    }
                }

                // --- Temporary Gifts ---
                var tempGiftsElement = accountElement.Element("TemporaryGifts");
                if (tempGiftsElement != null)
                {
                    var itemStrings = tempGiftsElement.Value.Split(',').Where(s => !string.IsNullOrWhiteSpace(s));
                    foreach (var itemStr in itemStrings)
                    {
                        var itemId = int.Parse(itemStr.Split('#')[0]);
                        accountData.UniqueTemporaryGiftItemData.TryGetValue(itemStr, out var enchantList);
                        accountData.TemporaryGifts.Add(new Item(itemId, enchantList?.FirstOrDefault()));
                    }
                }

                // --- Potions (no enchantments) ---
                var potionsElement = accountElement.Element("Potions");
                if (potionsElement != null)
                {
                    accountData.Potions = potionsElement.Value.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                }
            }
        }

        private Dictionary<string, List<string>> ParseUniqueItemData(XElement uniqueItemInfoElement)
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
                    continue; // Skip if key is invalid
                }

                // If the key is not in the dictionary, add it with a new list
                if (!uniqueItemData.ContainsKey(key))
                {
                    uniqueItemData[key] = new List<string>();
                }
                
                // Add the item's value to the list for that key
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
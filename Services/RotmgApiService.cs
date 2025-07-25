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
            Debug.WriteLine($"[XML Raw] {xml}");

            var charsElement = xDoc.Root;

            accountData.MaxNumChars = (int?)charsElement.Attribute("maxNumChars") ?? 0;

            var accountElement = charsElement.Element("Account");
            if (accountElement != null)
            {
                accountData.Name = accountElement.Element("Name")?.Value;
                accountData.Credits = (int?)accountElement.Element("Credits") ?? 0;
                accountData.Fame = (int?)accountElement.Element("Fame") ?? 0;
                accountData.Star = (int?)accountElement.Element("Star") ?? 0; // star is not a thing needs to be calculated
                var guildElement = accountElement.Element("Guild");
                if (guildElement != null)
                {
                    accountData.GuildName = guildElement.Element("Name")?.Value;
                    accountData.GuildRank = (int?)guildElement.Element("Rank") ?? 0;
                }
            }

            accountData.Characters = charsElement.Elements("Char")
                .Select(c =>
                {
                    var character = new Character
                    {
                        Id = (int)c.Attribute("id"),
                        ObjectType = (int)c.Element("ObjectType"),
                        Skin = (int?)c.Element("Texture") ?? 0,
                        Level = (int?)c.Element("Level") ?? 0,
                        Exp = (int?)c.Element("Exp") ?? 0,
                        CurrentFame = (int?)c.Element("CurrentFame") ?? 0,
                        Equipment = c.Element("Equipment")?.Value,
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
                        UniqueItemData = ParseUniqueItemData(c.Element("UniqueItemInfo"))
                    };
                    character.ParsePCStats();
                    return character;
                }).ToList();

            // Parse Vault
            var vaultElement = charsElement.Descendants("Vault").FirstOrDefault();
            if (vaultElement != null)
            {
                var vaultData = new VaultData();
                foreach (var chestElement in vaultElement.Elements("Chest"))
                {
                    var chestData = new ChestData();
                    var items = chestElement.Value.Split(',')
                        .Select(item => item.Trim())
                        .ToList();
                    chestData.Items.AddRange(items);
                    vaultData.Chests.Add(chestData);
                }
                accountData.Vault = vaultData;
            }
            else
            {
                Debug.WriteLine("[Vault Parsing] ERROR didnt find wtf.");

            }

            // Parse Potions
            var potionsElement = charsElement.Descendants("Potions").FirstOrDefault();
            if (potionsElement != null)
            {
                accountData.Potions = potionsElement.Value.Split(',')
                    .Select(item => item.Trim())
                    .ToList();
            }
            else
            {
                Debug.WriteLine("[Potions Parsing] No <Potions> element found.");
            }

            // Parse MaterialStorage
            var materialStorageElement = charsElement.Descendants("MaterialStorage").FirstOrDefault();
            if (materialStorageElement != null)
            {
                var materialStorageData = new VaultData();
                foreach (var chestElement in materialStorageElement.Elements("Chest"))
                {
                    var chestData = new ChestData();
                    var items = chestElement.Value.Split(',')
                        .Select(item => item.Trim())
                        .ToList();
                    chestData.Items.AddRange(items);
                    materialStorageData.Chests.Add(chestData);
                }
                accountData.MaterialStorage = materialStorageData;
            }
            else
            {
                Debug.WriteLine("[MaterialStorage Parsing] No <MaterialStorage> element found.");
            }

            // Parse Gifts
            var giftsElement = charsElement.Descendants("Gifts").FirstOrDefault();
            if (giftsElement != null)
            {
                accountData.Gifts = giftsElement.Value.Split(',')
                    .Select(item => item.Trim())
                    .ToList();
            }
            else
            {
                Debug.WriteLine("[Gifts Parsing] No <Gifts> element found.");
            }

            // Parse TemporaryGifts
            var tempGiftsElement = charsElement.Descendants("TemporaryGifts").FirstOrDefault();
            if (tempGiftsElement != null)
            {
                accountData.TemporaryGifts = tempGiftsElement.Value.Split(',')
                    .Select(item => item.Trim())
                    .ToList();
            }
            else
            {
                Debug.WriteLine("[TemporaryGifts Parsing] No <TemporaryGifts> element found.");
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
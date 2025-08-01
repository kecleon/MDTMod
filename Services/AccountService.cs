using MDTadusMod.Data;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MDTadusMod.Services
{
    public class AccountService
    {
        private readonly string _basePath;
        private readonly string _accountsFilePath;
        private readonly string _accountDataPath;

        public AccountService()
        {
            _basePath = FileSystem.AppDataDirectory;
            _accountsFilePath = Path.Combine(_basePath, "accounts.xml"); // Changed to .xml
            _accountDataPath = Path.Combine(_basePath, "AccountData");

            // Ensure the directory for individual account data exists
            if (!Directory.Exists(_accountDataPath))
            {
                Directory.CreateDirectory(_accountDataPath);
            }
        }

        public async Task<List<Account>> GetAccountsAsync()
        {
            if (!File.Exists(_accountsFilePath))
            {
                return new List<Account>();
            }

            var serializer = new XmlSerializer(typeof(List<Account>));
            var xmlContent = await File.ReadAllTextAsync(_accountsFilePath);

            using (var stringReader = new StringReader(xmlContent))
            {
                return (List<Account>)serializer.Deserialize(stringReader);
            }
        }

        public async Task SaveAccountsAsync(List<Account> accounts)
        {
            var serializer = new XmlSerializer(typeof(List<Account>));

            using (var stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, accounts);
                var xmlContent = stringWriter.ToString();
                await File.WriteAllTextAsync(_accountsFilePath, xmlContent);
            }
        }

        public async Task<AccountData> GetAccountDataAsync(Guid accountId)
        {
            var filePath = Path.Combine(_accountDataPath, $"{accountId}.xml");
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(AccountData));
                var xmlContent = await File.ReadAllTextAsync(filePath);

                using (var stringReader = new StringReader(xmlContent))
                {
                    var data = (AccountData)serializer.Deserialize(stringReader);
                    // Rehydrate equipment for all characters
                    if (data?.Characters != null)
                        foreach (var c in data.Characters)
                            c.RehydrateEquipment();
                    return data;
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[AccountService] Failed to deserialize account data for {accountId}. Deleting cached file. Error: {ex.Message}");
                File.Delete(filePath);
                return null;
            }
        }

        public async Task SaveAccountDataAsync(AccountData data)
        {
            var filePath = Path.Combine(_accountDataPath, $"{data.AccountId}.xml");
            var serializer = new XmlSerializer(typeof(AccountData));

            using (var stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, data);
                var xmlContent = stringWriter.ToString();
                await File.WriteAllTextAsync(filePath, xmlContent);
            }
        }
    }
}
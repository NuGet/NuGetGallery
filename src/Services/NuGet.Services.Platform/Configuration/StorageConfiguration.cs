using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage;

namespace NuGet.Services.Configuration
{
    public class StorageConfiguration : ICustomConfigurationSection
    {
        private Dictionary<KnownStorageAccount, CloudStorageAccount> _accounts;

        public CloudStorageAccount GetAccount(KnownStorageAccount account)
        {
            CloudStorageAccount connectionString;
            if (!_accounts.TryGetValue(account, out connectionString))
            {
                return null;
            }
            return connectionString;
        }

        public void Resolve(string prefix, ConfigurationHub hub)
        {
            _accounts = Enum.GetValues(typeof(KnownStorageAccount))
                .OfType<KnownStorageAccount>()
                .Select(a => new KeyValuePair<KnownStorageAccount, string>(a, hub.GetSetting(prefix + a.ToString())))
                .Where(p => !String.IsNullOrEmpty(p.Value))
                .ToDictionary(p => p.Key, p => CloudStorageAccount.Parse(p.Value));
        }
    }
}

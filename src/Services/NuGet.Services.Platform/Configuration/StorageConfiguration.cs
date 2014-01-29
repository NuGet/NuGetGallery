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
        public Dictionary<KnownStorageAccount, CloudStorageAccount> Accounts { get; private set; }

        public CloudStorageAccount Primary { get { return GetAccount(KnownStorageAccount.Primary); } }
        public CloudStorageAccount Legacy { get { return GetAccount(KnownStorageAccount.Legacy); } }
        public CloudStorageAccount Backup { get { return GetAccount(KnownStorageAccount.Backup); } }

        public CloudStorageAccount GetAccount(KnownStorageAccount account)
        {
            CloudStorageAccount connectionString;
            if (!Accounts.TryGetValue(account, out connectionString))
            {
                return null;
            }
            return connectionString;
        }

        public void Resolve(string prefix, ConfigurationHub hub)
        {
            Accounts = Enum.GetValues(typeof(KnownStorageAccount))
                .OfType<KnownStorageAccount>()
                .Select(a => new KeyValuePair<KnownStorageAccount, string>(a, hub.GetSetting(prefix + a.ToString())))
                .Where(p => !String.IsNullOrEmpty(p.Value))
                .ToDictionary(p => p.Key, p => CloudStorageAccount.Parse(p.Value));
        }
    }
}

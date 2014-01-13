using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Configuration;

namespace NuGet.Services.Storage
{
    public class StorageHub
    {
        private static Dictionary<string, Func<StorageHub, StorageAccountHub>> _knownAccounts = new Dictionary<string, Func<StorageHub, StorageAccountHub>>(StringComparer.OrdinalIgnoreCase) {
            {"primary", s => s.Primary},
            {"backup", s => s.Backup},
            {"legacy", s => s.Legacy}
        };

        public StorageAccountHub Primary { get; private set; }
        public StorageAccountHub Backup { get; private set; }
        public StorageAccountHub Legacy { get; private set; }

        protected StorageHub() { }

        public StorageHub(ConfigurationHub configuration)
            : this(
                primary: TryLoadAccount(configuration, KnownStorageAccount.Primary),
                backup: TryLoadAccount(configuration, KnownStorageAccount.Backup),
                legacy: TryLoadAccount(configuration, KnownStorageAccount.Legacy))
        {
        }

        public StorageHub(StorageAccountHub primary, StorageAccountHub backup, StorageAccountHub legacy)
            : this()
        {
            Primary = primary;
            Backup = backup;
            Legacy = legacy;
        }

        public StorageAccountHub GetAccount(AzureStorageReference reference)
        {
            Func<StorageHub, StorageAccountHub> handler;
            if (!_knownAccounts.TryGetValue(reference.AccountName, out handler))
            {
                return new StorageAccountHub(
                    new CloudStorageAccount(
                        new StorageCredentials(
                            reference.AccountName, 
                            reference.AccountKey),
                        useHttps: true));
            }
            return handler(this);
        }

        public CloudBlobContainer GetContainer(AzureStorageReference reference)
        {
            var account = GetAccount(reference);
            if (account == null)
            {
                return null;
            }
            return account.Blobs.Client.GetContainerReference(reference.Container);
        }

        private static StorageAccountHub TryLoadAccount(ConfigurationHub configuration, KnownStorageAccount account)
        {
            var connectionString = configuration.Storage.GetAccount(account);
            if (connectionString == null)
            {
                return null;
            }
            return new StorageAccountHub(connectionString);
        }
    }
}

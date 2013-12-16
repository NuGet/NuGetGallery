using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Configuration;

namespace NuGet.Services.Storage
{
    public class StorageHub
    {
        public StorageAccountHub Primary { get; private set; }
        public StorageAccountHub Backup { get; private set; }

        protected StorageHub() { }

        public StorageHub(ConfigurationHub configuration)
            : this(
                primary: TryLoadAccount(configuration, KnownStorageAccount.Primary),
                backup: TryLoadAccount(configuration, KnownStorageAccount.Backup))
        {
        }

        public StorageHub(StorageAccountHub primary, StorageAccountHub backup)
            : this()
        {
            Primary = primary;
            Backup = backup;
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace NuGet.Services.Storage
{
    public class StorageAccountHub
    {
        public virtual CloudStorageAccount Account { get; private set; }
        public virtual string ConnectionString { get; private set; }

        public virtual TableStorageHub Tables { get; private set; }
        public virtual BlobStorageHub Blobs { get; private set; }
        public virtual QueueStorageHub Queues { get; private set; }
        public virtual string Name { get { return Account.Credentials.AccountName; } }

        public StorageAccountHub(CloudStorageAccount account)
        {
            Account = account;

            if (account != null)
            {
                ConnectionString = account.ToString(exportSecrets: true);
                Tables = new TableStorageHub(account.CreateCloudTableClient());
                Blobs = new BlobStorageHub(account.CreateCloudBlobClient());
                Queues = new QueueStorageHub(account.CreateCloudQueueClient());
            }
        }
    }
}

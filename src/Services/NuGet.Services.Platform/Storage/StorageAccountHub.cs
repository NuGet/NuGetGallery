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
        public CloudStorageAccount Account { get; private set; }
        public string ConnectionString { get; private set; }

        public TableStorageHub Tables { get; private set; }
        public BlobStorageHub Blobs { get; private set; }
        public QueueStorageHub Queues { get; private set; }

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

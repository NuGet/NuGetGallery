using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.Queue;

namespace NuGet.Services.Storage
{
    public class QueueStorageHub
    {
        public CloudQueueClient Client { get; private set; }

        public QueueStorageHub(CloudQueueClient client)
        {
            Client = client;
        }

        public AzureQueue Queue(string name)
        {
            return new AzureQueue(Client.GetQueueReference(name));
        }
    }
}

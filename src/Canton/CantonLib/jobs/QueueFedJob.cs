using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public abstract class QueueFedJob : CantonJob
    {
        private readonly string _queueName;
        private CloudQueue _queue;

        public QueueFedJob(Config config, Storage storage, string queueName)
            : base(config, storage)
        {
            _queueName = queueName;
        }

        public CloudQueue Queue
        {
            get
            {
                if (_queue == null)
                {
                    var client = Account.CreateCloudQueueClient();
                    _queue = client.GetQueueReference(_queueName);
                }

                return _queue;
            }
        }
    }
}

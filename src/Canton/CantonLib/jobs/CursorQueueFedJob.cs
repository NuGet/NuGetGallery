using Microsoft.WindowsAzure.Storage.Queue;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class CursorQueueFedJob : CollectorJob
    {
        private readonly string _queueName;
        private CloudQueue _queue;

        public CursorQueueFedJob(Config config, Storage storage, string queueName, string cursorName)
            : base(config, storage, cursorName)
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

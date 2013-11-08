using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Queue.Protocol;

namespace NuGetGallery.Jobs
{
    public class JobRequestQueue
    {
        public static readonly string DefaultQueueName = "nugetjobrequests";

        private CloudQueue _queue;

        public JobRequestQueue(CloudQueue queue)
        {
            _queue = queue;
        }

        public static JobRequestQueue WithDefaultName(CloudStorageAccount account)
        {
            return WithDefaultName(account.CreateCloudQueueClient());
        }

        public static JobRequestQueue WithDefaultName(CloudQueueClient client)
        {
            return new JobRequestQueue(client.GetQueueReference(DefaultQueueName));
        }

        public async Task<JobRequest> Dequeue(TimeSpan invisibleFor, CancellationToken token)
        {
            var message = await _queue.SafeExecute(q => q.GetMessageAsync(
                invisibleFor,
                new QueueRequestOptions(),
                new OperationContext(),
                token));

            if (message == null)
            {
                return null;
            }
            else
            {
                return JobRequest.Parse(message.AsString, message);
            }
        }
    }
}

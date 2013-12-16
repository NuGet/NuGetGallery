using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

namespace NuGet.Services.Storage
{
    public class AzureQueue
    {
        private CloudQueue _queue;
        
        public AzureQueue(CloudQueue queue)
        {
            _queue = queue;
        }

        public virtual Task<CloudQueueMessage> Dequeue(TimeSpan invisibleFor, CancellationToken token)
        {
            return _queue.SafeExecute(q => q.GetMessageAsync(
                invisibleFor,
                new QueueRequestOptions(),
                new OperationContext(),
                token));
        }

        public virtual Task Enqueue(CloudQueueMessage message, TimeSpan? visibilityTimeout, string clientRequestId)
        {
            return _queue.SafeExecute(q => q.AddMessageAsync(
                message,
                timeToLive: null,
                initialVisibilityDelay: visibilityTimeout,
                options: new QueueRequestOptions(),
                operationContext: new OperationContext() { ClientRequestID = clientRequestId }));
        }

        public virtual Task Delete(CloudQueueMessage message)
        {
            return _queue.SafeExecute(q => q.DeleteMessageAsync(message));
        }

        public virtual Task Update(CloudQueueMessage message, TimeSpan visibilityTimeout, MessageUpdateFields updateFields)
        {
            return _queue.SafeExecute(q => q.UpdateMessageAsync(message, visibilityTimeout, updateFields));
        }
    }
}

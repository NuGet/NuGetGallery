// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

namespace NuGet.Services.Storage
{
    public class AzureStorageQueue : IStorageQueue
    {
        private Lazy<Task<CloudQueue>> _queueTask;

        /// <summary>
        /// After calling <see cref="GetNextAsync(CancellationToken)"/>, this is the duration of time that the message is invisible to other users for.
        /// </summary>
        private static readonly TimeSpan _visibilityTimeout = TimeSpan.FromMinutes(5);

        public AzureStorageQueue(CloudStorageAccount account, string queueName)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            _queueTask = new Lazy<Task<CloudQueue>>(async () =>
            {
                var queue = account.CreateCloudQueueClient().GetQueueReference(queueName);
                await queue.CreateIfNotExistsAsync();
                return queue;
            });
        }

        public async Task AddAsync(string contents, CancellationToken token)
        {
            var azureMessage = new AzureStorageQueueMessage(contents, dequeueCount: 0);
            await (await _queueTask.Value).AddMessageAsync(azureMessage.Message, token);
        }

        public async Task<StorageQueueMessage> GetNextAsync(CancellationToken token)
        {
            var nextMessage = await (await _queueTask.Value).GetMessageAsync(
                visibilityTimeout: _visibilityTimeout, 
                options: null, 
                operationContext: null, 
                cancellationToken: token);

            if (nextMessage == null)
            {
                return null;
            }

            return new AzureStorageQueueMessage(nextMessage);
        }

        public async Task RemoveAsync(StorageQueueMessage message, CancellationToken token)
        {
            if (!(message is AzureStorageQueueMessage))
            {
                throw new ArgumentException("This message was not returned from this queue!", nameof(message));
            }

            await (await _queueTask.Value).DeleteMessageAsync((message as AzureStorageQueueMessage).Message, token);
        }

        public async Task<int?> GetMessageCount(CancellationToken token)
        {
            var queue = await _queueTask.Value;
            await queue.FetchAttributesAsync(token);
            return queue.ApproximateMessageCount;
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace NuGet.Services.Storage
{
    public class AzureStorageQueue : IStorageQueue
    {
        private const string MESSAGE_NOT_FROM_THIS_QUEUE_EXCEPTION = "This message was not returned from this queue!";
        private Lazy<Task<QueueClient>> _queueTask;

        /// <summary>
        /// After calling <see cref="GetNextAsync(CancellationToken)"/>, this is the duration of time that the message is invisible to other users for.
        /// </summary>
        private static readonly TimeSpan _visibilityTimeout = TimeSpan.FromMinutes(5);

        public AzureStorageQueue(QueueServiceClient account, string queueName)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            _queueTask = new Lazy<Task<QueueClient>>(async () =>
            {
                var queue = account.GetQueueClient(queueName);
                await queue.CreateIfNotExistsAsync();
                return queue;
            });
        }

        public async Task AddAsync(string contents, CancellationToken token)
        {
            await (await _queueTask.Value).SendMessageAsync(contents, token);
        }

        public async Task<StorageQueueMessage> GetNextAsync(CancellationToken token)
        {
            var queueClient = await _queueTask.Value;
            QueueMessage nextMessage = null;

            QueueProperties queueProperties = await queueClient.GetPropertiesAsync();
            if(queueProperties.ApproximateMessagesCount > 0)
            {
                nextMessage = await queueClient.ReceiveMessageAsync(visibilityTimeout: _visibilityTimeout, cancellationToken: token);
            }

            if (nextMessage == null)
            {
                return null;
            }

            return new AzureStorageQueueMessage(nextMessage);
        }

        public async Task RemoveAsync(StorageQueueMessage message, CancellationToken token)
        {
            if (message is not AzureStorageQueueMessage queueMessage)
            {
                throw new ArgumentException(MESSAGE_NOT_FROM_THIS_QUEUE_EXCEPTION, nameof(message));
            }

            var queueClient = await _queueTask.Value;
            if (await queueClient.ExistsAsync())
            {
                await queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt, token);
            }

            return;
        }

        public async Task<int?> GetMessageCount(CancellationToken token)
        {
            var queueClient = await _queueTask.Value;
            QueueProperties queueProperties = await queueClient.GetPropertiesAsync(token);
            return queueProperties.ApproximateMessagesCount;
        }
    }
}
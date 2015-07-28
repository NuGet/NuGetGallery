// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Newtonsoft.Json;

namespace Stats.AzureCdnLogs.Common
{
    public class PackageStatisticsQueue
    {
        private const string _queueName = "packagestatisticsqueue";
        private readonly CloudQueue _queue;
        private readonly TimeSpan _visibilityTimeout = TimeSpan.FromMinutes(5);

        public PackageStatisticsQueue(CloudStorageAccount cloudStorageAccount)
        {
            var client = cloudStorageAccount.CreateCloudQueueClient();
            if (client == null)
            {
                throw new InvalidOperationException("Client is null.");
            }

            client.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromMilliseconds(500), 3);

            _queue = client.GetQueueReference(_queueName.ToLowerInvariant());
        }

        public async Task CreateIfNotExists()
        {
            await _queue.CreateIfNotExistsAsync();
        }

        public async Task AddMessageAsync(PackageStatisticsQueueMessage message)
        {
            var serializedMessage = JsonConvert.SerializeObject(message);
            var cloudQueueMessage = new CloudQueueMessage(serializedMessage);
            await _queue.AddMessageAsync(cloudQueueMessage);
        }

        public async Task<IReadOnlyCollection<PackageStatisticsQueueMessage>> GetMessagesAsync()
        {
            // max 4 as each message contains up to 250 table record identifiers
            // resulting in 1000 records, which is the maximum for batch operations on table storage

            var t1 = Task.FromResult(await GetMessageAsync());
            var t2 = Task.FromResult(await GetMessageAsync());
            var t3 = Task.FromResult(await GetMessageAsync());
            var t4 = Task.FromResult(await GetMessageAsync());

            await Task.WhenAll(t1, t2, t3, t4);

            var messages = new List<PackageStatisticsQueueMessage>();
            messages.Add(t1.Result);
            messages.Add(t2.Result);
            messages.Add(t3.Result);
            messages.Add(t4.Result);

            return messages;
        }

        public async Task<PackageStatisticsQueueMessage> GetMessageAsync()
        {
            var message = await _queue.GetMessageAsync(_visibilityTimeout, null, null);
            var result = JsonConvert.DeserializeObject<PackageStatisticsQueueMessage>(message.AsString);

            if (result != null)
            {
                result.Id = message.Id;
                result.PopReceipt = message.PopReceipt;
                result.DequeueCount = message.DequeueCount;
            }

            return result;
        }

        public async Task DeleteMessage(PackageStatisticsQueueMessage message)
        {
            await _queue.DeleteMessageAsync(message.Id, message.PopReceipt);
        }

        public async Task DeleteMessagesAsync(IEnumerable<PackageStatisticsQueueMessage> messages)
        {
            var tasks = new List<Task>();

            foreach (var message in messages)
            {
                var task = Task.Run(async () => await DeleteMessage(message));
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }
    }
}
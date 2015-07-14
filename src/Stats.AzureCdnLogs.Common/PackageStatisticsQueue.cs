// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
    }
}
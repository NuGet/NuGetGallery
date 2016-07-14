// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace NuGet.Jobs.Validation.Common
{
    public class PackageValidationQueue
    {
        private readonly ConcurrentDictionary<string, CloudQueue> _queues = new ConcurrentDictionary<string, CloudQueue>(); 
        private readonly string _containerNamePrefix;
        private readonly CloudQueueClient _cloudQueueClient;

        public PackageValidationQueue(CloudStorageAccount cloudStorageAccount, string containerNamePrefix)
        {
            _containerNamePrefix = containerNamePrefix;
            _cloudQueueClient = cloudStorageAccount.CreateCloudQueueClient();
        }

        private async Task<CloudQueue> GetQueueAsync(string validatorName)
        {
            var queueName = (_containerNamePrefix + validatorName).ToLowerInvariant();

            CloudQueue queue;
            if (!_queues.TryGetValue(queueName, out queue))
            {
                queue = _cloudQueueClient.GetQueueReference(queueName);
                await queue.CreateIfNotExistsAsync();
                _queues.TryAdd(queueName, queue);
            }
            return queue;
        }

        public async Task EnqueueAsync(string validatorName, PackageValidationMessage message)
        {
            message.Package = message.Package.TruncateForAzureQueue();

            Trace.TraceInformation("Start enqueue validation {0} {1} - package {2} {3}...", validatorName, message.ValidationId, message.PackageId, message.PackageVersion);

            var queue = await GetQueueAsync(validatorName);
            await queue.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(message)));

            Trace.TraceInformation("Finished enqueue validation {0} {1} - package {2} {3}.", validatorName, message.ValidationId, message.PackageId, message.PackageVersion);
        }

        public async Task<IReadOnlyCollection<PackageValidationMessage>> DequeueAsync(string validatorName, int messageCount, TimeSpan visibilityTimeout)
        {
            Trace.TraceInformation("Start dequeue validation {0} (maximum {1} items)...", validatorName, messageCount);

            var results = new List<PackageValidationMessage>();
            var queue = await GetQueueAsync(validatorName);
            var messages = await queue.GetMessagesAsync(messageCount, visibilityTimeout, null, null);
            foreach (var message in messages)
            {
                var deserializedMessage = JsonConvert.DeserializeObject<PackageValidationMessage>(message.AsString);
                deserializedMessage.MessageId = message.Id;
                deserializedMessage.InsertionTime = message.InsertionTime;
                deserializedMessage.PopReceipt = message.PopReceipt;
                deserializedMessage.DequeueCount = message.DequeueCount;

                results.Add(deserializedMessage);
            }

            Trace.TraceInformation("Finished dequeue validation {0} ({1} items).", validatorName, results.Count);

            return results;
        }

        public async Task DeleteAsync(string validatorName, PackageValidationMessage message)
        {
            Trace.TraceInformation("Start complete validation {0}...", validatorName);
            
            var queue = await GetQueueAsync(validatorName);
            await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt);

            Trace.TraceInformation("Finished complete validation {0}.", validatorName);
        }
    }
}
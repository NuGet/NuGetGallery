// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<PackageValidationQueue> _logger;

        public PackageValidationQueue(CloudStorageAccount cloudStorageAccount, string containerNamePrefix, ILoggerFactory loggerFactory)
        {
            _containerNamePrefix = containerNamePrefix;
            _cloudQueueClient = cloudStorageAccount.CreateCloudQueueClient();
            _logger = loggerFactory.CreateLogger<PackageValidationQueue>();
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

            _logger.LogInformation($"Start enqueue validation {{{TraceConstant.ValidatorName}}} " +
                    $"{{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}...", 
                validatorName, 
                message.ValidationId, 
                message.PackageId, 
                message.PackageVersion);

            var queue = await GetQueueAsync(validatorName);
            await queue.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(message)));

            _logger.LogInformation($"Finished enqueue validation {{{TraceConstant.ValidatorName}}} " +
                    $"{{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}.", 
                validatorName, 
                message.ValidationId, 
                message.PackageId, 
                message.PackageVersion);
        }

        public async Task<IReadOnlyCollection<PackageValidationMessage>> DequeueAsync(string validatorName, int messageCount, TimeSpan visibilityTimeout)
        {
            _logger.LogInformation($"Start dequeue validation {{{TraceConstant.ValidatorName}}} " +
                    $"(maximum {{{TraceConstant.MessageCount}}} items)...", 
                validatorName, 
                messageCount);

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

            _logger.LogInformation($"Finished dequeue validation {{{TraceConstant.ValidatorName}}} " +
                $"({{{TraceConstant.ResultCount}}} items).", validatorName, results.Count);

            return results;
        }

        public async Task DeleteAsync(string validatorName, PackageValidationMessage message)
        {
            _logger.LogInformation($"Start complete validation {{{TraceConstant.ValidatorName}}}...", validatorName);
            
            var queue = await GetQueueAsync(validatorName);
            await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt);

            _logger.LogInformation($"Finished complete validation {{{TraceConstant.ValidatorName}}}.", validatorName);
        }
    }
}
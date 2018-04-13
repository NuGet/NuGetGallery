// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using NuGet.Services.Storage;
using NuGet.Versioning;

namespace NuGet.Services.V3PerPackage
{
    public class PerWorkerProcessor
    {
        private readonly IStorageQueue<PackageMessage> _queue;
        private readonly PerBatchProcessor _perBatchProcessor;
        private readonly ILogger<PerWorkerProcessor> _logger;

        public PerWorkerProcessor(IStorageQueue<PackageMessage> queue, PerBatchProcessor perBatchProcessor, ILogger<PerWorkerProcessor> logger)
        {
            _queue = queue;
            _perBatchProcessor = perBatchProcessor;
            _logger = logger;
        }

        public async Task ProcessAsync(PerWorkerContext workerContext, int messageCount)
        {
            bool hasMoreMessages;
            var processed = 0;
            do
            {
                var currentBatchSize = Math.Min(workerContext.Process.BatchSize, messageCount - processed);
                hasMoreMessages = await ProcessNextMessageAsync(workerContext, currentBatchSize);
                processed += currentBatchSize;
            }
            while (hasMoreMessages && processed < messageCount);
        }

        private async Task<bool> ProcessNextMessageAsync(PerWorkerContext workerContext, int batchSize)
        {
            var batchContext = new PerBatchContext(workerContext, UniqueName.New("batch"));

            var packageIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var messages = new List<StorageQueueMessage<PackageMessage>>();
            var packageContexts = new List<PerPackageContext>();
            StorageQueueMessage<PackageMessage> lastMessage;
            int lastDequeueCount = 0;
            do
            {
                lastMessage = await _queue.GetNextAsync(CancellationToken.None);

                if (lastMessage != null)
                {
                    var packageId = lastMessage.Contents.PackageId.Trim();
                    var packageVersion = NuGetVersion.Parse(lastMessage.Contents.PackageVersion.Trim()).ToNormalizedString();
                    var packageIdentity = $"{packageId}/{packageVersion}";

                    // If this is a duplicate package, complete it and skip it.
                    if (!packageIdentities.Add(packageIdentity))
                    {
                        await _queue.RemoveAsync(lastMessage, CancellationToken.None);
                        continue;
                    }

                    lastDequeueCount = GetDequeueCount(lastMessage);
                    messages.Add(lastMessage);
                    packageContexts.Add(new PerPackageContext(batchContext, packageId, packageVersion));
                }
            }
            while (messages.Count < batchSize && lastMessage != null && lastDequeueCount <= 1);

            if (packageContexts.Count == 0)
            {
                return false;
            }

            var complete = await ProcessPackagesAsync(batchContext, packageContexts);
            if (complete)
            {
                foreach (var message in messages)
                {
                    try
                    {
                        await _queue.RemoveAsync(message, CancellationToken.None);
                    }
                    catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotFound)
                    {
                        // Ignore this error. The message has already been removed.
                    }
                }
            }

            return true;
        }

        private static int GetDequeueCount(StorageQueueMessage<PackageMessage> lastMessage)
        {
            // This is a hack since dequeue count is not available on the public API.
            var storageQueueMessage = lastMessage
                .GetType()
                .GetProperty("Message", BindingFlags.Instance | BindingFlags.NonPublic)?
                .GetValue(lastMessage);

            var cloudQueueMessage = storageQueueMessage?
                .GetType()
                .GetProperty("Message", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(storageQueueMessage) as CloudQueueMessage;

            return cloudQueueMessage?.DequeueCount ?? 1;
        }

        private async Task<bool> ProcessPackagesAsync(PerBatchContext batchContext, List<PerPackageContext> packageContexts)
        {
            var packagesForLogging = packageContexts
                .Select(x => new { Id = x.PackageId, Version = x.PackageVersion })
                .ToList();

            bool complete;
            try
            {
                complete = await _perBatchProcessor.ProcessAsync(batchContext, packageContexts);

                if (complete)
                {
                    foreach (var package in packageContexts)
                    {
                        _logger.LogInformation(
                            "Package {PackageId}/{PackageVersion} was completed by worker {WorkerName}, process {ProcessName}.",
                            package.PackageId,
                            package.PackageVersion,
                            batchContext.Worker.Name,
                            batchContext.Process.Name);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Packages {Packages} were not completed by worker {WorkerName}, process {ProcessName}.",
                        packagesForLogging,
                        batchContext.Worker.Name,
                        batchContext.Process.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    0,
                    ex,
                    "An exception was thrown while processing packages {Packages}, worker {WorkerName}, process {ProcessName}.",
                    packagesForLogging,
                    batchContext.Worker.Name,
                    batchContext.Process.Name);

                complete = false;
            }

            try
            {
                await _perBatchProcessor.CleanUpAsync(batchContext, packageContexts);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    0,
                    ex,
                    "An exception was thrown while cleaning up packages {Packages}, worker {WorkerName}, process {ProcessName}.",
                    packagesForLogging,
                    batchContext.Worker.Name,
                    batchContext.Process.Name);
            }

            return complete;
        }
    }
}

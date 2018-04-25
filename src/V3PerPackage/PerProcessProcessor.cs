// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.V3PerPackage
{
    public class PerProcessProcessor
    {
        private readonly PerWorkerProcessor _perWorkerProcessor;
        private readonly ILogger<PerProcessContext> _logger;

        public PerProcessProcessor(PerWorkerProcessor perWorkerProcessor, ILogger<PerProcessContext> logger)
        {
            _perWorkerProcessor = perWorkerProcessor;
            _logger = logger;
        }

        public async Task ProcessAsync(PerProcessContext context)
        {
            var evenCount = context.MessageCount / context.WorkerCount;
            var extraCount = evenCount + (context.MessageCount % context.WorkerCount);

            var tasks = Enumerable
                .Range(0, context.WorkerCount)
                .Select(x => ProcessPerWorkerAsync(context, x == 0 ? extraCount : evenCount))
                .ToList();

            await Task.WhenAll(tasks);
        }

        private async Task ProcessPerWorkerAsync(PerProcessContext processContext, int messageCount)
        {
            var context = new PerWorkerContext(processContext, UniqueName.New("worker"));

            await _perWorkerProcessor.ProcessAsync(context, messageCount);
        }

        public async Task CleanUpAsync(PerProcessContext context)
        {
            var blobClient = BlobStorageUtilities.GetBlobClient(context.Global);

            // Delete catalog2dnx artifacts.
            await CleanUpUtilities.DeleteBlobsWithPrefix(
                blobClient,
                context.Global.FlatContainerContainerName,
                context.FlatContainerStoragePath,
                _logger);
        }
    }
}

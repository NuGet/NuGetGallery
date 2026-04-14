// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UpdateBlobProperties
{
    public class Processor : IProcessor
    {
        private readonly ICollector _collector;
        private readonly IUpdater _updater;
        private readonly IOptionsSnapshot<UpdateBlobPropertiesConfiguration> _configuration;
        private readonly Cursor _cursor;
        private readonly ILogger<Processor> _logger;

        public Processor(ICollector collector,
            IUpdater updater,
            IOptionsSnapshot<UpdateBlobPropertiesConfiguration> configuration,
            Cursor cursor,
            ILogger<Processor> logger)
        {
            _collector = collector;
            _updater = updater;
            _configuration = configuration;
            _cursor = cursor;
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            await _cursor.Load(token);

            _logger.LogInformation("Loaded the cursor value. ProcessedMaxKey: {cursorValue}.", _cursor.Value);

            var minKey = _cursor.Value + 1;
            var maxKey = _configuration.Value.MaxKey;
            var maxDegreeOfParallelism = _configuration.Value.MaxDegreeOfParallelism;

            if (minKey > maxKey)
            {
                _logger.LogInformation("All keys have been processed.");

                return;
            }

            _logger.LogInformation("Processing pages with minKey: {minKey} and maxKey: {maxKey}. Max degree of parallelism is {maxDegreeOfParallelism}", minKey, maxKey, maxDegreeOfParallelism);

            await foreach (IList<PackageInfo> packageInfos in _collector.GetPagesOfPackageInfosAsync(minKey: minKey, maxKey: maxKey))
            {
                var packageInfosToProcess = new ConcurrentBag<PackageInfo>(packageInfos);

                var tasks = Enumerable
                    .Range(0, maxDegreeOfParallelism)
                    .Select(x => ProcessPackageInfosAsync(packageInfosToProcess, cts.Token))
                    .ToList();

                while (tasks.Count > 0)
                {
                    var completedTask = await Task.WhenAny(tasks);
                    if (completedTask.IsFaulted || completedTask.IsCanceled)
                    {
                        cts.Cancel();
                        await completedTask;
                    }

                    tasks.Remove(completedTask);
                }

                _cursor.Value = packageInfos.Last().Key;
                await _cursor.Save(token);

                _logger.LogInformation("Saved the cursor value. ProcessedMaxKey: {cursorValue}.", _cursor.Value);
            }
        }

        private async Task ProcessPackageInfosAsync(ConcurrentBag<PackageInfo> packageInfos, CancellationToken token)
        {
            while (packageInfos.TryTake(out var packageInfo) && !token.IsCancellationRequested)
            {
                await _updater.UpdateBlobPropertiesAsync(packageInfo, token);
            }
        }
    }
}

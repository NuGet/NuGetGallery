// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class Db2AzureSearchCommand
    {
        private readonly INewPackageRegistrationProducer _producer;
        private readonly IPackageEntityIndexActionBuilder _indexActionBuilder;
        private readonly IIndexBuilder _indexBuilder;
        private readonly Func<IBatchPusher> _batchPusherFactory;
        private readonly IOptionsSnapshot<Db2AzureSearchConfiguration> _options;
        private readonly ILogger<Db2AzureSearchCommand> _logger;

        public Db2AzureSearchCommand(
            INewPackageRegistrationProducer producer,
            IPackageEntityIndexActionBuilder indexActionBuilder,
            IIndexBuilder indexBuilder,
            Func<IBatchPusher> batchPusherFactory,
            IOptionsSnapshot<Db2AzureSearchConfiguration> options,
            ILogger<Db2AzureSearchCommand> logger)
        {
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
            _indexActionBuilder = indexActionBuilder ?? throw new ArgumentNullException(nameof(indexActionBuilder));
            _indexBuilder = indexBuilder ?? throw new ArgumentNullException(nameof(indexBuilder));
            _batchPusherFactory = batchPusherFactory ?? throw new ArgumentNullException(nameof(batchPusherFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (_options.Value.MaxConcurrentBatches <= 0)
            {
                throw new ArgumentException(
                    $"The {nameof(AzureSearchConfiguration.MaxConcurrentBatches)} must be greater than zero.",
                    nameof(options));
            }
        }

        public async Task ExecuteAsync()
        {
            var allWork = new ConcurrentBag<NewPackageRegistration>();
            using (var cancelledCts = new CancellationTokenSource())
            using (var produceWorkCts = new CancellationTokenSource())
            {
                // Initialize the indexes
                await InitializeAsync();

                // Set up the producer and the consumers.
                var producerTask = ProduceWorkAsync(allWork, produceWorkCts, cancelledCts.Token);
                var consumerTasks = Enumerable
                    .Range(0, _options.Value.MaxConcurrentBatches)
                    .Select(i => ConsumeWorkAsync(allWork, produceWorkCts.Token, cancelledCts.Token))
                    .ToList();
                var allTasks = new[] { producerTask }.Concat(consumerTasks).ToList();

                // If one of the tasks throws an exception before the work is completed, cancel the work.
                var firstTask = await Task.WhenAny(allTasks);
                if (firstTask.IsFaulted)
                {
                    cancelledCts.Cancel();
                }

                await firstTask;
                await Task.WhenAll(allTasks);
            }
        }

        private async Task InitializeAsync()
        {
            if (_options.Value.ReplaceIndexes)
            {
                await _indexBuilder.DeleteSearchIndexIfExistsAsync();
                await _indexBuilder.DeleteHijackIndexIfExistsAsync();
            }

            await _indexBuilder.CreateSearchIndexAsync();
            await _indexBuilder.CreateHijackIndexAsync();
        }

        private async Task ProduceWorkAsync(
            ConcurrentBag<NewPackageRegistration> allWork,
            CancellationTokenSource produceWorkCts,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            await _producer.ProduceWorkAsync(allWork, cancellationToken);
            produceWorkCts.Cancel();
        }

        private async Task ConsumeWorkAsync(
            ConcurrentBag<NewPackageRegistration> allWork,
            CancellationToken produceWorkToken,
            CancellationToken cancellationToken)
        {
            await Task.Yield();

            var batchPusher = _batchPusherFactory();

            NewPackageRegistration work = null;
            try
            {
                while ((allWork.TryTake(out work) || !produceWorkToken.IsCancellationRequested)
                    && !cancellationToken.IsCancellationRequested)
                {
                    // If there's no work to do, wait a bit before checking again.
                    if (work == null)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100));
                        continue;
                    }

                    var indexActions = _indexActionBuilder.AddNewPackageRegistration(work);

                    batchPusher.EnqueueIndexActions(work.PackageId, indexActions);
                    await batchPusher.PushFullBatchesAsync();
                }

                await batchPusher.FinishAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    0,
                    ex,
                    "An exception was thrown while processing package ID {PackageId}.",
                    work?.PackageId ?? "(last batch...)");
                throw;
            }
        }
    }
}

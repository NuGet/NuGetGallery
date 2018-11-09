// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.AzureSearch.Wrappers;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class Db2AzureSearchCommand
    {
        private readonly INewPackageRegistrationProducer _producer;
        private readonly IIndexActionBuilder _indexActionBuilder;
        private readonly ISearchServiceClientWrapper _serviceClient;
        private readonly ISearchIndexClientWrapper _searchIndexClient;
        private readonly ISearchIndexClientWrapper _hijackIndexClient;
        private readonly IVersionListDataClient _versionListDataClient;
        private readonly IOptionsSnapshot<Db2AzureSearchConfiguration> _options;
        private readonly ILogger<Db2AzureSearchCommand> _logger;

        public Db2AzureSearchCommand(
            INewPackageRegistrationProducer producer,
            IIndexActionBuilder indexActionBuilder,
            ISearchServiceClientWrapper serviceClient,
            ISearchIndexClientWrapper searchIndexClient,
            ISearchIndexClientWrapper hijackIndexClient,
            IVersionListDataClient versionListDataClient,
            IOptionsSnapshot<Db2AzureSearchConfiguration> options,
            ILogger<Db2AzureSearchCommand> logger)
        {
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
            _indexActionBuilder = indexActionBuilder ?? throw new ArgumentNullException(nameof(indexActionBuilder));
            _serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
            _searchIndexClient = searchIndexClient ?? throw new ArgumentNullException(nameof(searchIndexClient));
            _hijackIndexClient = hijackIndexClient ?? throw new ArgumentNullException(nameof(hijackIndexClient));
            _versionListDataClient = versionListDataClient ?? throw new ArgumentNullException(nameof(versionListDataClient));
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
                await DeleteIndexIfExistsAsync(_options.Value.SearchIndexName);
                await DeleteIndexIfExistsAsync(_options.Value.HijackIndexName);
            }

            await CreateIndexAsync<SearchDocument.Full>(_options.Value.SearchIndexName);
            await CreateIndexAsync<HijackDocument.Full>(_options.Value.HijackIndexName);
        }

        private async Task DeleteIndexIfExistsAsync(string indexName)
        {
            if (await _serviceClient.Indexes.ExistsAsync(indexName))
            {
                _logger.LogWarning("Deleting index {IndexName}.", indexName);
                await _serviceClient.Indexes.DeleteAsync(indexName);
                _logger.LogWarning("Done deleting index {IndexName}.", indexName);
            }
        }

        private async Task CreateIndexAsync<T>(string indexName)
        {
            _logger.LogInformation("Creating index {IndexName}.", indexName);
            await _serviceClient.Indexes.CreateAsync(new Index
            {
                Name = indexName,
                Fields = FieldBuilder.BuildForType<T>(),
            });
            _logger.LogInformation("Done creating index {IndexName}.", indexName);
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

            var searchActions = new Queue<IndexAction<KeyedDocument>>();
            var hijackActions = new Queue<IndexAction<KeyedDocument>>();

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

                    // Process the index changes
                    foreach (var action in indexActions.Search)
                    {
                        searchActions.Enqueue(action);
                    }

                    foreach (var action in indexActions.Hijack)
                    {
                        hijackActions.Enqueue(action);
                    }

                    var searchBatchesTask = PushFullBatchesAsync(_searchIndexClient, searchActions);
                    var hijackBatchesTask = PushFullBatchesAsync(_hijackIndexClient, hijackActions);
                    await Task.WhenAll(searchBatchesTask, hijackBatchesTask);

                    // Write the version list data
                    await _versionListDataClient.ReplaceAsync(
                        work.PackageId,
                        indexActions.VersionListDataResult.Result,
                        indexActions.VersionListDataResult.AccessCondition);
                }

                var searchFinishTask = IndexAsync(_searchIndexClient, searchActions);
                var hijackFinishTask = IndexAsync(_hijackIndexClient, hijackActions);
                await Task.WhenAll(searchFinishTask, hijackFinishTask);
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

        private async Task PushFullBatchesAsync(
            ISearchIndexClientWrapper indexClient,
            Queue<IndexAction<KeyedDocument>> actions)
        {
            while (actions.Count > _options.Value.AzureSearchBatchSize)
            {
                var batch = new List<IndexAction<KeyedDocument>>();
                while (batch.Count < _options.Value.AzureSearchBatchSize)
                {
                    batch.Add(actions.Dequeue());
                }

                await IndexAsync(indexClient, batch);
            }
        }

        private async Task IndexAsync(
            ISearchIndexClientWrapper indexClient,
            IReadOnlyCollection<IndexAction<KeyedDocument>> batch)
        {
            if (batch.Count == 0)
            {
                return;
            }

            await Task.Yield();

            _logger.LogInformation(
                "Pushing batch of {BatchSize} to index {IndexName}.",
                batch.Count,
                indexClient.IndexName);
            var batchResults = await indexClient.Documents.IndexAsync(new IndexBatch<KeyedDocument>(batch));
            const int errorsToLog = 5;
            var errorCount = 0;
            foreach (var result in batchResults.Results)
            {
                if (!result.Succeeded)
                {
                    if (errorCount < errorsToLog)
                    {
                        _logger.LogError(
                            "Indexing document with key {Key} failed. {StatusCode}: {ErrorMessage}",
                            result.Key,
                            result.StatusCode,
                            result.ErrorMessage);
                    }

                    errorCount++;
                }
            }

            if (errorCount > 0)
            {
                throw new InvalidOperationException($"{errorCount} errors were found when indexing a batch. Only {errorsToLog} were logged.");
            }
        }
    }
}

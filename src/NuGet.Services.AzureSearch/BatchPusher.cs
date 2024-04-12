// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using NuGet.Services.AzureSearch.Wrappers;

namespace NuGet.Services.AzureSearch
{
    public class BatchPusher : IBatchPusher
    {
        private readonly ISearchClientWrapper _searchIndex;
        private readonly ISearchClientWrapper _hijackIndex;
        private readonly IVersionListDataClient _versionListDataClient;
        private readonly IOptionsSnapshot<AzureSearchJobConfiguration> _options;
        private readonly IOptionsSnapshot<AzureSearchJobDevelopmentConfiguration> _developmentOptions;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<BatchPusher> _logger;
        internal readonly Dictionary<string, int> _idReferenceCount;
        internal readonly Queue<IdAndValue<IndexDocumentsAction<KeyedDocument>>> _searchActions;
        internal readonly Queue<IdAndValue<IndexDocumentsAction<KeyedDocument>>> _hijackActions;
        internal readonly Dictionary<string, ResultAndAccessCondition<VersionListData>> _versionListDataResults;

        public BatchPusher(
            ISearchClientWrapper searchIndexClient,
            ISearchClientWrapper hijackIndexClient,
            IVersionListDataClient versionListDataClient,
            IOptionsSnapshot<AzureSearchJobConfiguration> options,
            IOptionsSnapshot<AzureSearchJobDevelopmentConfiguration> developmentOptions,
            IAzureSearchTelemetryService telemetryService,
            ILogger<BatchPusher> logger)
        {
            _searchIndex = searchIndexClient ?? throw new ArgumentNullException(nameof(searchIndexClient));
            _hijackIndex = hijackIndexClient ?? throw new ArgumentNullException(nameof(hijackIndexClient));
            _versionListDataClient = versionListDataClient ?? throw new ArgumentNullException(nameof(versionListDataClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _developmentOptions = developmentOptions ?? throw new ArgumentNullException(nameof(developmentOptions));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _idReferenceCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            _searchActions = new Queue<IdAndValue<IndexDocumentsAction<KeyedDocument>>>();
            _hijackActions = new Queue<IdAndValue<IndexDocumentsAction<KeyedDocument>>>();
            _versionListDataResults = new Dictionary<string, ResultAndAccessCondition<VersionListData>>();

            if (_options.Value.MaxConcurrentVersionListWriters <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"The {nameof(AzureSearchJobConfiguration.MaxConcurrentVersionListWriters)} must be greater than zero.");
            }

            if (_options.Value.AzureSearchBatchSize <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"The {nameof(AzureSearchJobConfiguration.AzureSearchBatchSize)} must be greater than zero.");
            }
        }

        public void EnqueueIndexActions(string packageId, IndexActions indexActions)
        {
            if (_versionListDataResults.ContainsKey(packageId))
            {
                throw new ArgumentException("This package ID has already been enqueued.", nameof(packageId));
            }

            if (indexActions.IsEmpty)
            {
                throw new ArgumentException("There must be at least one index action.", nameof(indexActions));
            }

            foreach (var action in indexActions.Hijack)
            {
                EnqueueAndIncrement(_hijackActions, packageId, action);
            }

            foreach (var action in indexActions.Search)
            {
                EnqueueAndIncrement(_searchActions, packageId, action);
            }

            _versionListDataResults.Add(packageId, indexActions.VersionListDataResult);
        }

        public async Task<BatchPusherResult> TryPushFullBatchesAsync()
        {
            return await TryPushBatchesAsync(onlyFull: true);
        }

        public async Task<BatchPusherResult> TryFinishAsync()
        {
            return await TryPushBatchesAsync(onlyFull: false);
        }

        private async Task<BatchPusherResult> TryPushBatchesAsync(bool onlyFull)
        {
            var failedPackageIds = new List<string>();
            failedPackageIds.AddRange(await PushBatchesAsync(_hijackIndex, _hijackActions, onlyFull));
            failedPackageIds.AddRange(await PushBatchesAsync(_searchIndex, _searchActions, onlyFull));
            return new BatchPusherResult(failedPackageIds);
        }

        private async Task<List<string>> PushBatchesAsync(
            ISearchClientWrapper indexClient,
            Queue<IdAndValue<IndexDocumentsAction<KeyedDocument>>> actions,
            bool onlyFull)
        {
            var failedPackageIds = new List<string>();
            while ((onlyFull && actions.Count >= _options.Value.AzureSearchBatchSize)
                || (!onlyFull && actions.Count > 0))
            {
                var allFinished = new List<IdAndValue<ResultAndAccessCondition<VersionListData>>>();
                var batch = new List<IndexDocumentsAction<KeyedDocument>>();

                while (batch.Count < _options.Value.AzureSearchBatchSize && actions.Count > 0)
                {
                    var idAndValue = DequeueAndDecrement(actions, out int newCount);
                    batch.Add(idAndValue.Value);

                    if (newCount == 0)
                    {
                        allFinished.Add(NewIdAndValue(idAndValue.Id, _versionListDataResults[idAndValue.Id]));
                        Guard.Assert(_versionListDataResults.Remove(idAndValue.Id), "The version list data result should have existed.");
                    }
                }

                await IndexAsync(indexClient, batch);

                if (allFinished.Any())
                {
                    failedPackageIds.AddRange(await UpdateVersionListsAsync(allFinished));
                }
            }

            var extraVersionListData = _versionListDataResults.Keys.Except(_idReferenceCount.Keys);
            Guard.Assert(
                !extraVersionListData.Any(),
                $"There are some version list data results without reference counts, e.g. {string.Join(", ", extraVersionListData.Take(5))}.");
            var extraReferenceCounts = _idReferenceCount.Keys.Except(_versionListDataResults.Keys);
            Guard.Assert(
                !extraReferenceCounts.Any(),
                $"There are some reference counts without version list data results, e.g. {string.Join(", ", extraReferenceCounts.Take(5))}");

            return failedPackageIds;
        }

        private async Task IndexAsync(
            ISearchClientWrapper indexClient,
            IReadOnlyList<IndexDocumentsAction<KeyedDocument>> batch)
        {
            if (batch.Count == 0)
            {
                return;
            }

            if (batch.Count > _options.Value.AzureSearchBatchSize)
            {
                throw new ArgumentException("The provided batch is too large.");
            }

            _logger.LogInformation(
                "Pushing batch of {BatchSize} to index {IndexName}.",
                batch.Count,
                indexClient.IndexName);

            IReadOnlyList<IndexingResult> indexingResults = null;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var batchInput = new IndexDocumentsBatch<KeyedDocument>();
                batchInput.Actions.AddRange(batch);
                var batchResults = await indexClient.IndexAsync(batchInput);
                indexingResults = batchResults.Results;

                stopwatch.Stop();
                _telemetryService.TrackIndexPushSuccess(indexClient.IndexName, batch.Count, stopwatch.Elapsed);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.RequestEntityTooLarge && batch.Count > 1)
            {
                stopwatch.Stop();
                _telemetryService.TrackIndexPushSplit(indexClient.IndexName, batch.Count);

                var halfCount = batch.Count / 2;
                var halfA = batch.Take(halfCount).ToList();
                var halfB = batch.Skip(halfCount).ToList();

                _logger.LogWarning(
                    0,
                    ex,
                    "The request body for a batch of {BatchSize} was too large. Splitting into two batches of size " +
                    "{HalfA} and {HalfB}.",
                    batch.Count,
                    halfA.Count,
                    halfB.Count);

                await IndexAsync(indexClient, halfA);
                await IndexAsync(indexClient, halfB);
            }

            if (indexingResults != null)
            {
                const int errorsToLog = 5;
                var errorCount = 0;
                foreach (var result in indexingResults)
                {
                    if (!result.Succeeded)
                    {
                        if (errorCount < errorsToLog)
                        {
                            _logger.LogError(
                                "Indexing document with key {Key} failed for index {IndexName}. {StatusCode}: {ErrorMessage}",
                                result.Key,
                                indexClient.IndexName,
                                result.Status,
                                result.ErrorMessage);
                        }

                        errorCount++;
                    }
                }

                if (errorCount > 0)
                {
                    _logger.LogError(
                        "{ErrorCount} errors were found when indexing a batch for index {IndexName}. {LoggedErrors} were logged.",
                        errorCount,
                        indexClient.IndexName,
                        Math.Min(errorCount, errorsToLog));
                    throw new InvalidOperationException(
                        $"Errors were found when indexing a batch. Up to {errorsToLog} errors get logged.",
                        new IndexBatchException(indexingResults));
                }
            }
        }

        private async Task<HashSet<string>> UpdateVersionListsAsync(List<IdAndValue<ResultAndAccessCondition<VersionListData>>> allFinished)
        {
            if (_developmentOptions.Value.DisableVersionListWriters)
            {
                _logger.LogWarning(
                    "Skipped updating {VersionListCount} version lists.",
                    allFinished.Count);
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var versionListIdSample = allFinished
                .OrderByDescending(x => x.Value.Result.VersionProperties.Count(v => v.Value.Listed))
                .Select(x => x.Id)
                .Take(5)
                .ToArray();
            var workerCount = Math.Min(allFinished.Count, _options.Value.MaxConcurrentVersionListWriters);
            _logger.LogInformation(
                "Updating {VersionListCount} version lists with {WorkerCount} workers, including {IdSample}.",
                allFinished.Count,
                workerCount,
                versionListIdSample);

            var work = new ConcurrentBag<IdAndValue<ResultAndAccessCondition<VersionListData>>>(allFinished);
            var failedPackageIds = new ConcurrentQueue<string>();
            using (_telemetryService.TrackVersionListsUpdated(allFinished.Count, workerCount))
            {
                var tasks = Enumerable
                    .Range(0, workerCount)
                    .Select(async x =>
                    {
                        await Task.Yield();
                        while (work.TryTake(out var finished))
                        {
                            var success = await _versionListDataClient.TryReplaceAsync(
                                finished.Id,
                                finished.Value.Result,
                                finished.Value.AccessCondition);

                            if (!success)
                            {
                                failedPackageIds.Enqueue(finished.Id);
                            }
                        }
                    })
                    .ToList();
                await Task.WhenAll(tasks);

                _logger.LogInformation(
                    "Done updating {VersionListCount} version lists. {FailureCount} version lists failed.",
                    allFinished.Count - failedPackageIds.Count,
                    failedPackageIds.Count);

                return failedPackageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void EnqueueAndIncrement<T>(Queue<IdAndValue<T>> queue, string id, T value)
        {
            if (_idReferenceCount.TryGetValue(id, out var count))
            {
                Guard.Assert(count >= 1, "The existing reference count should always be greater than zero.");
                _idReferenceCount[id] = count + 1;
            }
            else
            {
                _idReferenceCount[id] = 1;
            }

            queue.Enqueue(NewIdAndValue(id, value));
        }

        private IdAndValue<T> DequeueAndDecrement<T>(Queue<IdAndValue<T>> queue, out int newCount)
        {
            var idAndValue = queue.Dequeue();

            var oldCount = _idReferenceCount[idAndValue.Id];
            newCount = oldCount - 1;
            Guard.Assert(newCount >= 0, "The reference count should never be negative.");

            if (newCount == 0)
            {
                _idReferenceCount.Remove(idAndValue.Id);
            }
            else
            {
                _idReferenceCount[idAndValue.Id] = newCount;
            }

            return idAndValue;
        }

        private IdAndValue<T> NewIdAndValue<T>(string id, T value)
        {
            return new IdAndValue<T>(id, value);
        }
    }
}

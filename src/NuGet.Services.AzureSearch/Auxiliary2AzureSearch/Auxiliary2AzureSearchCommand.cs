// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Wrappers;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Versioning;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class Auxiliary2AzureSearchCommand : IAzureSearchCommand
    {
        /// <summary>
        /// A package ID can result in one document per search filter if the there is a version that applies to each
        /// of the filters. The simplest such case is a prerelease, SemVer 1.0.0 package version like 1.0.0-beta. This
        /// version applies to all package filters.
        /// </summary>
        private static readonly int MaxDocumentsPerId = Enum.GetValues(typeof(SearchFilters)).Length;

        private readonly IAuxiliaryFileClient _auxiliaryFileClient;
        private readonly IDownloadDataClient _downloadDataClient;
        private readonly IVerifiedPackagesDataClient _verifiedPackagesDataClient;
        private readonly IDownloadSetComparer _downloadSetComparer;
        private readonly ISearchDocumentBuilder _searchDocumentBuilder;
        private readonly ISearchIndexActionBuilder _indexActionBuilder;
        private readonly Func<IBatchPusher> _batchPusherFactory;
        private readonly ISystemTime _systemTime;
        private readonly IOptionsSnapshot<Auxiliary2AzureSearchConfiguration> _options;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<Auxiliary2AzureSearchCommand> _logger;
        private readonly StringCache _stringCache;

        public Auxiliary2AzureSearchCommand(
            IAuxiliaryFileClient auxiliaryFileClient,
            IDownloadDataClient downloadDataClient,
            IVerifiedPackagesDataClient verifiedPackagesDataClient,
            IDownloadSetComparer downloadSetComparer,
            ISearchDocumentBuilder searchDocumentBuilder,
            ISearchIndexActionBuilder indexActionBuilder,
            Func<IBatchPusher> batchPusherFactory,
            ISystemTime systemTime,
            IOptionsSnapshot<Auxiliary2AzureSearchConfiguration> options,
            IAzureSearchTelemetryService telemetryService,
            ILogger<Auxiliary2AzureSearchCommand> logger)
        {
            _auxiliaryFileClient = auxiliaryFileClient ?? throw new ArgumentNullException(nameof(auxiliaryFileClient));
            _downloadDataClient = downloadDataClient ?? throw new ArgumentNullException(nameof(downloadDataClient));
            _verifiedPackagesDataClient = verifiedPackagesDataClient ?? throw new ArgumentNullException(nameof(verifiedPackagesDataClient));
            _downloadSetComparer = downloadSetComparer ?? throw new ArgumentNullException(nameof(downloadSetComparer));
            _searchDocumentBuilder = searchDocumentBuilder ?? throw new ArgumentNullException(nameof(searchDocumentBuilder));
            _indexActionBuilder = indexActionBuilder ?? throw new ArgumentNullException(nameof(indexActionBuilder));
            _batchPusherFactory = batchPusherFactory ?? throw new ArgumentNullException(nameof(batchPusherFactory));
            _systemTime = systemTime ?? throw new ArgumentNullException(nameof(systemTime));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stringCache = new StringCache();

            if (_options.Value.MaxConcurrentBatches <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"The {nameof(AzureSearchJobConfiguration.MaxConcurrentBatches)} must be greater than zero.");
            }

            if (_options.Value.MaxConcurrentVersionListWriters <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"The {nameof(AzureSearchJobConfiguration.MaxConcurrentVersionListWriters)} must be greater than zero.");
            }
        }

        public async Task ExecuteAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var outcome = JobOutcome.Failure;
            try
            {
                var hasVerifiedPackagesChanged = await CopyVerifiedPackagesAsync();
                var hasIndexChanged = await PushIndexChangesAsync();
                outcome = hasVerifiedPackagesChanged || hasIndexChanged ? JobOutcome.Success : JobOutcome.NoOp;
            }
            finally
            {
                stopwatch.Stop();
                _telemetryService.TrackAuxiliary2AzureSearchCompleted(outcome, stopwatch.Elapsed);
            }
        }

        private async Task<bool> CopyVerifiedPackagesAsync()
        {
            // The "old" data in this case is the latest file that was copied to the region's storage container by this
            // job (or initialized by Db2AzureSearch).
            var oldResult = await _verifiedPackagesDataClient.ReadLatestAsync(
                AccessConditionWrapper.GenerateEmptyCondition(),
                _stringCache);

            // The "new" data in this case is from the auxiliary data container that is updated by the
            // Search.GenerateAuxiliaryData job.
            var newData = await _auxiliaryFileClient.LoadVerifiedPackagesAsync();

            var changes = new HashSet<string>(oldResult.Data, oldResult.Data.Comparer);
            changes.SymmetricExceptWith(newData);
            _logger.LogInformation("{Count} package IDs have verified status changes.", changes.Count);

            if (changes.Count == 0)
            {
                return false;
            }
            else
            {
                await _verifiedPackagesDataClient.ReplaceLatestAsync(newData, oldResult.Metadata.GetIfMatchCondition());
                return true;
            }
        }

        private async Task<bool> PushIndexChangesAsync()
        {
            // The "old" data in this case is the download count data that was last indexed by this job (or
            // initialized by Db2AzureSearch).
            _logger.LogInformation("Fetching old download count data from blob storage.");
            var oldResult = await _downloadDataClient.ReadLatestIndexedAsync(
                AccessConditionWrapper.GenerateEmptyCondition(),
                _stringCache);

            // The "new" data in this case is from the statistics pipeline.
            _logger.LogInformation("Fetching new download count data from blob storage.");
            var newData = await _auxiliaryFileClient.LoadDownloadDataAsync();

            _logger.LogInformation("Removing invalid IDs and versions from the old data.");
            CleanDownloadData(oldResult.Data);

            _logger.LogInformation("Removing invalid IDs and versions from the new data.");
            CleanDownloadData(newData);

            // Fetch the download overrides from the auxiliary file. Note that the overriden downloads are kept
            // separate from downloads data as the original data will be persisted to auxiliary data, whereas the
            // overriden data will be persisted to Azure Search.
            _logger.LogInformation("Overriding download count data.");
            var downloadOverrides = await _auxiliaryFileClient.LoadDownloadOverridesAsync();
            var overridenDownloads = newData.ApplyDownloadOverrides(downloadOverrides, _logger);

            _logger.LogInformation("Detecting download count changes.");
            var changes = _downloadSetComparer.Compare(oldResult.Data, overridenDownloads);
            var idBag = new ConcurrentBag<string>(changes.Keys);
            _logger.LogInformation("{Count} package IDs have download count changes.", idBag.Count);

            if (!changes.Any())
            {
                return false;
            }

            _logger.LogInformation(
                "Starting {Count} workers pushing download count changes to Azure Search.",
                _options.Value.MaxConcurrentBatches);
            await ParallelAsync.Repeat(
                () => WorkAsync(idBag, changes),
                _options.Value.MaxConcurrentBatches);
            _logger.LogInformation("All of the download count changes have been pushed to Azure Search.");

            _logger.LogInformation("Uploading the new download count data to blob storage.");
            await _downloadDataClient.ReplaceLatestIndexedAsync(newData, oldResult.Metadata.GetIfMatchCondition());
            return true;
        }

        private async Task WorkAsync(ConcurrentBag<string> idBag, SortedDictionary<string, long> changes)
        {
            // Perform two batching mechanisms:
            //
            //   1. Group package IDs into batches so version lists can be fetched in parallel.
            //   2. Group index actions so documents can be pushed to Azure Search in batches.
            // 
            // Also, throttle the pushes to Azure Search based on time so that we don't cause too much load.
            var idsToIndex = new ConcurrentBag<string>();
            var indexActionsToPush = new ConcurrentBag<IdAndValue<IndexActions>>();
            var timeSinceLastPush = new Stopwatch();

            while (idBag.TryTake(out var id))
            {
                // FIRST, check if we have a full batch of package IDs to produce index actions for.
                //
                // If all of the IDs to index and the current ID were to need a document for each search filter and
                // that number plus the current index actions to push would make the batch larger than the maximum
                // batch size, produce index actions for the IDs that we have collected so far.
                if (GetBatchSize(indexActionsToPush) + ((idsToIndex.Count + 1) * MaxDocumentsPerId) > _options.Value.AzureSearchBatchSize)
                {
                    await GenerateIndexActionsAsync(idsToIndex, indexActionsToPush, changes);
                }

                // SECOND, check if we have a full batch of index actions to push to Azure Search.
                //
                // If the current ID were to need a document for each search filter and the current batch size would
                // make the batch larger than the maximum batch size, push the index actions we have so far.
                if (GetBatchSize(indexActionsToPush) + MaxDocumentsPerId > _options.Value.AzureSearchBatchSize)
                {
                    _logger.LogInformation(
                        "Starting to push a batch. There are {IdCount} unprocessed IDs left to index and push.",
                        idBag.Count);
                    await PushIndexActionsAsync(indexActionsToPush, timeSinceLastPush);
                }

                // THIRD, now that the two batching "buffers" have been flushed if necessary, add the current ID to the
                // batch of IDs to produce index actions for.
                idsToIndex.Add(id);
            }

            // Process any leftover IDs that didn't make it into a full batch.
            if (idsToIndex.Any())
            {
                await GenerateIndexActionsAsync(idsToIndex, indexActionsToPush, changes);
            }

            // Push any leftover index actions that didn't make it into a full batch.
            if (indexActionsToPush.Any())
            {
                await PushIndexActionsAsync(indexActionsToPush, timeSinceLastPush);
            }

            Guard.Assert(idsToIndex.IsEmpty, "There should be no more IDs to process.");
            Guard.Assert(indexActionsToPush.IsEmpty, "There should be no more index actions to push.");
        }

        /// <summary>
        /// Generate index actions for each provided ID. This reads the version list per package ID so we want to
        /// parallel this work by <see cref="AzureSearchJobConfiguration.MaxConcurrentVersionListWriters"/>.
        /// </summary>
        private async Task GenerateIndexActionsAsync(
            ConcurrentBag<string> idsToIndex,
            ConcurrentBag<IdAndValue<IndexActions>> indexActionsToPush,
            SortedDictionary<string, long> changes)
        {
            await ParallelAsync.Repeat(
                async () =>
                {
                    while (idsToIndex.TryTake(out var id))
                    {
                        var indexActions = await _indexActionBuilder.UpdateAsync(
                            id,
                            sf => _searchDocumentBuilder.UpdateDownloadCount(id, sf, changes[id]));

                        if (indexActions.IsEmpty)
                        {
                            continue;
                        }

                        Guard.Assert(indexActions.Hijack.Count == 0, "There should be no hijack index changes.");

                        indexActionsToPush.Add(new IdAndValue<IndexActions>(id, indexActions));
                    }
                },
                _options.Value.MaxConcurrentVersionListWriters);
        }

        private async Task<int> PushIndexActionsAsync(
            ConcurrentBag<IdAndValue<IndexActions>> indexActionsToPush,
            Stopwatch timeSinceLastPush)
        {
            var elapsed = timeSinceLastPush.Elapsed;
            if (timeSinceLastPush.IsRunning && elapsed < _options.Value.MinPushPeriod)
            {
                var timeUntilNextPush = _options.Value.MinPushPeriod - elapsed;
                _logger.LogInformation(
                    "Waiting for {Duration} before continuing.",
                    timeUntilNextPush);
                await _systemTime.Delay(timeUntilNextPush);
            }

            /// Create a new batch pusher just for this operation. Note that we don't use the internal queue of the
            /// batch pusher for more than a single batch because we want to control exactly when batches are pushed to
            /// Azure Search so that we can observe the <see cref="Auxiliary2AzureSearchConfiguration.MinPushPeriod"/>
            /// configuration property.
            var batchPusher = _batchPusherFactory();

            _logger.LogInformation(
                "Pushing a batch of {IdCount} IDs and {DocumentCount} documents.",
                indexActionsToPush.Count,
                GetBatchSize(indexActionsToPush));

            while (indexActionsToPush.TryTake(out var indexActions))
            {
                batchPusher.EnqueueIndexActions(indexActions.Id, indexActions.Value);
            }

            // Note that this method can throw a storage exception if one of the version lists has been modified during
            // the execution of this job loop.
            await batchPusher.FinishAsync();

            // Restart the timer AFTER the push is completed to err on the side of caution.
            timeSinceLastPush.Restart();

            return 0;
        }

        /// <summary>
        /// This returns the number of documents that will be pushed to an Azure Search index in a
        /// single batch. The caller is responsible that this number does not exceed
        /// <see cref="AzureSearchJobConfiguration.AzureSearchBatchSize"/>. If this job were to start pushing changes
        /// to more than one index (more than the search index), then the number returned here should be the max of
        /// the document counts per index.
        /// </summary>
        private int GetBatchSize(ConcurrentBag<IdAndValue<IndexActions>> indexActionsToPush)
        {
            return indexActionsToPush.Sum(x => x.Value.Search.Count);
        }

        private void CleanDownloadData(DownloadData data)
        {
            var invalidIdCount = 0;
            var invalidVersionCount = 0;
            var nonNormalizedVersionCount = 0;

            foreach (var id in data.Keys.ToList())
            {
                var isValidId = id.Length <= PackageIdValidator.MaxPackageIdLength && PackageIdValidator.IsValidPackageId(id);
                if (!isValidId)
                {
                    invalidIdCount++;
                }

                foreach (var version in data[id].Keys.ToList())
                {
                    var isValidVersion = NuGetVersion.TryParse(version, out var parsedVersion);
                    if (!isValidVersion)
                    {
                        invalidVersionCount++;
                    }

                    if (!isValidId || !isValidVersion)
                    {
                        // Clear the download count if the ID or version is invalid.
                        data.SetDownloadCount(id, version, 0);
                        continue;
                    }

                    var normalizedVersion = parsedVersion.ToNormalizedString();
                    var isNormalizedVersion = StringComparer.OrdinalIgnoreCase.Equals(version, normalizedVersion);

                    if (!isNormalizedVersion)
                    {
                        nonNormalizedVersionCount++;

                        // Use the normalized version string if the original was not normalized.
                        var downloads = data.GetDownloadCount(id, version);
                        data.SetDownloadCount(id, version, 0);
                        data.SetDownloadCount(id, normalizedVersion, downloads);
                    }
                }
            }

            _logger.LogInformation(
                "There were {InvalidIdCount} invalid IDs, {InvalidVersionCount} invalid versions, and " +
                "{NonNormalizedVersionCount} non-normalized IDs.",
                invalidIdCount,
                invalidVersionCount,
                nonNormalizedVersionCount);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Protocol.Catalog;
using NuGet.Services.Metadata.Catalog;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class AzureSearchCollectorLogic : ICommitCollectorLogic
    {
        private readonly ICatalogClient _catalogClient;
        private readonly ICatalogIndexActionBuilder _indexActionBuilder;
        private readonly Func<IBatchPusher> _batchPusherFactory;
        private readonly IOptionsSnapshot<Catalog2AzureSearchConfiguration> _options;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<AzureSearchCollectorLogic> _logger;

        public AzureSearchCollectorLogic(
            ICatalogClient catalogClient,
            ICatalogIndexActionBuilder indexActionBuilder,
            Func<IBatchPusher> batchPusherFactory,
            IOptionsSnapshot<Catalog2AzureSearchConfiguration> options,
            IAzureSearchTelemetryService telemetryService,
            ILogger<AzureSearchCollectorLogic> logger)
        {
            _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
            _indexActionBuilder = indexActionBuilder ?? throw new ArgumentNullException(nameof(indexActionBuilder));
            _batchPusherFactory = batchPusherFactory ?? throw new ArgumentNullException(nameof(batchPusherFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (_options.Value.MaxConcurrentBatches <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"The {nameof(AzureSearchJobConfiguration.MaxConcurrentBatches)} must be greater than zero.");
            }

            if (_options.Value.MaxConcurrentCatalogLeafDownloads <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"The {nameof(Catalog2AzureSearchConfiguration.MaxConcurrentCatalogLeafDownloads)} must be greater than zero.");
            }
        }

        public Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(IEnumerable<CatalogCommitItem> catalogItems)
        {
            if (!catalogItems.Any())
            {
                return Task.FromResult(Enumerable.Empty<CatalogCommitItemBatch>());
            }

            var maxCommitTimestamp = catalogItems.Max(x => x.CommitTimeStamp);

            // Create a single batch of all unprocessed catalog items so that we can have complete control of the
            // parallelism in this class.
            return Task.FromResult<IEnumerable<CatalogCommitItemBatch>>(new[]
            {
                new CatalogCommitItemBatch(
                    catalogItems,
                    key: null,
                    commitTimestamp: maxCommitTimestamp),
            });
        }

        public async Task OnProcessBatchAsync(
            IEnumerable<CatalogCommitItem> items)
        {
            var itemList = items.ToList();

            // Ignore all but the latest catalog commit items per package identity.
            var latestItems = items
                .GroupBy(x => x.PackageIdentity)
                .Select(GetLatest)
                .ToList();

            // Group the catalog commit items by package ID.
            var workEnumerable = latestItems
                .GroupBy(x => x.PackageIdentity.Id, StringComparer.OrdinalIgnoreCase)
                .Select(x => new Work(x.Key, x.ToList()));
            var allWork = new ConcurrentBag<Work>(workEnumerable);

            using (_telemetryService.TrackCatalog2AzureSearchProcessBatch(itemList.Count, latestItems.Count, allWork.Count))
            {
                // In parallel, generate all index actions required to handle this batch.
                var allIndexActions = await ProcessWorkAsync(latestItems, allWork);

                // In sequence, push batches of index actions to Azure Search. We do this because the maximum set of catalog
                // items that can be processed here is a single catalog page, which has around 550 items. The maximum batch
                // size for pushing to Azure Search is 1000 documents so there is no benefit to parallelizing this part.
                // Azure Search indexing on their side is more efficient with fewer, larger batches.
                var batchPusher = _batchPusherFactory();
                foreach (var indexAction in allIndexActions)
                {
                    batchPusher.EnqueueIndexActions(indexAction.Id, indexAction.Value);
                }
                await batchPusher.FinishAsync();
            }
        }

        private async Task<ConcurrentBag<IdAndValue<IndexActions>>> ProcessWorkAsync(
            IReadOnlyList<CatalogCommitItem> latestItems,
            ConcurrentBag<Work> allWork)
        {
            // Fetch the full catalog leaf for each item that is the package details type.
            var allEntryToLeaf = await GetEntryToLeafAsync(latestItems);

            // Process the package ID groups in parallel, collecting all index actions for later.
            var output = new ConcurrentBag<IdAndValue<IndexActions>>();
            var tasks = Enumerable
                .Range(0, _options.Value.MaxConcurrentBatches)
                .Select(async x =>
                {
                    await Task.Yield();
                    while (allWork.TryTake(out var work))
                    {
                        var entryToLeaf = work
                            .Entries
                            .Where(IsOnlyPackageDetails)
                            .ToDictionary(e => e, e => allEntryToLeaf[e], ReferenceEqualityComparer<CatalogCommitItem>.Default);
                        var indexActions = await GetPackageIdIndexActionsAsync(work.Entries, entryToLeaf);
                        output.Add(new IdAndValue<IndexActions>(work.PackageId, indexActions));
                    }
                })
                .ToList();
            await Task.WhenAll(tasks);

            return output;
        }

        private CatalogCommitItem GetLatest(IEnumerable<CatalogCommitItem> entries)
        {
            CatalogCommitItem max = null;
            foreach (var entry in entries)
            {
                if (max == null)
                {
                    max = entry;
                    continue;
                }

                Guard.Assert(
                    StringComparer.OrdinalIgnoreCase.Equals(max.PackageIdentity, entry.PackageIdentity),
                    "The entries compared should have the same package identity.");

                if (entry.CommitTimeStamp > max.CommitTimeStamp)
                {
                    max = entry;
                }
                else if (entry.CommitTimeStamp == max.CommitTimeStamp)
                {
                    const string message = "There are multiple catalog leaves for a single package at one time.";
                    _logger.LogError(
                        message + " ID: {PackageId}, version: {PackageVersion}, commit timestamp: {CommitTimestamp:O}",
                        entry.PackageIdentity.Id,
                        entry.PackageIdentity.Version.ToFullString(),
                        entry.CommitTimeStamp);
                    throw new InvalidOperationException(message);
                }
            }

            return max;
        }

        private async Task<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>> GetEntryToLeafAsync(
            IEnumerable<CatalogCommitItem> entries)
        {
            var packageDetailsEntries = entries.Where(IsOnlyPackageDetails);
            var allWork = new ConcurrentBag<CatalogCommitItem>(packageDetailsEntries);
            var output = new ConcurrentBag<KeyValuePair<CatalogCommitItem, PackageDetailsCatalogLeaf>>();

            using (_telemetryService.TrackCatalogLeafDownloadBatch(allWork.Count))
            {
                var tasks = Enumerable
                    .Range(0, _options.Value.MaxConcurrentCatalogLeafDownloads)
                    .Select(async x =>
                    {
                        await Task.Yield();
                        while (allWork.TryTake(out var work))
                        {
                            try
                            {
                                _logger.LogInformation(
                                    "Downloading catalog leaf for {PackageId} {Version}: {Url}",
                                    work.PackageIdentity.Id,
                                    work.PackageIdentity.Version.ToNormalizedString(),
                                    work.Uri.AbsoluteUri);

                                var leaf = await _catalogClient.GetPackageDetailsLeafAsync(work.Uri.AbsoluteUri);
                                output.Add(KeyValuePair.Create(work, leaf));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(
                                    0,
                                    ex,
                                    "An exception was thrown when fetching the package details leaf for {Id} {Version}. " +
                                    "The URL is {Url}",
                                    work.PackageIdentity.Id,
                                    work.PackageIdentity.Version,
                                    work.Uri.AbsoluteUri);
                                throw;
                            }
                        }
                    })
                    .ToList();

                await Task.WhenAll(tasks);

                return output.ToDictionary(
                    x => x.Key,
                    x => x.Value,
                    ReferenceEqualityComparer<CatalogCommitItem>.Default);
            }
        }

        private static bool IsOnlyPackageDetails(CatalogCommitItem e)
        {
            return e.IsPackageDetails && !e.IsPackageDelete;
        }

        private async Task<IndexActions> GetPackageIdIndexActionsAsync(
            IReadOnlyList<CatalogCommitItem> entries,
            IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf> entryToLeaf)
        {
            var packageId = entries
                .Select(x => x.PackageIdentity.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Single();

            return await _indexActionBuilder.AddCatalogEntriesAsync(
                packageId,
                entries,
                entryToLeaf);
        }

        private class Work
        {
            public Work(
                string packageId,
                IReadOnlyList<CatalogCommitItem> entries)
            {
                PackageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
                Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            }

            public string PackageId { get; }
            public IReadOnlyList<CatalogCommitItem> Entries { get; }
        }
    }
}

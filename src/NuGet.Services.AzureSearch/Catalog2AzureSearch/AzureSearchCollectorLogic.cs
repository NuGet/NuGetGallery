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
using NuGet.Services.V3;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class AzureSearchCollectorLogic : ICommitCollectorLogic
    {
        private readonly ICatalogIndexActionBuilder _indexActionBuilder;
        private readonly Func<IBatchPusher> _batchPusherFactory;
        private readonly IDocumentFixUpEvaluator _fixUpEvaluator;
        private readonly CommitCollectorUtility _utility;
        private readonly IOptionsSnapshot<Catalog2AzureSearchConfiguration> _options;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<AzureSearchCollectorLogic> _logger;

        public AzureSearchCollectorLogic(
            ICatalogIndexActionBuilder indexActionBuilder,
            Func<IBatchPusher> batchPusherFactory,
            IDocumentFixUpEvaluator fixUpEvaluator,
            CommitCollectorUtility utility,
            IOptionsSnapshot<Catalog2AzureSearchConfiguration> options,
            IAzureSearchTelemetryService telemetryService,
            ILogger<AzureSearchCollectorLogic> logger)
        {
            _indexActionBuilder = indexActionBuilder ?? throw new ArgumentNullException(nameof(indexActionBuilder));
            _batchPusherFactory = batchPusherFactory ?? throw new ArgumentNullException(nameof(batchPusherFactory));
            _fixUpEvaluator = fixUpEvaluator ?? throw new ArgumentNullException(nameof(fixUpEvaluator));
            _utility = utility ?? throw new ArgumentNullException(nameof(utility));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (_options.Value.MaxConcurrentBatches <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"The {nameof(AzureSearchJobConfiguration.MaxConcurrentBatches)} must be greater than zero.");
            }
        }

        public Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(IEnumerable<CatalogCommitItem> catalogItems)
        {
            // Create a single batch of all unprocessed catalog items so that we can have complete control of the
            // parallelism in this class.
            return Task.FromResult(_utility.CreateSingleBatch(catalogItems));
        }

        public async Task OnProcessBatchAsync(IEnumerable<CatalogCommitItem> items)
        {
            await ProcessItemsAsync(items, allowFixUp: true);
        }

        private async Task ProcessItemsAsync(IEnumerable<CatalogCommitItem> items, bool allowFixUp)
        {
            var itemList = items.ToList();
            var latestItems = _utility.GetLatestPerIdentity(items);
            var allWork = _utility.GroupById(latestItems);

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

                try
                {
                    await batchPusher.FinishAsync();
                }
                catch (InvalidOperationException ex) when (allowFixUp)
                {
                    var result = await _fixUpEvaluator.TryFixUpAsync(itemList, allIndexActions, ex);
                    if (!result.Applicable)
                    {
                        throw;
                    }

                    await ProcessItemsAsync(result.ItemList, allowFixUp: false);
                }
            }
        }

        private async Task<ConcurrentBag<IdAndValue<IndexActions>>> ProcessWorkAsync(
            IReadOnlyList<CatalogCommitItem> latestItems,
            ConcurrentBag<IdAndValue<IReadOnlyList<CatalogCommitItem>>> allWork)
        {
            // Fetch the full catalog leaf for each item that is the package details type.
            var allEntryToLeaf = await _utility.GetEntryToDetailsLeafAsync(latestItems);

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
                            .Value
                            .Where(CommitCollectorUtility.IsOnlyPackageDetails)
                            .ToDictionary(e => e, e => allEntryToLeaf[e], ReferenceEqualityComparer<CatalogCommitItem>.Default);
                        var indexActions = await GetPackageIdIndexActionsAsync(work.Value, entryToLeaf);
                        output.Add(new IdAndValue<IndexActions>(work.Id, indexActions));
                    }
                })
                .ToList();
            await Task.WhenAll(tasks);

            return output;
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
    }
}

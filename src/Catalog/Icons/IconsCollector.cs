// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class IconsCollector : CommitCollector
    {
        private readonly IStorageFactory _targetStorageFactory;
        private readonly ICatalogClient _catalogClient;
        private readonly ICatalogLeafDataProcessor _catalogLeafDataProcessor;
        private readonly IIconCopyResultCachePersistence _iconCopyResultCache;
        private readonly ILogger<IconsCollector> _logger;

        public IconsCollector(
            Uri index,
            ITelemetryService telemetryService,
            IStorageFactory targetStorageFactory,
            ICatalogClient catalogClient,
            ICatalogLeafDataProcessor catalogLeafDataProcessor,
            IIconCopyResultCachePersistence iconCopyResultCache,
            Func<HttpMessageHandler> httpHandlerFactory,
            ILogger<IconsCollector> logger)
            : base(index, telemetryService, httpHandlerFactory, httpClientTimeout: TimeSpan.FromMinutes(5))
        {
            _targetStorageFactory = targetStorageFactory ?? throw new ArgumentNullException(nameof(targetStorageFactory));
            _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
            _catalogLeafDataProcessor = catalogLeafDataProcessor ?? throw new ArgumentNullException(nameof(catalogLeafDataProcessor));
            _iconCopyResultCache = iconCopyResultCache ?? throw new ArgumentNullException(nameof(iconCopyResultCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(
            IEnumerable<CatalogCommitItem> catalogItems)
        {
            var maxCommitTimestamp = catalogItems.Max(x => x.CommitTimeStamp);

            return Task.FromResult<IEnumerable<CatalogCommitItemBatch>>(new[]
            {
                new CatalogCommitItemBatch(
                    catalogItems,
                    key: null,
                    commitTimestamp: maxCommitTimestamp),
            });
        }

        protected override async Task<bool> OnProcessBatchAsync(
            CollectorHttpClient client,
            IEnumerable<CatalogCommitItem> items,
            JToken context,
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken)
        {
            await _iconCopyResultCache.InitializeAsync(cancellationToken);

            var filteredItems = items
                .GroupBy(i => i.PackageIdentity)                          // if we have multiple commits for the same package (id AND version)
                .Select(g => g.OrderBy(i => i.CommitTimeStamp).ToList()); // group them together for processing in order
            var itemsToProcess = new ConcurrentBag<IReadOnlyCollection<CatalogCommitItem>>(filteredItems);
            var tasks = Enumerable
                .Range(1, ServicePointManager.DefaultConnectionLimit)
                .Select(_ => ProcessIconsAsync(itemsToProcess, cancellationToken));
            await Task.WhenAll(tasks);

            await _iconCopyResultCache.SaveAsync(cancellationToken);

            return true;
        }

        private async Task ProcessIconsAsync(
            ConcurrentBag<IReadOnlyCollection<CatalogCommitItem>> items,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            var storage = _targetStorageFactory.Create();

            using (_logger.BeginScope("{CallGuid}", Guid.NewGuid()))
            while (items.TryTake(out var entries))
            {
                var firstItem = entries.First();
                using (_logger.BeginScope("Processing commits for {PackageId} {PackageVersion}", firstItem.PackageIdentity.Id, firstItem.PackageIdentity.Version))
                {
                    foreach (var item in entries)
                    {
                        if (item.IsPackageDetails)
                        {
                            PackageDetailsCatalogLeaf leaf;
                            try
                            {
                                leaf = await _catalogClient.GetPackageDetailsLeafAsync(item.Uri.AbsoluteUri);
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(0, e, "Error while trying to retrieve catalog leaf {LeafUrl}", item.Uri.AbsoluteUri);
                                throw;
                            }
                            await _catalogLeafDataProcessor.ProcessPackageDetailsLeafAsync(storage, item, leaf.IconUrl, leaf.IconFile, cancellationToken);
                        }
                        else if (item.IsPackageDelete)
                        {
                            await _catalogLeafDataProcessor.ProcessPackageDeleteLeafAsync(storage, item, cancellationToken);
                        }
                    }
                }
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Storage;

namespace NuGet.Services.V3PerPackage
{
    public class EnqueueCollector : CommitCollector
    {
        private readonly IStorageQueue<PackageMessage> _queue;

        public EnqueueCollector(
            IOptionsSnapshot<V3PerPackageConfiguration> configuration,
            ITelemetryService telemetryService,
            IStorageQueue<PackageMessage> queue,
            Func<HttpMessageHandler> handlerFunc) : base(
                configuration.Value.SourceCatalogIndex,
                telemetryService,
                handlerFunc)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        protected override Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(IEnumerable<CatalogCommitItem> catalogItems)
        {
            var catalogItemList = catalogItems.ToList();

            var maxCommitTimestamp = catalogItems.Max(x => x.CommitTimeStamp);

            return Task.FromResult(new[] { new CatalogCommitItemBatch(catalogItemList) }.AsEnumerable());
        }

        protected override async Task<bool> OnProcessBatchAsync(
            CollectorHttpClient client,
            IEnumerable<CatalogCommitItem> items,
            JToken context,
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken)
        {
            var itemBag = new ConcurrentBag<CatalogCommitItem>(items);

            var tasks = Enumerable
                .Range(0, 16)
                .Select(_ => EnqueueAsync(itemBag, cancellationToken))
                .ToList();

            await Task.WhenAll(tasks);

            return true;
        }

        private async Task EnqueueAsync(ConcurrentBag<CatalogCommitItem> itemBag, CancellationToken cancellationToken)
        {
            while (itemBag.TryTake(out var item))
            {
                if (!item.TypeUris.Any(itemType => itemType.AbsoluteUri != Schema.DataTypes.PackageDetails.AbsoluteUri))
                {
                    continue;
                }

                var id = item.PackageIdentity.Id.ToLowerInvariant();
                var version = item.PackageIdentity.Version.ToNormalizedString().ToLowerInvariant();

                await _queue.AddAsync(new PackageMessage(id, version), cancellationToken);
            }
        }
    }
}
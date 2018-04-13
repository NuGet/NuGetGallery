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
using NuGet.Services.Metadata.Catalog.Helpers;
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

        protected override Task<IEnumerable<CatalogItemBatch>> CreateBatches(IEnumerable<CatalogItem> catalogItems)
        {
            var catalogItemList = catalogItems.ToList();

            var maxCommitTimestamp = catalogItems.Max(x => x.CommitTimeStamp);

            return Task.FromResult(new[] { new CatalogItemBatch(maxCommitTimestamp, catalogItemList) }.AsEnumerable());
        }

        protected override async Task<bool> OnProcessBatch(
            CollectorHttpClient client,
            IEnumerable<JToken> items,
            JToken context,
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken)
        {
            var itemBag = new ConcurrentBag<JToken>(items);

            var tasks = Enumerable
                .Range(0, 16)
                .Select(_ => EnqueueAsync(itemBag, cancellationToken))
                .ToList();

            await Task.WhenAll(tasks);

            return true;
        }

        private async Task EnqueueAsync(ConcurrentBag<JToken> itemBag, CancellationToken cancellationToken)
        {
            while (itemBag.TryTake(out var item))
            {
                var id = item["nuget:id"].ToString().ToLowerInvariant();
                var version = NuGetVersionUtility.NormalizeVersion(item["nuget:version"].ToString().ToLowerInvariant());
                var type = item["@type"].ToString().Replace("nuget:", Schema.Prefixes.NuGet);

                if (type != Schema.DataTypes.PackageDetails.ToString())
                {
                    continue;
                }

                await _queue.AddAsync(new PackageMessage(id, version), cancellationToken);
            }
        }
    }
}

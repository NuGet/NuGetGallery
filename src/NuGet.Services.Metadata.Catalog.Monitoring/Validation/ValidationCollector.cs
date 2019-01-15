// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Storage;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Creates <see cref="PackageValidatorContext"/>s from Catalog entries and adds them to a <see cref="IStorageQueue{PackageValidatorContext}"/>s.
    /// </summary>
    public class ValidationCollector : SortingIdVersionCollector
    {
        private readonly IStorageQueue<PackageValidatorContext> _queue;

        private ILogger<ValidationCollector> _logger;

        public ValidationCollector(
            IStorageQueue<PackageValidatorContext> queue,
            Uri index,
            ITelemetryService telemetryService,
            ILogger<ValidationCollector> logger,
            Func<HttpMessageHandler> handlerFunc = null)
            : base(index, telemetryService, handlerFunc)
        {
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ProcessSortedBatchAsync(
            CollectorHttpClient client,
            KeyValuePair<FeedPackageIdentity, IList<CatalogCommitItem>> sortedBatch,
            JToken context,
            CancellationToken cancellationToken)
        {
            var packageId = sortedBatch.Key.Id;
            var packageVersion = sortedBatch.Key.Version;
            var feedPackage = new FeedPackageIdentity(packageId, packageVersion);

            _logger.LogInformation("Processing catalog entries for {PackageId} {PackageVersion}.", packageId, packageVersion);

            var catalogEntries = sortedBatch.Value.Select(CatalogIndexEntry.Create);

            _logger.LogInformation("Adding {MostRecentCatalogEntryUri} to queue.", catalogEntries.OrderByDescending(c => c.CommitTimeStamp).First().Uri);

            await _queue.AddAsync(
                new PackageValidatorContext(feedPackage, catalogEntries),
                cancellationToken);
        }
    }
}
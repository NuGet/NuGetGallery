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

namespace NuGet.Services.V3
{
    public class CommitCollectorUtility
    {
        private readonly ICatalogClient _catalogClient;
        private readonly IV3TelemetryService _telemetryService;
        private readonly IOptionsSnapshot<CommitCollectorConfiguration> _options;
        private readonly ILogger<CommitCollectorUtility> _logger;

        public CommitCollectorUtility(
            ICatalogClient catalogClient,
            IV3TelemetryService telemetryService,
            IOptionsSnapshot<CommitCollectorConfiguration> options,
            ILogger<CommitCollectorUtility> logger)
        {
            _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (_options.Value.MaxConcurrentCatalogLeafDownloads <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"The {nameof(ICommitCollectorConfiguration.MaxConcurrentCatalogLeafDownloads)} must be greater than zero.");
            }
        }

        public IEnumerable<CatalogCommitItemBatch> CreateSingleBatch(IEnumerable<CatalogCommitItem> catalogItems)
        {
            if (!catalogItems.Any())
            {
                return Enumerable.Empty<CatalogCommitItemBatch>();
            }

            var maxCommitTimestamp = catalogItems.Max(x => x.CommitTimeStamp);

            return new[]
            {
                new CatalogCommitItemBatch(
                    catalogItems,
                    key: null,
                    commitTimestamp: maxCommitTimestamp),
            };
        }

        public List<CatalogCommitItem> GetLatestPerIdentity(IEnumerable<CatalogCommitItem> items)
        {
            return items
                .GroupBy(x => x.PackageIdentity)
                .Select(GetLatestPerSingleIdentity)
                .ToList();
        }

        private CatalogCommitItem GetLatestPerSingleIdentity(IEnumerable<CatalogCommitItem> entries)
        {
            CatalogCommitItem max = null;
            foreach (var entry in entries)
            {
                if (max == null)
                {
                    max = entry;
                    continue;
                }

                if (!StringComparer.OrdinalIgnoreCase.Equals(max.PackageIdentity, entry.PackageIdentity))
                {
                    throw new InvalidOperationException("The entries compared should have the same package identity.");
                }

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

        public ConcurrentBag<IdAndValue<IReadOnlyList<CatalogCommitItem>>> GroupById(List<CatalogCommitItem> latestItems)
        {
            var workEnumerable = latestItems
                .GroupBy(x => x.PackageIdentity.Id, StringComparer.OrdinalIgnoreCase)
                .Select(x => new IdAndValue<IReadOnlyList<CatalogCommitItem>>(x.Key, x.ToList()));

            var allWork = new ConcurrentBag<IdAndValue<IReadOnlyList<CatalogCommitItem>>>(workEnumerable);
            return allWork;
        }

        public async Task<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>> GetEntryToDetailsLeafAsync(
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

        public static bool IsOnlyPackageDetails(CatalogCommitItem e)
        {
            return e.IsPackageDetails && !e.IsPackageDelete;
        }
    }
}

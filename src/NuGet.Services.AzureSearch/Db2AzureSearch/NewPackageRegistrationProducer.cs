// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class NewPackageRegistrationProducer : INewPackageRegistrationProducer
    {
        private readonly IEntitiesContextFactory _contextFactory;
        private readonly IOptionsSnapshot<Db2AzureSearchConfiguration> _options;
        private readonly ILogger<NewPackageRegistrationProducer> _logger;
        private readonly IAuxiliaryFileClient _auxiliaryFileClient;

        public NewPackageRegistrationProducer(
            IEntitiesContextFactory contextFactory,
            IOptionsSnapshot<Db2AzureSearchConfiguration> options,
            IAuxiliaryFileClient auxiliaryFileClient,
            ILogger<NewPackageRegistrationProducer> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _auxiliaryFileClient = auxiliaryFileClient ?? throw new ArgumentNullException(nameof(auxiliaryFileClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<InitialAuxiliaryData> ProduceWorkAsync(
            ConcurrentBag<NewPackageRegistration> allWork,
            CancellationToken cancellationToken)
        {
            var ranges = await GetPackageRegistrationRangesAsync();

            // Fetch exclude packages list from auxiliary files.
            // These packages are excluded from the default search's results.
            var excludedPackages = await _auxiliaryFileClient.LoadExcludedPackagesAsync();

            Guard.Assert(
                excludedPackages.Comparer == StringComparer.OrdinalIgnoreCase,
                $"Excluded packages HashSet should be using {nameof(StringComparer.OrdinalIgnoreCase)}");

            // Fetch the download data from the auxiliary file, since this is what is used for displaying download
            // counts in the search service. The gallery DB and the downloads data file have different download count
            // numbers we don't use the gallery DB values.
            var downloads = await _auxiliaryFileClient.LoadDownloadDataAsync();

            // Fetch the download overrides from the auxiliary file. Note that the overriden downloads are kept
            // separate from downloads data as the original data will be persisted to auxiliary data, whereas the
            // overriden data will be persisted to Azure Search.
            var downloadOverrides = await _auxiliaryFileClient.LoadDownloadOverridesAsync();
            var overridenDownloads = downloads.ApplyDownloadOverrides(downloadOverrides, _logger);

            // Build a list of the owners data and verified IDs as we collect package registrations from the database.
            var ownersBuilder = new PackageIdToOwnersBuilder(_logger);
            var verifiedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < ranges.Count && !cancellationToken.IsCancellationRequested; i++)
            {
                if (ShouldWait(allWork, log: true))
                {
                    while (ShouldWait(allWork, log: false))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }

                    _logger.LogInformation("Resuming fetching package registrations from the database.");
                }

                var range = ranges[i];

                var allPackages = await GetPackagesAsync(range);
                var keyToPackages = allPackages
                    .GroupBy(x => x.PackageRegistrationKey)
                    .ToDictionary(x => x.Key, x => x.ToList());

                var packageRegistrationInfo = await GetPackageRegistrationInfoAsync(range);

                foreach (var pr in packageRegistrationInfo)
                {
                    if (!keyToPackages.TryGetValue(pr.Key, out var packages))
                    {
                        packages = new List<Package>();
                    }

                    var isExcludedByDefault = excludedPackages.Contains(pr.Id);

                    allWork.Add(new NewPackageRegistration(
                        pr.Id,
                        overridenDownloads.GetDownloadCount(pr.Id),
                        pr.Owners,
                        packages,
                        isExcludedByDefault));

                    ownersBuilder.Add(pr.Id, pr.Owners);

                    if (pr.IsVerified)
                    {
                        verifiedPackages.Add(pr.Id);
                    }
                }

                _logger.LogInformation("Done initializing batch {Number}/{Count}.", i + 1, ranges.Count);
            }

            return new InitialAuxiliaryData(
                ownersBuilder.GetResult(),
                downloads,
                excludedPackages,
                verifiedPackages);
        }

        private bool ShouldWait(ConcurrentBag<NewPackageRegistration> allWork, bool log)
        {
            var packageCount = allWork.Sum(x => x.Packages.Count);
            var max = 2 * _options.Value.DatabaseBatchSize;

            if (packageCount > max)
            {
                if (log)
                {
                    _logger.LogInformation(
                        "There are {PackageCount} packages in memory waiting to be pushed to Azure Search. " +
                        "Waiting until this number drops below {Max} before fetching more packages.",
                        packageCount,
                        max);
                }

                return true;
            }

            return false;
        }

        private async Task<IReadOnlyList<Package>> GetPackagesAsync(PackageRegistrationRange range)
        {
            using (var context = await CreateContextAsync())
            {
                var minKey = range.MinKey;
                var query = context
                    .Set<Package>()
                    .Include(x => x.PackageRegistration)
                    .Include(x => x.PackageTypes)
                    .Where(p => p.PackageStatusKey == PackageStatus.Available)
                    .Where(p => p.PackageRegistrationKey >= minKey);

                if (range.MaxKey.HasValue)
                {
                    var maxKey = range.MaxKey.Value;
                    query = query
                        .Where(p => p.PackageRegistrationKey <= maxKey);
                }

                LogFetching("packages", range);

                return await query.ToListAsync();
            }
        }

        private void LogFetching(string fetched, PackageRegistrationRange range)
        {
            if (range.MaxKey.HasValue)
            {
                _logger.LogInformation(
                    "Fetching " + fetched + " with package registration key >= {MinKey} and <= {MaxKey} (~{Count} packages).",
                    range.MinKey,
                    range.MaxKey,
                    range.PackageCount);
            }
            else
            {
                _logger.LogInformation("Fetching " + fetched + " with package registration key >= {MinKey} (~{Count} packages).",
                    range.MinKey,
                    range.PackageCount);
            }
        }

        private async Task<IReadOnlyList<PackageRegistrationInfo>> GetPackageRegistrationInfoAsync(PackageRegistrationRange range)
        {
            using (var context = await CreateContextAsync())
            {
                var minKey = range.MinKey;
                var query = context
                    .Set<PackageRegistration>()
                    .Include(x => x.Owners)
                    .Where(pr => pr.Key >= minKey);

                if (range.MaxKey.HasValue)
                {
                    var maxKey = range.MaxKey.Value;
                    query = query
                        .Where(pr => pr.Key <= maxKey);
                }

                LogFetching("owners", range);

                var packageRegistrations = await query.ToListAsync();

                return packageRegistrations
                    .Where(pr => !ShouldSkipPackageRegistration(pr))
                    .Select(pr => new PackageRegistrationInfo(
                        pr.Key,
                        pr.Id,
                        pr.Owners.Select(x => x.Username).ToArray(),
                        pr.IsVerified))
                    .ToList();
            }
        }

        private bool ShouldSkipPackageRegistration(PackageRegistration packageRegistration)
        {
            if (_options.Value.SkipPackagePrefixes == null)
            {
                return false;
            }

            foreach (var skippedPackagePrefix in _options.Value.SkipPackagePrefixes)
            {
                if (packageRegistration.Id.StartsWith(skippedPackagePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<IReadOnlyList<PackageRegistrationRange>> GetPackageRegistrationRangesAsync()
        {
            using (var context = await CreateContextAsync())
            {
                _logger.LogInformation("Fetching all package registration keys and their available package counts.");

                // Get the number of packages per package registration key, in ascending order.
                var stopwatch = Stopwatch.StartNew();
                var packageCounts = await context
                    .Set<PackageRegistration>()
                    .OrderBy(pr => pr.Key)
                    .Select(pr => new
                    {
                        pr.Key,
                        PackageCount = pr.Packages.Where(p => p.PackageStatusKey == PackageStatus.Available).Count()
                    })
                    .ToListAsync();
                var totalPackages = packageCounts.Sum(pr => pr.PackageCount);

                // Sequentially group the package registrations up to a maximum batch size. If a single package
                // registration has a package count that is more than the batch size, it will be in its own batch.
                var batches = packageCounts
                    .Batch(pr => pr.PackageCount, _options.Value.DatabaseBatchSize)
                    .ToList();

                _logger.LogInformation(
                    "Got {Count} package registrations, {BatchCount} batches, which have {PackageCount} packages. Took {Duration}.",
                    packageCounts.Count,
                    batches.Count,
                    totalPackages,
                    stopwatch.Elapsed);

                // For each batch, generate a package registration key range. These range of keys collectively cover all
                // possible integer keys. We want to cover all possible integer keys so that we very clearly will avoid
                // missing any data.
                var ranges = new List<PackageRegistrationRange>();
                for (var i = 0; i < batches.Count; i++)
                {
                    int minKey;
                    if (i == 0)
                    {
                        minKey = 1;
                    }
                    else
                    {
                        minKey = batches[i][0].Key;
                    }

                    int? maxKey;
                    if (i < batches.Count - 1)
                    {
                        maxKey = batches[i + 1][0].Key - 1;
                    }
                    else
                    {
                        maxKey = null;
                    }

                    var packageCount = batches[i].Sum(x => x.PackageCount);
                    ranges.Add(new PackageRegistrationRange(minKey, maxKey, packageCount));
                }

                return ranges;
            }
        }

        private async Task<IEntitiesContext> CreateContextAsync()
        {
            return await _contextFactory.CreateAsync(readOnly: true);
        }

        private class PackageRegistrationRange
        {
            public PackageRegistrationRange(int minKey, int? maxKey, int packageCount)
            {
                MinKey = minKey;
                MaxKey = maxKey;
                PackageCount = packageCount;
            }

            /// <summary>
            /// Inclusive package registration key minimum.
            /// </summary>
            public int MinKey { get; }

            /// <summary>
            /// Inclusive package registration key maximum. If this value is null, the range has an unbounded maximum.
            /// </summary>
            public int? MaxKey { get; }

            /// <summary>
            /// The estimated number of packages in this range.
            /// </summary>
            public int PackageCount { get; }
        }

        private class PackageRegistrationInfo
        {
            public PackageRegistrationInfo(int key, string id, string[] owners, bool isVerified)
            {
                Key = key;
                Id = id;
                Owners = owners;
                IsVerified = isVerified;
            }

            public int Key { get; }
            public string Id { get; }
            public string[] Owners { get; }
            public bool IsVerified { get; }
        }
    }
}

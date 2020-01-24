// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.V3;

namespace NuGet.Jobs.RegistrationComparer
{
    public class RegistrationComparerCollectorLogic : ICommitCollectorLogic
    {
        private readonly CommitCollectorUtility _utility;
        private readonly HiveComparer _comparer;
        private readonly IOptionsSnapshot<RegistrationComparerConfiguration> _options;
        private readonly ILogger<RegistrationComparerCollectorLogic> _logger;

        public RegistrationComparerCollectorLogic(
            CommitCollectorUtility utility,
            HiveComparer comparer,
            IOptionsSnapshot<RegistrationComparerConfiguration> options,
            ILogger<RegistrationComparerCollectorLogic> logger)
        {
            _utility = utility ?? throw new ArgumentNullException(nameof(utility));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(IEnumerable<CatalogCommitItem> catalogItems)
        {
            return Task.FromResult(_utility.CreateSingleBatch(catalogItems));
        }

        public async Task OnProcessBatchAsync(IEnumerable<CatalogCommitItem> items)
        {
            var packageIdGroups = items
                .GroupBy(x => x.PackageIdentity.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    Id = g.Key.ToLowerInvariant(),
                    Versions = g
                        .Select(x => x.PackageIdentity.Version)
                        .Distinct()
                        .OrderBy(x => x)
                        .Select(x => x.ToNormalizedString().ToLowerInvariant())
                        .ToList(),
                })
                .ToList();

            _logger.LogInformation("Comparing {Count} package IDs.", packageIdGroups.Count);

            var hiveGroups = _options
                .Value
                .Registrations
                .SelectMany(x => new[]
                {
                    new { Hive = nameof(x.LegacyBaseUrl), Url = x.LegacyBaseUrl.TrimEnd('/') + '/' },
                    new { Hive = nameof(x.GzippedBaseUrl), Url = x.GzippedBaseUrl.TrimEnd('/') + '/' },
                    new { Hive = nameof(x.SemVer2BaseUrl), Url = x.SemVer2BaseUrl.TrimEnd('/') + '/' },
                })
                .GroupBy(x => x.Hive, x => x.Url);

            var allWork = new ConcurrentBag<Func<Task>>();
            var failures = 0;
            foreach (var group in packageIdGroups)
            {
                foreach (var hiveGroup in hiveGroups)
                {
                    var baseUrls = hiveGroup.ToList();
                    var hive = hiveGroup.Key;
                    var id = group.Id;
                    var versions = group.Versions;
                    allWork.Add(async () =>
                    {
                        _logger.LogInformation("Verifying hive {Hive} for {PackageId}.", hive, id);
                        try
                        {
                            await _comparer.CompareAsync(baseUrls, id, versions);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failures);
                            _logger.LogError(ex, "The comparison failed.");
                        }
                    });
                }
            }

            await ParallelAsync
                .Repeat(async () =>
                {
                    await Task.Yield();
                    while (allWork.TryTake(out var work))
                    {
                        await work();
                    }
                },
                degreeOfParallelism: _options.Value.MaxConcurrentComparisons);

            if (failures > 0)
            {
                throw new InvalidOperationException($"{failures} hives failed the comparison.");
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Protocol.Catalog;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Jobs.Catalog2Registration
{
    public class RegistrationUpdater : IRegistrationUpdater
    {
        private static readonly Dictionary<HiveType, IReadOnlyList<HiveType>> HiveToReplicaHives = new Dictionary<HiveType, IReadOnlyList<HiveType>>
        {
            {
                HiveType.Legacy,
                new List<HiveType> { HiveType.Gzipped }
            },
            {
                HiveType.SemVer2,
                new List<HiveType>()
            },
        };

        private readonly IHiveUpdater _hiveUpdater;
        private readonly IOptionsSnapshot<Catalog2RegistrationConfiguration> _options;
        private readonly ILogger<RegistrationUpdater> _logger;

        public RegistrationUpdater(
            IHiveUpdater hiveUpdater,
            IOptionsSnapshot<Catalog2RegistrationConfiguration> options,
            ILogger<RegistrationUpdater> logger)
        {
            _hiveUpdater = hiveUpdater ?? throw new ArgumentNullException(nameof(hiveUpdater));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (_options.Value.MaxConcurrentHivesPerId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"The {nameof(Catalog2RegistrationConfiguration.MaxConcurrentHivesPerId)} must be greater than zero.");
            }
        }

        public async Task UpdateAsync(
            string id,
            IReadOnlyList<CatalogCommitItem> entries,
            IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf> entryToLeaf)
        {
            var registrationCommitTimestamp = DateTimeOffset.UtcNow;
            var hives = new ConcurrentBag<HiveType>(HiveToReplicaHives.Keys);
            await ParallelAsync.Repeat(
                async () =>
                {
                    await Task.Yield();
                    while (hives.TryTake(out var hive))
                    {
                        await ProcessHiveAsync(hive, id, entries, entryToLeaf, registrationCommitTimestamp);
                    }
                },
                _options.Value.MaxConcurrentHivesPerId);
        }

        private async Task ProcessHiveAsync(
            HiveType hive,
            string id,
            IReadOnlyList<CatalogCommitItem> entries,
            IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf> entryToLeaf,
            DateTimeOffset registrationCommitTimestamp)
        {
            var registrationCommitId = Guid.NewGuid().ToString();
            var registrationCommit = new CatalogCommit(registrationCommitId, registrationCommitTimestamp);
            using (_logger.BeginScope(
                "Processing package {PackageId}, " +
                "hive {Hive}, " +
                "replica hives {ReplicaHives}, " +
                "registration commit ID {RegistrationCommitId}, " +
                "registration commit timestamp {RegistrationCommitTimestamp:O}.",
                id,
                hive,
                HiveToReplicaHives[hive],
                registrationCommitId,
                registrationCommitTimestamp))
            {
                _logger.LogInformation("Processing {Count} catalog commit items.", entries.Count);
                await _hiveUpdater.UpdateAsync(hive, HiveToReplicaHives[hive], id, entries, entryToLeaf, registrationCommit);
            }
        }
    }
}

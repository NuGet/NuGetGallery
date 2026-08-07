// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.Catalog2Registration
{
    public class RegistrationLevelUpdater : IRegistrationLevelUpdater
    {
        /// <summary>
        /// The primary hives that own a registration index, mapped to their replica hives. This mirrors
        /// <see cref="RegistrationUpdater"/> so that ID-level attributes are written to exactly the same set of hives
        /// (and their replicas) as version-level metadata. Writing a primary hive automatically writes its replicas.
        /// </summary>
        private static readonly IReadOnlyDictionary<HiveType, IReadOnlyList<HiveType>> HiveToReplicaHives = new Dictionary<HiveType, IReadOnlyList<HiveType>>
        {
            { HiveType.Legacy, new List<HiveType> { HiveType.Gzipped } },
            { HiveType.SemVer2, new List<HiveType>() },
        };

        private readonly IHiveStorage _hiveStorage;
        private readonly IEntityBuilder _entityBuilder;
        private readonly ILogger<RegistrationLevelUpdater> _logger;

        public RegistrationLevelUpdater(
            IHiveStorage hiveStorage,
            IEntityBuilder entityBuilder,
            ILogger<RegistrationLevelUpdater> logger)
        {
            _hiveStorage = hiveStorage ?? throw new ArgumentNullException(nameof(hiveStorage));
            _entityBuilder = entityBuilder ?? throw new ArgumentNullException(nameof(entityBuilder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task UpdateAsync(string id, IReadOnlyList<string> sponsorshipUrls, DateTimeOffset commitTimestamp)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("The package id must not be null or empty.", nameof(id));
            }

            var hasSponsorshipUrls = sponsorshipUrls != null && sponsorshipUrls.Count > 0;
            _logger.LogInformation(
                "Applying {Count} ID-level sponsorship URLs to the {PackageId} registration index.",
                hasSponsorshipUrls ? sponsorshipUrls.Count : 0,
                id);

            foreach (var pair in HiveToReplicaHives)
            {
                var hive = pair.Key;
                var replicaHives = pair.Value;

                var index = await _hiveStorage.ReadIndexOrNullAsync(hive, id);
                if (index == null)
                {
                    // The registration index only exists once the package has at least one published version. If no
                    // index exists yet, there is nothing to stamp; the ID-level attribute is effectively dropped for
                    // this hive until a version is published.
                    _logger.LogInformation(
                        "No registration index exists for {PackageId} in the {Hive} hive; skipping ID-level update.",
                        id,
                        hive);
                    continue;
                }

                // Read-modify-write only the ID-level field. The version-level lane preserves this field because it
                // reads the existing index object and never touches SponsorshipUrls.
                index.SponsorshipUrls = hasSponsorshipUrls ? new List<string>(sponsorshipUrls) : null;
                _entityBuilder.UpdateCommit(index, new CatalogCommit(Guid.NewGuid().ToString(), commitTimestamp));

                await _hiveStorage.WriteIndexAsync(hive, replicaHives, id, index);

                _logger.LogInformation(
                    "Updated ID-level sponsorship URLs on the {PackageId} registration index in the {Hive} hive and {ReplicaHives} replica hives.",
                    id,
                    hive,
                    replicaHives);
            }
        }
    }
}

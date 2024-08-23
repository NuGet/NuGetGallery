// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Helpers;

using CatalogStorage = NuGet.Services.Metadata.Catalog.Persistence.Storage;

namespace NuGet.Services.Metadata.Catalog.Monitoring.Monitoring
{
    /// <summary>
    /// Fetches <see cref="PackageStatusOutdatedCheck"/>s from <see cref="DeletionAuditEntry.GetAsync(Persistence.IStorage, CancellationToken, DateTime?, DateTime?, ILogger)"/>.
    /// The <see cref="PackageStatusOutdatedCheck"/>s fetched represent packages that were deleted.
    /// </summary>
    /// <remarks>
    /// Some packages that were deleted may have been reuploaded.
    /// This source does not prevent those packages from being returned.
    /// </remarks>
    public class AuditingStoragePackageStatusOutdatedCheckSource : PackageStatusOutdatedCheckSource<DeletionAuditEntry>
    {
        private readonly CatalogStorage _auditingStorage;
        private readonly ILogger<AuditingStoragePackageStatusOutdatedCheckSource> _logger;

        private IReadOnlyCollection<DeletionAuditEntry> _cachedAuditEntries;

        public AuditingStoragePackageStatusOutdatedCheckSource(
            ReadWriteCursor cursor,
            CatalogStorage auditingStorage,
            ILogger<AuditingStoragePackageStatusOutdatedCheckSource> logger)
            : base(cursor)
        {
            _auditingStorage = auditingStorage ?? throw new ArgumentNullException(nameof(auditingStorage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override DateTime GetCursorValue(DeletionAuditEntry package)
        {
            return package.TimestampUtc.Value;
        }

        protected override PackageStatusOutdatedCheck GetPackageStatusOutdatedCheck(DeletionAuditEntry package)
        {
            return new PackageStatusOutdatedCheck(package);
        }

        protected override async Task<IReadOnlyCollection<DeletionAuditEntry>> GetPackagesToCheckAsync(DateTime since, DateTime max, int top, CancellationToken cancellationToken)
        {
            // Fetching audit entries is expensive, so cache them.
            // When we run out of cached entries, fetch more.
            if (_cachedAuditEntries == null || !_cachedAuditEntries.Any())
            {
                _logger.LogInformation("Fetching audit entries from storage.");
                var auditEntries = await DeletionAuditEntry
                    .GetAsync(_auditingStorage, CancellationToken.None, minTime: since, maxTime: max, logger: _logger);

                // A package may have multiple deleted audit entries.
                // Choose only the latest for each package.
                _cachedAuditEntries = auditEntries
                    .GroupBy(e => new FeedPackageIdentity(e.PackageId, e.PackageVersion))
                    .Select(g => g.OrderByDescending(e => e.TimestampUtc).First())
                    .OrderBy(e => e.TimestampUtc)
                    .ToList();

                _logger.LogInformation("Cached {AuditEntryCount} audit entries.", _cachedAuditEntries.Count());
            }
            else
            {
                _logger.LogInformation("Using cached audit entries.");
            }

            var nextAuditEntries = _cachedAuditEntries.Take(top).ToList();
            _cachedAuditEntries = _cachedAuditEntries
                .Skip(top)
                .ToList();

            return nextAuditEntries;
        }
    }
}

﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class PackageUpdateService : IPackageUpdateService
    {
        private readonly IPackageService _packageService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly ITelemetryService _telemetryService;
        private readonly IAuditingService _auditingService;
        private readonly IIndexingService _indexingService;

        public PackageUpdateService(
            IPackageService packageService,
            IEntitiesContext entitiesContext,
            ITelemetryService telemetryService,
            IAuditingService auditingService,
            IIndexingService indexingService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _indexingService = indexingService ?? throw new ArgumentNullException(nameof(indexingService));
        }

        public async Task MarkPackageListedAsync(Package package, bool commitChanges = true, bool updateIndex = true)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.Listed)
            {
                return;
            }

            if (package.PackageStatusKey == PackageStatus.Deleted)
            {
                throw new InvalidOperationException("A deleted package should never be listed!");
            }

            if (package.PackageStatusKey == PackageStatus.FailedValidation)
            {
                throw new InvalidOperationException("A package that failed validation should never be listed!");
            }

            if (!package.Listed && (package.IsLatestStable || package.IsLatest || package.IsLatestSemVer2 || package.IsLatestStableSemVer2))
            {
                throw new InvalidOperationException("An unlisted package should never be latest or latest stable!");
            }

            package.Listed = true;
            package.LastUpdated = DateTime.UtcNow;
            // NOTE: LastEdited will be overwritten by a trigger defined in the migration named "AddTriggerForPackagesLastEdited".
            package.LastEdited = DateTime.UtcNow;

            await _packageService.UpdateIsLatestAsync(package.PackageRegistration, commitChanges: false);

            await _auditingService.SaveAuditRecordAsync(new PackageAuditRecord(package, AuditedPackageAction.List));

            _telemetryService.TrackPackageListed(package);

            if (commitChanges)
            {
                await _entitiesContext.SaveChangesAsync();
            }

            if (updateIndex)
            {
                _indexingService.UpdatePackage(package);
            }
        }

        public async Task MarkPackageUnlistedAsync(Package package, bool commitChanges = true, bool updateIndex = true)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (!package.Listed)
            {
                return;
            }

            package.Listed = false;
            package.LastUpdated = DateTime.UtcNow;
            // NOTE: LastEdited will be overwritten by a trigger defined in the migration named "AddTriggerForPackagesLastEdited".
            package.LastEdited = DateTime.UtcNow;

            if (package.IsLatest || package.IsLatestStable || package.IsLatestSemVer2 || package.IsLatestStableSemVer2)
            {
                await _packageService.UpdateIsLatestAsync(package.PackageRegistration, commitChanges: false);
            }

            await _auditingService.SaveAuditRecordAsync(new PackageAuditRecord(package, AuditedPackageAction.Unlist));

            _telemetryService.TrackPackageUnlisted(package);

            if (commitChanges)
            {
                await _entitiesContext.SaveChangesAsync();
            }

            if (updateIndex)
            {
                _indexingService.UpdatePackage(package);
            }
        }

        public async Task UpdatePackagesAsync(IReadOnlyList<Package> packages, bool? listed, IDbContextTransaction transaction, bool updateIndex = true)
        {
            if (packages == null || !packages.Any())
            {
                throw new ArgumentException(nameof(packages));
            }

            var registration = packages.First().PackageRegistration;
            if (packages.Select(p => p.PackageRegistrationKey).Distinct().Count() > 1)
            {
                throw new ArgumentException("All packages to update must have the same ID.", nameof(packages));
            }

            if (listed.HasValue)
            {
                await MarkPackagesListingInBulkAsync(registration, packages, listed.Value);
            }

            await UpdatePackagesInBulkAsync(packages);

            transaction.Commit();

            if (updateIndex)
            {
                // The indexing service will find the latest version of the package to index--it doesn't matter what package we pass in.
                _indexingService.UpdatePackage(packages.First());
            }
        }

        private async Task MarkPackagesListingInBulkAsync(PackageRegistration registration, IReadOnlyList<Package> packages, bool listed)
        {
            var shouldUpdateIsLatest = false;
            foreach (var package in packages)
            {
                if (package.IsLatest || package.IsLatestStable || package.IsLatestSemVer2 || package.IsLatestStableSemVer2)
                {
                    shouldUpdateIsLatest = true;
                }

                if (listed)
                {
                    await MarkPackageListedAsync(package, updateIndex: false, commitChanges: false);
                }
                else
                {
                    await MarkPackageUnlistedAsync(package, updateIndex: false, commitChanges: false);
                }
            }

            if (shouldUpdateIsLatest)
            {
                await _packageService.UpdateIsLatestAsync(registration, commitChanges: false);
            }

            await _entitiesContext.SaveChangesAsync();
        }

        private const string UpdateBulkPackagesQueryFormat = @"
UPDATE [dbo].Packages
SET LastEdited = GETUTCDATE(), LastUpdated = GETUTCDATE()
WHERE [Key] IN ({0})";

        /// <remarks>
        /// Normally we would use a large parameterized SQL query for this.
        /// Unfortunately, however, there is a maximum number of parameters for a SQL query (around 2,000-3,000).
        /// By writing a query containing the package keys directly we can remove this restriction.
        /// Furthermore, package keys are not user data, so there is no risk to writing a query in this way.
        /// </remarks>
        private async Task UpdatePackagesInBulkAsync(IReadOnlyList<Package> packages)
        {
            var query = string.Format(
                UpdateBulkPackagesQueryFormat,
                string.Join(
                    ", ", 
                    packages
                        .Select(p => p.Key)
                        .OrderBy(k => k)));

            var result = await _entitiesContext
                .GetDatabase()
                .ExecuteSqlCommandAsync(query);

            // The query updates each row twice--once for the initial commit and a second time due to the trigger on LastEdited.
            var expectedResult = packages.Count() * 2;
            if (result != expectedResult)
            {
                throw new InvalidOperationException(
                    $"Updated an unexpected number of packages when performing a bulk update! " +
                    $"Updated {result} packages instead of {expectedResult}.");
            }
        }
    }
}
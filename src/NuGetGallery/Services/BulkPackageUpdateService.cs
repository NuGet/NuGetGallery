// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class BulkPackageUpdateService : IBulkPackageUpdateService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;

        private const string BaseQueryFormat = @"
UPDATE [dbo].Packages
SET LastEdited = GETUTCDATE(), LastUpdated = GETUTCDATE()
WHERE [Key] IN ({0})";

        public BulkPackageUpdateService(
            IEntitiesContext entitiesContext,
            IPackageService packageService)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        }

        public async Task UpdatePackagesAsync(IEnumerable<Package> packages, bool? setListed = null)
        {
            if (packages == null || !packages.Any())
            {
                throw new ArgumentException(nameof(packages));
            }

            if (setListed.HasValue)
            {
                // Update listed state first to minimize the amount of time between setting LastEdited and committing the transaction.
                await UpdatePackagesListedAsync(packages, setListed.Value);
            }

            await UpdatePackagesTimestampsAsync(packages);
        }

        private async Task UpdatePackagesListedAsync(IEnumerable<Package> packages, bool setListed)
        {
            foreach (var packagesByRegistration in packages.GroupBy(p => p.PackageRegistration))
            {
                await UpdatePackagesListedByRegistrationAsync(packagesByRegistration, setListed);
            }

            await _entitiesContext.SaveChangesAsync();
        }

        private async Task UpdatePackagesListedByRegistrationAsync(IGrouping<PackageRegistration, Package> packagesByRegistration, bool setListed)
        {
            foreach (var package in packagesByRegistration)
            {
                package.Listed = setListed;
            }

            if (!packagesByRegistration.Any(p => p.IsLatest || p.IsLatestStable || p.IsLatestSemVer2 || p.IsLatestStableSemVer2))
            {
                // Don't need to update latest if we haven't affected any latest packages.
                return;
            }

            await _packageService.UpdateIsLatestAsync(packagesByRegistration.Key, false);
        }

        /// <remarks>
        /// Normally we would use a large parameterized SQL query for this.
        /// Unfortunately, however, there is a maximum number of parameters for a SQL query (around 2,000-3,000).
        /// By writing a query containing the package keys directly we can remove this restriction.
        /// Furthermore, package keys are not user data, so there is no risk to writing a query in this way.
        /// </remarks>
        private async Task UpdatePackagesTimestampsAsync(IEnumerable<Package> packages)
        {
            var query = string.Format(
                BaseQueryFormat,
                string.Join(", ", packages.Select(p => p.Key)));

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
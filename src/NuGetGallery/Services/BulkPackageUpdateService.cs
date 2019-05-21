// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class BulkPackageUpdateService : IBulkPackageUpdateService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly IIndexingService _indexingService;

        private const string BaseQueryFormat = @"
UPDATE [dbo].Packages
SET LastEdited = GETUTCDATE(), LastUpdated = GETUTCDATE()
WHERE [Key] IN ({0})";

        public BulkPackageUpdateService(
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IIndexingService indexingService)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _indexingService = indexingService ?? throw new ArgumentNullException(nameof(indexingService));
        }

        public async Task UpdatePackages(IEnumerable<Package> packages, bool? setListed = null)
        {
            var parameters = packages
                .Select(p => p.Key)
                .Select((k, index) => new SqlParameter("@package" + index.ToString(), SqlDbType.Int) { Value = k });

            var query = string.Format(
                BaseQueryFormat, 
                string.Join(", ", parameters.Select(p => p.ParameterName)));

            var result = await _entitiesContext
                .GetDatabase()
                .ExecuteSqlCommandAsync(query, parameters.Cast<object>().ToArray());

            // The query updates each row twice--once for the initial commit and a second time due to the trigger on LastEdited.
            var expectedResult = packages.Count() * 2;
            if (result != expectedResult)
            {
                throw new InvalidOperationException(
                    $"Updated an unexpected number of packages when performing a bulk update! " +
                    $"Updated {result} packages instead of {expectedResult}.");
            }

            if (setListed.HasValue)
            {
                foreach (var packagesByRegistration in packages.GroupBy(p => p.PackageRegistration))
                {
                    foreach (var package in packages)
                    {
                        package.Listed = setListed.Value;
                    }

                    if (packagesByRegistration.Any(
                        p => p.IsLatest || p.IsLatestStable || p.IsLatestSemVer2 || p.IsLatestStableSemVer2))
                    {
                        await _packageService.UpdateIsLatestAsync(packagesByRegistration.Key, false);
                    }
                }

                await _entitiesContext.SaveChangesAsync();
            }

            // Update the indexing of the packages we updated.
            foreach (var package in packages)
            {
                _indexingService.UpdatePackage(package);
            }
        }
    }
}
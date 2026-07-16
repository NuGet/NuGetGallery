// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class UpdateListedService : IUpdateListedService
    {
        private readonly IPackageService _packageService;
        private readonly IPackageUpdateService _packageUpdateService;

        public UpdateListedService(
            IPackageService packageService,
            IPackageUpdateService packageUpdateService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageUpdateService = packageUpdateService ?? throw new ArgumentNullException(nameof(packageUpdateService));
        }

        public async Task<IReadOnlyList<UpdateListedPackageResult>> UpdateListedAsync(
            IReadOnlyList<UpdateListedPackageIdentity> packages,
            bool listed,
            string reason = null,
            string callerIdentity = null)
        {
            if (packages is null)
            {
                throw new ArgumentNullException(nameof(packages));
            }

            var results = new List<UpdateListedPackageResult>();

            // Group by package ID so we can call UpdateListedInBulkAsync per registration
            var groups = packages
                .Select(p => new
                {
                    Id = p.Id.Trim(),
                    Version = NuGetVersionFormatter.Normalize(p.Version.Trim())
                })
                .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var normalizedVersions = group
                    .Select(p => p.Version)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                List<Package> foundPackages = [];
                if (normalizedVersions.Count == 1)
                {
                    var package = _packageService.FindPackageByIdAndVersionStrict(group.Key, normalizedVersions.First());
                    if (package != null)
                    {
                        foundPackages.Add(package);
                    }
                }
                else
                {
                    foundPackages = _packageService.FindPackagesById(group.Key, PackageDeprecationFieldsToInclude.DeprecationAndRelationships)
                        .Where(x => normalizedVersions.Contains(x.NormalizedVersion))
                        .ToList();
                }

                var eligible = foundPackages
                    .Where(x => x.PackageStatusKey != PackageStatus.Deleted)
                    .Where(x => x.PackageStatusKey != PackageStatus.FailedValidation)
                    .ToList();

                var eligibleVersions = eligible
                    .Select(x => x.NormalizedVersion)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Add results for each requested version
                foreach (var entry in group)
                {
                    results.Add(new UpdateListedPackageResult
                    {
                        Id = group.Key,
                        Version = entry.Version,
                        Result = eligibleVersions.Contains(entry.Version)
                            ? UpdateListedServiceResult.Success
                            : UpdateListedServiceResult.PackageNotFound
                    });
                }

                if (eligible.Count > 0)
                {
                    await _packageUpdateService.UpdateListedInBulkAsync(eligible, listed, reason, callerIdentity);
                }
            }

            return results;
        }
    }
}

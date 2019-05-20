// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PackageDeprecationService : IPackageDeprecationService
    {
        private readonly IEntityRepository<PackageDeprecation> _deprecationRepository;
        private readonly IPackageService _packageService;
        private readonly IIndexingService _indexingService;

        public PackageDeprecationService(
           IEntityRepository<PackageDeprecation> deprecationRepository,
           IPackageService packageService,
           IIndexingService indexingService)
        {
            _deprecationRepository = deprecationRepository ?? throw new ArgumentNullException(nameof(deprecationRepository));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _indexingService = indexingService ?? throw new ArgumentNullException(nameof(indexingService));
        }

        public async Task UpdateDeprecation(
           IReadOnlyCollection<Package> packages,
           PackageDeprecationStatus status,
           PackageRegistration alternatePackageRegistration,
           Package alternatePackage,
           string customMessage,
           bool shouldUnlist,
           User user)
        {
            if (packages == null || !packages.Any())
            {
                throw new ArgumentException(nameof(packages));
            }

            if (packages.Select(p => p.Id).Distinct().Count() > 1)
            {
                throw new ArgumentException("All packages to deprecate must have the same ID.", nameof(packages));
            }

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var shouldDelete = status == PackageDeprecationStatus.NotDeprecated;
            var deprecations = new List<PackageDeprecation>();
            foreach (var package in packages)
            {
                var deprecation = package.Deprecations.SingleOrDefault();
                if (shouldDelete)
                {
                    if (deprecation != null)
                    {
                        package.Deprecations.Remove(deprecation);
                        deprecations.Add(deprecation);
                    }
                }
                else
                {
                    if (deprecation == null)
                    {
                        deprecation = new PackageDeprecation
                        {
                            Package = package
                        };

                        package.Deprecations.Add(deprecation);
                        deprecations.Add(deprecation);
                    }

                    deprecation.Status = status;
                    deprecation.DeprecatedByUser = user;

                    deprecation.AlternatePackageRegistration = alternatePackageRegistration;
                    deprecation.AlternatePackage = alternatePackage;

                    deprecation.CustomMessage = customMessage;
                }

                package.LastUpdated = DateTime.UtcNow;
                package.LastEdited = DateTime.UtcNow;

                if (shouldUnlist)
                {
                    package.Listed = false;
                }
            }

            if (shouldDelete)
            {
                _deprecationRepository.DeleteOnCommit(deprecations);
            }
            else
            {
                _deprecationRepository.InsertOnCommit(deprecations);
            }

            if (shouldUnlist && packages.Any(p => p.IsLatest || p.IsLatestStable || p.IsLatestSemVer2 || p.IsLatestStableSemVer2))
            {
                await _packageService.UpdateIsLatestAsync(packages.First().PackageRegistration, false);
            }

            // Update the indexing of the packages we updated the deprecation information of.
            foreach (var package in packages)
            {
                _indexingService.UpdatePackage(package);
            }

            await _deprecationRepository.CommitChangesAsync();
        }

        public PackageDeprecation GetDeprecationByPackage(Package package)
        {
            return _deprecationRepository.GetAll()
                .Include(d => d.AlternatePackage.PackageRegistration)
                .Include(d => d.AlternatePackageRegistration)
                .SingleOrDefault(d => d.PackageKey == package.Key);
        }
    }
}
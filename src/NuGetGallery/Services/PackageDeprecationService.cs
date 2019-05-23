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
        private readonly IBulkPackageUpdateService _bulkPackageUpdateService;
        private readonly IIndexingService _indexingService;

        public PackageDeprecationService(
           IEntityRepository<PackageDeprecation> deprecationRepository,
           IBulkPackageUpdateService bulkPackageUpdateService,
           IIndexingService indexingService)
        {
            _deprecationRepository = deprecationRepository ?? throw new ArgumentNullException(nameof(deprecationRepository));
            _bulkPackageUpdateService = bulkPackageUpdateService ?? throw new ArgumentNullException(nameof(bulkPackageUpdateService));
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

            var registration = packages.First().PackageRegistration;
            if (packages.Select(p => p.PackageRegistrationKey).Distinct().Count() > 1)
            {
                throw new ArgumentException("All packages to deprecate must have the same ID.", nameof(packages));
            }

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            using (var strategy = new SuspendDbExecutionStrategy())
            using (var transaction = _deprecationRepository.GetDatabase().BeginTransaction())
            {
                var shouldDelete = status == PackageDeprecationStatus.NotDeprecated;
                var deprecations = new List<PackageDeprecation>();
                var deprecationTime = DateTime.UtcNow;
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
                }

                if (shouldDelete)
                {
                    _deprecationRepository.DeleteOnCommit(deprecations);
                }
                else
                {
                    _deprecationRepository.InsertOnCommit(deprecations);
                }

                // Save deprecation changes before bulk updating the packages.
                await _deprecationRepository.CommitChangesAsync();

                await _bulkPackageUpdateService.UpdatePackagesAsync(packages, shouldUnlist ? false : (bool?)null);
                transaction.Commit();
            }

            _indexingService.UpdatePackageRegistration(registration);
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
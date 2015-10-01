// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class PackageDeleteService
        : IPackageDeleteService
    {
        private readonly IEntityRepository<PackageRegistration> _packageRegistrationRepository;
        private readonly IEntityRepository<Package> _packageRepository;
        private readonly IEntityRepository<PackageDelete> _packageDeletesRepository;
        private readonly IPackageService _packageService;
        private readonly IIndexingService _indexingService;

        public PackageDeleteService(
            IEntityRepository<PackageRegistration> packageRegistrationRepository,
            IEntityRepository<Package> packageRepository,
            IEntityRepository<PackageDelete> packageDeletesRepository,
            IPackageService packageService,
            IIndexingService indexingService)
        {
            _packageRegistrationRepository = packageRegistrationRepository;
            _packageRepository = packageRepository;
            _packageDeletesRepository = packageDeletesRepository;
            _packageService = packageService;
            _indexingService = indexingService;
        }

        public Task DeletePackagesAsync(IEnumerable<Package> packages, User deletedBy, string reason, string signature)
        {
            // Store the delete in the database
            var packageDelete = new PackageDelete
            {
                DeletedOn = DateTime.UtcNow,
                DeletedBy = deletedBy,
                Reason = reason,
                Signature = signature
            };

            foreach (var package in packages)
            {
                package.Deleted = true;
                packageDelete.Packages.Add(package);
            }

            _packageDeletesRepository.InsertOnCommit(packageDelete);

            // Update latest versions
            foreach (var packageRegistration in packages.GroupBy(p => p.PackageRegistration)
                .Select(g => g.First().PackageRegistration))
            {
                _packageService.UpdateIsLatest(packageRegistration, false);
            }

            // Commit changes
            _packageRepository.CommitChanges();
            _packageDeletesRepository.CommitChanges();

            // Force refresh the index
            _indexingService.UpdateIndex(true);

            return Task.FromResult(0);
        }
    }
}
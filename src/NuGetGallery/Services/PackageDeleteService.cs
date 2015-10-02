// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet;

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
        private readonly IPackageFileService _packageFileService;

        public PackageDeleteService(
            IEntityRepository<PackageRegistration> packageRegistrationRepository,
            IEntityRepository<Package> packageRepository,
            IEntityRepository<PackageDelete> packageDeletesRepository,
            IPackageService packageService,
            IIndexingService indexingService,
            IPackageFileService packageFileService)
        {
            _packageRegistrationRepository = packageRegistrationRepository;
            _packageRepository = packageRepository;
            _packageDeletesRepository = packageDeletesRepository;
            _packageService = packageService;
            _indexingService = indexingService;
            _packageFileService = packageFileService;
        }

        public async Task DeletePackagesAsync(IEnumerable<Package> packages, User deletedBy, string reason, string signature)
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

            // Backup the package binary and remove it from main storage
            foreach (var package in packages)
            {
                using (var packageStream = await _packageFileService.DownloadPackageFileAsync(package))
                {
                    await _packageFileService.StorePackageFileInBackupLocationAsync(package, packageStream);
                }
                await _packageFileService.DeletePackageFileAsync(package.PackageRegistration.Id, string.IsNullOrEmpty(package.NormalizedVersion) 
                    ? SemanticVersion.Parse(package.Version).ToNormalizedString() : package.NormalizedVersion);
            }

            // Commit changes
            _packageRepository.CommitChanges();
            _packageDeletesRepository.CommitChanges();

            // Force refresh the index
            _indexingService.UpdateIndex(true);
        }
    }
}
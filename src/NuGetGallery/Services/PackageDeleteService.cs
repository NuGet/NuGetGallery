// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;
using NuGetGallery.Auditing;

namespace NuGetGallery
{
    public class PackageDeleteService
        : IPackageDeleteService
    {
        internal const string DeletePackageRegistrationQuery = @"
            IF NOT EXISTS (
                SELECT TOP 1 [Key]
                FROM Packages AS p
                WHERE p.[PackageRegistrationKey] = @key)
            BEGIN
                DELETE pro FROM PackageRegistrationOwners AS pro
                WHERE pro.[PackageRegistrationKey] = @key

                DELETE pr FROM PackageRegistrations AS pr
                WHERE pr.[Key] = @key
            END";

        private readonly IEntityRepository<Package> _packageRepository;
        private readonly IEntityRepository<PackageDelete> _packageDeletesRepository;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly IIndexingService _indexingService;
        private readonly IPackageFileService _packageFileService;
        private readonly IAuditingService _auditingService;

        public PackageDeleteService(
            IEntityRepository<Package> packageRepository,
            IEntityRepository<PackageDelete> packageDeletesRepository,
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IIndexingService indexingService,
            IPackageFileService packageFileService,
            IAuditingService auditingService)
        {
            _packageRepository = packageRepository;
            _packageDeletesRepository = packageDeletesRepository;
            _entitiesContext = entitiesContext;
            _packageService = packageService;
            _indexingService = indexingService;
            _packageFileService = packageFileService;
            _auditingService = auditingService;
        }

        public async Task SoftDeletePackagesAsync(IEnumerable<Package> packages, User deletedBy, string reason, string signature)
        {
            EntitiesConfiguration.SuspendExecutionStrategy = true;
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                // Increase command timeout
                _entitiesContext.SetCommandTimeout(seconds: 300);

                // Keep package registrations
                var packageRegistrations = packages
                    .GroupBy(p => p.PackageRegistration)
                    .Select(g => g.First().PackageRegistration)
                    .ToList();

                // Backup the package binaries and remove from main storage
                // We're doing this early in the process as we need the metadata to still exist in the DB.
                await BackupPackageBinaries(packages);

                // Store the soft delete in the database
                var packageDelete = new PackageDelete
                {
                    DeletedOn = DateTime.UtcNow,
                    DeletedBy = deletedBy,
                    Reason = reason,
                    Signature = signature
                };

                foreach (var package in packages)
                {
                    package.Listed = false;
                    package.Deleted = true;
                    packageDelete.Packages.Add(package);

                    await _auditingService.SaveAuditRecordAsync(CreateAuditRecord(package, package.PackageRegistration, AuditedPackageAction.SoftDelete, reason));
                }

                _packageDeletesRepository.InsertOnCommit(packageDelete);

                // Update latest versions
                await UpdateIsLatestAsync(packageRegistrations);

                // Commit changes
                await _packageRepository.CommitChangesAsync();
                await _packageDeletesRepository.CommitChangesAsync();
                transaction.Commit();
            }
            EntitiesConfiguration.SuspendExecutionStrategy = false;


            // Force refresh the index
            UpdateSearchIndex();
        }

        public async Task HardDeletePackagesAsync(IEnumerable<Package> packages, User deletedBy, string reason, string signature, bool deleteEmptyPackageRegistration)
        {
            EntitiesConfiguration.SuspendExecutionStrategy = true;
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                // Increase command timeout
                _entitiesContext.SetCommandTimeout(seconds: 300);

                // Keep package registrations
                var packageRegistrations = packages.GroupBy(p => p.PackageRegistration).Select(g => g.First().PackageRegistration).ToList();

                // Backup the package binaries and remove from main storage
                // We're doing this early in the process as we need the metadata to still exist in the DB.
                await BackupPackageBinaries(packages);

                // Remove the package and related entities from the database
                foreach (var package in packages)
                {
                    await ExecuteSqlCommandAsync(_entitiesContext.GetDatabase(),
                        "DELETE pa FROM PackageAuthors pa JOIN Packages p ON p.[Key] = pa.PackageKey WHERE p.[Key] = @key",
                        new SqlParameter("@key", package.Key));
                    await ExecuteSqlCommandAsync(_entitiesContext.GetDatabase(),
                        "DELETE pd FROM PackageDependencies pd JOIN Packages p ON p.[Key] = pd.PackageKey WHERE p.[Key] = @key",
                        new SqlParameter("@key", package.Key));
                    await ExecuteSqlCommandAsync(_entitiesContext.GetDatabase(),
                        "DELETE pf FROM PackageFrameworks pf JOIN Packages p ON p.[Key] = pf.Package_Key WHERE p.[Key] = @key",
                        new SqlParameter("@key", package.Key));

                    await _auditingService.SaveAuditRecordAsync(CreateAuditRecord(package, package.PackageRegistration, AuditedPackageAction.Delete, reason));

                    package.PackageRegistration.Packages.Remove(package);
                    _packageRepository.DeleteOnCommit(package);
                }

                // Update latest versions
                await UpdateIsLatestAsync(packageRegistrations);

                // Commit changes to package repository
                await _packageRepository.CommitChangesAsync();

                // Remove package registrations that have no more packages?
                if (deleteEmptyPackageRegistration)
                {
                    await RemovePackageRegistrationsWithoutPackages(packageRegistrations);
                }

                // Commit transaction
                transaction.Commit();
            }
            EntitiesConfiguration.SuspendExecutionStrategy = false;

            // Force refresh the index
            UpdateSearchIndex();
        }

        protected virtual async Task ExecuteSqlCommandAsync(Database database, string sql, params object[] parameters)
        {
            await database.ExecuteSqlCommandAsync(sql, parameters);
        }

        private async Task UpdateIsLatestAsync(IEnumerable<PackageRegistration> packageRegistrations)
        {
            // Update latest versions
            foreach (var packageRegistration in packageRegistrations)
            {
                await _packageService.UpdateIsLatestAsync(packageRegistration, commitChanges: false);
            }
        }

        private async Task RemovePackageRegistrationsWithoutPackages(IEnumerable<PackageRegistration> packageRegistrations)
        {
            // Remove package registrations that have no more packages
            // (making the identifier available again)
            foreach (var packageRegistration in packageRegistrations)
            {
                if (!packageRegistration.Packages.Any())
                {
                    // the query also checks if packages exist, we want to avoid
                    // the delete if someone else uploaded a new package in the meanwhile
                    await ExecuteSqlCommandAsync(_entitiesContext.GetDatabase(),
                        DeletePackageRegistrationQuery,
                        new SqlParameter("@key", packageRegistration.Key));
                }
            }
        }

        private async Task BackupPackageBinaries(IEnumerable<Package> packages)
        {
            // Backup the package binaries and remove from main storage
            foreach (var package in packages)
            {
                using (var packageStream = await _packageFileService.DownloadPackageFileAsync(package))
                {
                    if (packageStream != null)
                    {
                        await _packageFileService.StorePackageFileInBackupLocationAsync(package, packageStream);
                    }
                }
                await _packageFileService.DeletePackageFileAsync(package.PackageRegistration.Id,
                        string.IsNullOrEmpty(package.NormalizedVersion)
                            ? NuGetVersion.Parse(package.Version).ToNormalizedString()
                            : package.NormalizedVersion);
            }
        }

        private void UpdateSearchIndex()
        {
            // Force refresh the index
            _indexingService.UpdateIndex(true);
        }

        protected virtual PackageAuditRecord CreateAuditRecord(Package package, PackageRegistration packageRegistration, AuditedPackageAction action, string reason)
        {
            return new PackageAuditRecord(package, action, reason);
        }
    }
}
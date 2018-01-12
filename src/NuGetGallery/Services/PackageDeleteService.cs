// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGet.Versioning;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;

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
                DELETE por FROM PackageOwnerRequests As por
                WHERE por.[PackageRegistrationKey] = @key                

                DELETE pro FROM PackageRegistrationOwners AS pro
                WHERE pro.[PackageRegistrationKey] = @key

                DELETE pr FROM PackageRegistrations AS pr
                WHERE pr.[Key] = @key
            END";

        private readonly IEntityRepository<Package> _packageRepository;
        private readonly IEntityRepository<PackageRegistration> _packageRegistrationRepository;
        private readonly IEntityRepository<PackageDelete> _packageDeletesRepository;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly IIndexingService _indexingService;
        private readonly IPackageFileService _packageFileService;
        private readonly IAuditingService _auditingService;
        private readonly IPackageDeleteConfiguration _config;

        public PackageDeleteService(
            IEntityRepository<Package> packageRepository,
            IEntityRepository<PackageRegistration> packageRegistrationRepository,
            IEntityRepository<PackageDelete> packageDeletesRepository,
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IIndexingService indexingService,
            IPackageFileService packageFileService,
            IAuditingService auditingService,
            IPackageDeleteConfiguration config)
        {
            _packageRepository = packageRepository;
            _packageRegistrationRepository = packageRegistrationRepository;
            _packageDeletesRepository = packageDeletesRepository;
            _entitiesContext = entitiesContext;
            _packageService = packageService;
            _indexingService = indexingService;
            _packageFileService = packageFileService;
            _auditingService = auditingService;
            _config = config;

            if (config.HourLimitWithMaximumDownloads.HasValue
                && config.StatisticsUpdateFrequencyInHours.HasValue
                && config.HourLimitWithMaximumDownloads.Value <= config.StatisticsUpdateFrequencyInHours.Value)
            {
                throw new ArgumentException($"{nameof(_config.StatisticsUpdateFrequencyInHours)} must be less than " +
                    $"{nameof(_config.HourLimitWithMaximumDownloads)}.",
                    nameof(config));
            }
        }

        public Task<bool> CanPackageBeDeletedByUserAsync(Package package)
        {
            if (package.PackageStatusKey == PackageStatus.Deleted)
            {
                return Task.FromResult(false);
            }

            // For now, don't allow user's to delete their packages.
            // https://github.com/NuGet/Engineering/issues/921
            return Task.FromResult(false);
        }

        public async Task SoftDeletePackagesAsync(IEnumerable<Package> packages, User deletedBy, string reason, string signature)
        {
            using (var strategy = new SuspendDbExecutionStrategy())
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
                    /// We do not call <see cref="IPackageService.MarkPackageUnlistedAsync(Package, bool)"/> here
                    /// because that writes an audit entry. Additionally, the latest bits are already updated by
                    /// the package status change.
                    package.Listed = false;

                    await _packageService.UpdatePackageStatusAsync(
                        package,
                        PackageStatus.Deleted,
                        commitChanges: false);

                    packageDelete.Packages.Add(package);

                    await _auditingService.SaveAuditRecordAsync(CreateAuditRecord(package, package.PackageRegistration, AuditedPackageAction.SoftDelete, reason));
                }

                _packageDeletesRepository.InsertOnCommit(packageDelete);

                // Commit changes
                await _packageRepository.CommitChangesAsync();
                await _packageDeletesRepository.CommitChangesAsync();
                transaction.Commit();
            }

            // Force refresh the index
            UpdateSearchIndex();
        }

        public async Task HardDeletePackagesAsync(IEnumerable<Package> packages, User deletedBy, string reason, string signature, bool deleteEmptyPackageRegistration)
        {
            using (var strategy = new SuspendDbExecutionStrategy())
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

            // Force refresh the index
            UpdateSearchIndex();
        }

        public Task ReflowHardDeletedPackageAsync(string id, string version)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new UserSafeException("Must supply an ID for the hard-deleted package to reflow.");
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new UserSafeException("Must supply a version for the hard-deleted package to reflow.");
            }

            var normalizedId = id.ToLowerInvariant();
            if (!NuGetVersion.TryParse(version, out var normalizedVersion))
            {
                throw new UserSafeException($"{version} is not a valid version string!");
            }

            var normalizedVersionString = normalizedVersion.ToNormalizedString();

            var existingPackageRegistration = _packageRegistrationRepository.GetAll()
                .SingleOrDefault(p => p.Id == normalizedId);

            if (existingPackageRegistration != null)
            {
                var existingPackage = _packageRepository.GetAll()
                    .Where(p => p.PackageRegistrationKey == existingPackageRegistration.Key)
                    .SingleOrDefault(p => p.NormalizedVersion == normalizedVersionString);

                if (existingPackage != null)
                {
                    throw new UserSafeException($"The package {id} {normalizedVersion} exists! You can only reflow hard-deleted packages that do not exist.");
                }
            }

            var auditRecord = new PackageAuditRecord(
                normalizedId,
                normalizedVersionString,
                hash: string.Empty,
                packageRecord: null,
                registrationRecord: null,
                action: AuditedPackageAction.Delete,
                reason: "reflow hard-deleted package");
            return _auditingService.SaveAuditRecordAsync(auditRecord);
        }

        protected virtual async Task ExecuteSqlCommandAsync(IDatabase database, string sql, params object[] parameters)
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
                // Backup the package from the "validating" container.
                using (var packageStream = await _packageFileService.DownloadValidationPackageFileAsync(package))
                {
                    if (packageStream != null)
                    {
                        await _packageFileService.StorePackageFileInBackupLocationAsync(package, packageStream);
                    }
                }

                // Backup the package from the "packages" container.
                using (var packageStream = await _packageFileService.DownloadPackageFileAsync(package))
                {
                    if (packageStream != null)
                    {
                        await _packageFileService.StorePackageFileInBackupLocationAsync(package, packageStream);
                    }
                }

                var id = package.PackageRegistration.Id;
                var version = string.IsNullOrEmpty(package.NormalizedVersion)
                            ? NuGetVersion.Parse(package.Version).ToNormalizedString()
                            : package.NormalizedVersion;

                await _packageFileService.DeletePackageFileAsync(id, version);
                await _packageFileService.DeleteValidationPackageFileAsync(id, version);

                // Delete readme file for this package.
                await TryDeleteReadMeMdFile(package);
            }
        }

        /// <summary>
        /// Delete package readme.md file, if it exists.
        /// </summary>
        private async Task TryDeleteReadMeMdFile(Package package)
        {
            try
            {
                await _packageFileService.DeleteReadMeMdFileAsync(package);
            }
            catch (StorageException) { }
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

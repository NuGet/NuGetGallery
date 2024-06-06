// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
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

                UPDATE PackageDeprecations
                SET AlternatePackageRegistrationKey = NULL
                WHERE AlternatePackageRegistrationKey = @key

                DELETE r FROM PackageRenames AS r
                WHERE r.[ToPackageRegistrationKey] = @key

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
        private readonly IStatisticsService _statisticsService;
        private readonly ITelemetryService _telemetryService;
        private readonly ISymbolPackageFileService _symbolPackageFileService;
        private readonly ISymbolPackageService _symbolPackageService;
        private readonly IEntityRepository<SymbolPackage> _symbolPackageRepository;
        private readonly ICoreLicenseFileService _coreLicenseFileService;
        private readonly ICoreReadmeFileService _coreReadmeFileService;

        public PackageDeleteService(
            IEntityRepository<Package> packageRepository,
            IEntityRepository<PackageRegistration> packageRegistrationRepository,
            IEntityRepository<PackageDelete> packageDeletesRepository,
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IIndexingService indexingService,
            IPackageFileService packageFileService,
            IAuditingService auditingService,
            IPackageDeleteConfiguration config,
            IStatisticsService statisticsService,
            ITelemetryService telemetryService,
            ISymbolPackageFileService symbolPackageFileService,
            ISymbolPackageService symbolPackageService,
            IEntityRepository<SymbolPackage> symbolPackageRepository,
            ICoreLicenseFileService coreLicenseFileService,
            ICoreReadmeFileService coreReadmeFileService)
        {
            _packageRepository = packageRepository ?? throw new ArgumentNullException(nameof(packageRepository));
            _packageRegistrationRepository = packageRegistrationRepository ?? throw new ArgumentNullException(nameof(packageRegistrationRepository));
            _packageDeletesRepository = packageDeletesRepository ?? throw new ArgumentNullException(nameof(packageDeletesRepository));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _indexingService = indexingService ?? throw new ArgumentNullException(nameof(indexingService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _statisticsService = statisticsService ?? throw new ArgumentNullException(nameof(statisticsService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _symbolPackageFileService = symbolPackageFileService ?? throw new ArgumentNullException(nameof(symbolPackageFileService));
            _symbolPackageService = symbolPackageService ?? throw new ArgumentNullException(nameof(symbolPackageService));
            _symbolPackageRepository = symbolPackageRepository ?? throw new ArgumentNullException(nameof(symbolPackageRepository));
            _coreLicenseFileService = coreLicenseFileService ?? throw new ArgumentNullException(nameof(coreLicenseFileService));
            _coreReadmeFileService = coreReadmeFileService ?? throw new ArgumentNullException(nameof(coreReadmeFileService));

            if (config.HourLimitWithMaximumDownloads.HasValue
                && config.StatisticsUpdateFrequencyInHours.HasValue
                && config.HourLimitWithMaximumDownloads.Value <= config.StatisticsUpdateFrequencyInHours.Value)
            {
                throw new ArgumentException($"{nameof(_config.StatisticsUpdateFrequencyInHours)} must be less than " +
                    $"{nameof(_config.HourLimitWithMaximumDownloads)}.",
                    nameof(config));
            }
        }

        public async Task<bool> CanPackageBeDeletedByUserAsync(
            Package package,
            ReportPackageReason? reportPackageReason,
            PackageDeleteDecision? packageDeleteDecision)
        {
            if (!_config.AllowUsersToDeletePackages)
            {
                return false;
            }

            var details = await GetUserPackageDeleteEvent(package, reportPackageReason, packageDeleteDecision);

            if (package.PackageStatusKey == PackageStatus.Deleted)
            {
                return IsAccepted(details, UserPackageDeleteOutcome.AlreadyDeleted);
            }
            else if (package.PackageRegistration.IsLocked)
            {
                return IsAccepted(details, UserPackageDeleteOutcome.LockedRegistration);
            }
            // Handle the "early" delete case, where the package version download count is not considered but total
            // download count on the entire ID (all versions) is considered.
            else if (_config.StatisticsUpdateFrequencyInHours.HasValue
                && details.SinceCreated < TimeSpan.FromHours(_config.StatisticsUpdateFrequencyInHours.Value))
            {
                if (_config.MaximumDownloadsForPackageId.HasValue)
                {
                    // Do not allow a delete of a package version if the package registration record has too many downloads.
                    if (details.IdDatabaseDownloads > _config.MaximumDownloadsForPackageId.Value)
                    {
                        return IsAccepted(details, UserPackageDeleteOutcome.TooManyIdDatabaseDownloads);
                    }

                    // Do not allow a delete of a package version if the package ID report has too many downloads.
                    if (details.IdReportDownloads > _config.MaximumDownloadsForPackageId.Value)
                    {
                        return IsAccepted(details, UserPackageDeleteOutcome.TooManyIdReportDownloads);
                    }
                }

                return IsAccepted(details, UserPackageDeleteOutcome.Accepted);
            }
            // Handle the "late" delete case, where package version download count is considered.
            else if (_config.HourLimitWithMaximumDownloads.HasValue
                && details.SinceCreated < TimeSpan.FromHours(_config.HourLimitWithMaximumDownloads.Value))
            {
                if (_config.MaximumDownloadsForPackageVersion.HasValue)
                {
                    // Do not allow the delete if the statistics are stale.
                    if (await AreStatisticsStaleAsync())
                    {
                        return IsAccepted(details, UserPackageDeleteOutcome.StaleStatistics);
                    }

                    // Do not allow a delete of a package version if the package record has too many downloads.
                    if (details.VersionDatabaseDownloads > _config.MaximumDownloadsForPackageVersion.Value)
                    {
                        return IsAccepted(details, UserPackageDeleteOutcome.TooManyVersionDatabaseDownloads);
                    }

                    // Do not allow a delete of a package version if the package report has too many downloads.
                    if (details.VersionReportDownloads > _config.MaximumDownloadsForPackageVersion.Value)
                    {
                        return IsAccepted(details, UserPackageDeleteOutcome.TooManyVersionReportDownloads);
                    }
                }

                return IsAccepted(details, UserPackageDeleteOutcome.Accepted);
            }
            // If no time ranges are configured, allow downloads any time.
            else if (_config.HourLimitWithMaximumDownloads.HasValue
                || _config.StatisticsUpdateFrequencyInHours.HasValue)
            {
                return IsAccepted(details,  UserPackageDeleteOutcome.TooLate);
            }
            
            return IsAccepted(details, UserPackageDeleteOutcome.Accepted);
        }
        
        private bool IsAccepted(UserPackageDeleteEvent details, UserPackageDeleteOutcome outcome)
        {
            // Only report telemetry if a reason has been specified. 
            if (details.ReportPackageReason.HasValue)
            {
                _telemetryService.TrackUserPackageDeleteChecked(details, outcome);
            }

            return outcome == UserPackageDeleteOutcome.Accepted;
        }

        private async Task<UserPackageDeleteEvent> GetUserPackageDeleteEvent(
            Package package,
            ReportPackageReason? reportPackageReason,
            PackageDeleteDecision? packageDeleteDecision)
        {
            var sinceCreated = DateTime.UtcNow - package.Created;

            var report = await _statisticsService.GetPackageDownloadsByVersion(package.PackageRegistration.Id);

            var idReportDownloads = report?
                .Facts
                .Sum(x => x.Amount) ?? 0;

            var versionReportDownloads = report?
                .Facts
                .Where(x => HasVersion(x, package.NormalizedVersion))
                .Sum(x => x.Amount) ?? 0;

            var details = new UserPackageDeleteEvent(
                sinceCreated,
                package.Key,
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                package.PackageRegistration.DownloadCount,
                idReportDownloads,
                package.DownloadCount,
                versionReportDownloads,
                reportPackageReason,
                packageDeleteDecision);

            return details;
        }

        private bool HasVersion(StatisticsFact fact, string version)
        {
            return fact != null
                && fact.Dimensions.TryGetValue("Version", out string actualVersion)
                && StringComparer.OrdinalIgnoreCase.Equals(version, actualVersion);
        }

        private async Task<bool> AreStatisticsStaleAsync()
        {
            await _statisticsService.Refresh();

            var lastUpdated = _statisticsService.LastUpdatedUtc;
            if (!lastUpdated.HasValue)
            {
                return true;
            }

            var sinceUpdated = DateTime.UtcNow - lastUpdated.Value;
            if (sinceUpdated > TimeSpan.FromHours(_config.StatisticsUpdateFrequencyInHours.Value))
            {
                return true;
            }

            return false;
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

                    UnlinkPackageDeprecations(package);

                    // Mark all associated symbol packages for deletion.
                    foreach (var symbolPackage in package.SymbolPackages)
                    {
                        await _symbolPackageService.UpdateStatusAsync(
                            symbolPackage,
                            PackageStatus.Deleted,
                            commitChanges: false);
                    }

                    packageDelete.Packages.Add(package);

                    await _auditingService.SaveAuditRecordAsync(CreateAuditRecord(package, package.PackageRegistration, AuditedPackageAction.SoftDelete, reason));

                    _telemetryService.TrackPackageDelete(package, isHardDelete: false);
                }

                _packageDeletesRepository.InsertOnCommit(packageDelete);

                // Commit changes
                await _packageRepository.CommitChangesAsync();
                await _symbolPackageRepository.CommitChangesAsync();
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
                    UnlinkPackageDeprecations(package);

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

                    _telemetryService.TrackPackageDelete(package, isHardDelete: true);

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

        public async Task ReflowHardDeletedPackageAsync(string id, string version)
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
                deprecationRecord: null,
                action: AuditedPackageAction.Delete,
                reason: "reflow hard-deleted package");

            await _auditingService.SaveAuditRecordAsync(auditRecord);

            _telemetryService.TrackPackageHardDeleteReflow(normalizedId, normalizedVersionString);
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
                // Backup the package and symbols package from the "validating" container.
                await BackupFromValidationsContainerAsync(_packageFileService, package);
                await BackupFromValidationsContainerAsync(_symbolPackageFileService, package);

                // Backup the package and symbols package from the "packages"/"symbol-packages" containers, respectively.
                await BackupFromPackagesContainerAsync(_packageFileService, package);
                await BackupFromPackagesContainerAsync(_symbolPackageFileService, package);

                var id = package.PackageRegistration.Id;
                var version = string.IsNullOrEmpty(package.NormalizedVersion)
                            ? NuGetVersion.Parse(package.Version).ToNormalizedString()
                            : package.NormalizedVersion;

                await _packageFileService.DeletePackageFileAsync(id, version);
                // we didn't backup license file before deleting it because it is backed up as part of the package
                await _coreLicenseFileService.DeleteLicenseFileAsync(id, version);
                await _symbolPackageFileService.DeletePackageFileAsync(id, version);

                await _packageFileService.DeleteValidationPackageFileAsync(id, version);
                await _symbolPackageFileService.DeleteValidationPackageFileAsync(id, version);

                // Delete readme file for this package.
                await TryDeleteReadMeMdFile(package);
            }
        }

        private async Task BackupFromValidationsContainerAsync(ICorePackageFileService fileService, Package package)
        {
            using (var packageStream = await fileService.DownloadValidationPackageFileAsync(package))
            {
                if (packageStream != null)
                {
                    await fileService.StorePackageFileInBackupLocationAsync(package, packageStream);
                }
            }
        }

        private async Task BackupFromPackagesContainerAsync(ICorePackageFileService fileService, Package package)
        {
            using (var packageStream = await fileService.DownloadPackageFileAsync(package))
            {
                if (packageStream != null)
                {
                    await fileService.StorePackageFileInBackupLocationAsync(package, packageStream);
                }
            }
        }

        /// <summary>
        /// Delete package readme.md file, if it exists.
        /// </summary>
        private async Task TryDeleteReadMeMdFile(Package package)
        {
            try
            {
                if (package.HasEmbeddedReadme)
                {
                    await _coreReadmeFileService.DeleteReadmeFileAsync(package.Id, package.Version);
                }
                else
                {
                    await _packageFileService.DeleteReadMeMdFileAsync(package);
                }
            }
            catch (CloudBlobStorageException) { }
        }

        private void UnlinkPackageDeprecations(Package package)
        {
            foreach (var deprecation in package.AlternativeOf.ToList())
            {
                package.AlternativeOf.Remove(deprecation);
                deprecation.AlternatePackage = null;
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

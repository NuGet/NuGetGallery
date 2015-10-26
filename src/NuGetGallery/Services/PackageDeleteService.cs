// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet;
using NuGetGallery.Auditing;

namespace NuGetGallery
{
    public class PackageDeleteService
        : IPackageDeleteService
    {
        private readonly IEntityRepository<Package> _packageRepository;
        private readonly IEntityRepository<PackageDelete> _packageDeletesRepository;
        private readonly DbContext _dbContext;
        private readonly IPackageService _packageService;
        private readonly IIndexingService _indexingService;
        private readonly IPackageFileService _packageFileService;
        private readonly AuditingService _auditingService;

        public PackageDeleteService(
            IEntityRepository<Package> packageRepository,
            IEntityRepository<PackageDelete> packageDeletesRepository,
            DbContext dbContext,
            IPackageService packageService,
            IIndexingService indexingService,
            IPackageFileService packageFileService,
            AuditingService auditingService)
        {
            _packageRepository = packageRepository;
            _packageDeletesRepository = packageDeletesRepository;
            _dbContext = dbContext;
            _packageService = packageService;
            _indexingService = indexingService;
            _packageFileService = packageFileService;
            _auditingService = auditingService;
        }

        public async Task SoftDeletePackagesAsync(IEnumerable<Package> packages, User deletedBy, string reason, string signature)
        {
            EntitiesConfiguration.SuspendExecutionStrategy = true;
            using (var transaction = _dbContext.Database.BeginTransaction())
            {
                // Keep package registrations
                var packageRegistrations = packages.GroupBy(p => p.PackageRegistration).Select(g => g.First().PackageRegistration).ToList();

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

                    await _auditingService.SaveAuditRecord(CreateAuditRecord(package, package.PackageRegistration, PackageAuditAction.SoftDeleted, reason));
                }

                _packageDeletesRepository.InsertOnCommit(packageDelete);

                // Update latest versions
                UpdateIsLatest(packageRegistrations);

                // Commit changes
                _packageRepository.CommitChanges();
                _packageDeletesRepository.CommitChanges();
                transaction.Commit();
            }
            EntitiesConfiguration.SuspendExecutionStrategy = false;


            // Force refresh the index
            UpdateSearchIndex();
        }

        public async Task HardDeletePackagesAsync(IEnumerable<Package> packages, User deletedBy, string reason, string signature)
        {
            EntitiesConfiguration.SuspendExecutionStrategy = true;
            using (var transaction = _dbContext.Database.BeginTransaction())
            {
                // Keep package registrations
                var packageRegistrations = packages.GroupBy(p => p.PackageRegistration).Select(g => g.First().PackageRegistration).ToList();

                // Backup the package binaries and remove from main storage
                // We're doing this early in the process as we need the metadata to still exist in the DB.
                await BackupPackageBinaries(packages);

                // Remove the package and related entities from the database
                foreach (var package in packages)
                {
                    await ExecuteSqlCommandAsync(_dbContext.Database,
                        "DELETE pa FROM PackageAuthors pa JOIN Packages p ON p.[Key] = pa.PackageKey WHERE p.[Key] = @key",
                        new SqlParameter("@key", package.Key));
                    await ExecuteSqlCommandAsync(_dbContext.Database,
                        "DELETE pd FROM PackageDependencies pd JOIN Packages p ON p.[Key] = pd.PackageKey WHERE p.[Key] = @key",
                        new SqlParameter("@key", package.Key));
                    await ExecuteSqlCommandAsync(_dbContext.Database,
                        "DELETE ps FROM PackageStatistics ps JOIN Packages p ON p.[Key] = ps.PackageKey WHERE p.[Key] = @key",
                        new SqlParameter("@key", package.Key));
                    await ExecuteSqlCommandAsync(_dbContext.Database,
                        "DELETE pf FROM PackageFrameworks pf JOIN Packages p ON p.[Key] = pf.Package_Key WHERE p.[Key] = @key",
                        new SqlParameter("@key", package.Key));

                    await _auditingService.SaveAuditRecord(CreateAuditRecord(package, package.PackageRegistration, PackageAuditAction.Deleted, reason));
                    _packageRepository.DeleteOnCommit(package);
                }
                
                // Update latest versions
                UpdateIsLatest(packageRegistrations);

                // Commit changes
                _packageRepository.CommitChanges();
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
        
        private void UpdateIsLatest(IEnumerable<PackageRegistration> packageRegistrations)
        {
            // Update latest versions
            foreach (var packageRegistration in packageRegistrations)
            {
                _packageService.UpdateIsLatest(packageRegistration, false);
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
                            ? SemanticVersion.Parse(package.Version).ToNormalizedString()
                            : package.NormalizedVersion);
            }
        }

        private void UpdateSearchIndex()
        {
            // Force refresh the index
            _indexingService.UpdateIndex(true);
        }

        protected virtual PackageAuditRecord CreateAuditRecord(Package package, PackageRegistration packageRegistration, PackageAuditAction action, string reason)
        {
            return new PackageAuditRecord(package, ConvertToDataTable(package), ConvertToDataTable(packageRegistration), action, reason);
        }

        public static DataTable ConvertToDataTable<T>(T instance)
        {
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(T));
            DataTable table = new DataTable() { Locale = CultureInfo.CurrentCulture };

            List<object> values = new List<object>();
            for (int i = 0; i < properties.Count; i++)
            {
                var propertyDescriptor = properties[i];
                var propertyType = Nullable.GetUnderlyingType(propertyDescriptor.PropertyType) ?? propertyDescriptor.PropertyType;
                if (!IsComplexType(propertyType))
                {
                    table.Columns.Add(propertyDescriptor.Name, propertyType);
                    values.Add(propertyDescriptor.GetValue(instance) ?? DBNull.Value);
                }
            }

            table.Rows.Add(values.ToArray());

            return table;
        }

        public static bool IsComplexType(Type type)
        {
            if (type.IsSubclassOf(typeof (ValueType)) || type == typeof (string))
            {
                return false;
            }
            return true;
        }
    }
}
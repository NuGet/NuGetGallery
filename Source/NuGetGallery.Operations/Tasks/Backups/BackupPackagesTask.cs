using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Operations.Tasks;

namespace NuGetGallery.Operations
{
    [Command("backuppackages", "Back up all packages at the source storage server", AltName = "bps", MaxArgs = 0)]
    public class BackupPackagesTask : DatabaseAndStorageTask
    {
        public CloudStorageAccount BackupStorage { get; private set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (BackupStorage == null)
            {
                BackupStorage = StorageAccount;
                if (CurrentEnvironment != null)
                {
                    BackupStorage = CurrentEnvironment.BackupStorage;
                }
            }
        }

        public override void ExecuteCommand()
        {
            Log.Trace("Getting list of packages to back up...");
            var packagesToBackUp = GetPackagesToBackUp();
            
            var processedCount = 0;
            Log.Trace(
                    "Backing up '{0}/packages' -> '{2}/packagebackups'.",
                    StorageAccount.Credentials.AccountName,
                    BackupStorage.Credentials.AccountName);

            var client = CreateBlobClient();
            var backupClient = BackupStorage.CreateCloudBlobClient();

            var backupBlobs = backupClient.GetContainerReference("package-backups");
            var packageBlobs = client.GetContainerReference("packages");
            if (!WhatIf)
            {
                backupBlobs.CreateIfNotExists();
            }
            Parallel.ForEach(packagesToBackUp, new ParallelOptions { MaxDegreeOfParallelism = 10 }, package =>
            {
                try
                {
                    var packageBlob = packageBlobs.GetBlockBlobReference(Util.GetPackageFileName(package.Id, package.Version));
                    var backupBlob = backupBlobs.GetBlockBlobReference(Util.GetPackageBackupFileName(package.Id, package.Version, package.Hash));
                    bool exists = backupBlob.Exists();
                    if (!exists && !WhatIf)
                    {
                        backupBlob.StartCopyFromBlob(packageBlob);
                    }

                    Interlocked.Increment(ref processedCount);
                    Log.Info(
                        "[{2:000000}/{3:000000} {4:00.0}%] {5} Backup of '{0}@{1}'.",
                        package.Id,
                        package.Version,
                        processedCount,
                        packagesToBackUp.Count,
                        (double)processedCount / (double)packagesToBackUp.Count,
                        exists ? "Skipped" : "Started");

                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref processedCount);
                    Log.Error(
                        "[{2:000000}/{3:000000} {4:00.0}%] Error Starting Backup of '{0}@{1}': {5}",
                        package.Id,
                        package.Version,
                        processedCount,
                        packagesToBackUp.Count,
                        (double)processedCount / (double)packagesToBackUp.Count,
                        ex.Message);
                }
            });
        }

        IList<Package> GetPackagesToBackUp()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                return dbExecutor.Query<Package>(@"
                    SELECT pr.Id, p.Version, p.Hash 
                    FROM Packages p 
                        JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey 
                    WHERE p.ExternalPackageUrl IS NULL
                    ORDER BY Id, Version, Hash").ToList();

            }
        }

        void UploadPackageBackup(
            string id,
            string version,
            string hash,
            string downloadPath)
        {
            var cloudClient = CreateBlobClient();

            var packageBackupsContainer = Util.GetPackageBackupsBlobContainer(cloudClient);

            var backupFileName = Util.GetPackageBackupFileName(
                id,
                version,
                hash);

            var backupPackageBlob = packageBackupsContainer.GetBlockBlobReference(backupFileName);
            
            backupPackageBlob.UploadFile(downloadPath);
            backupPackageBlob.Properties.ContentType = "application/zip";
            backupPackageBlob.SetProperties();
        }
    }
}

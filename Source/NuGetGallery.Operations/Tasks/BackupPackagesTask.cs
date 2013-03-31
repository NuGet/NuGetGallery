using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;

namespace NuGetGallery.Operations
{
    [Command("backuppackages", "Back up all packages at the source storage server", AltName = "bps", MaxArgs = 0)]
    public class BackupPackagesTask : DatabaseAndStorageTask
    {
        private readonly string _tempFolder;

        public BackupPackagesTask()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "NuGetGalleryOps");
            Directory.CreateDirectory(_tempFolder);
        }

        public override void ExecuteCommand()
        {
            Log.Trace("Getting list of packages to back up...");
            var packagesToBackUp = GetPackagesToBackUp();
            Log.Trace("Getting list of already backed up packages...");
            var packageBackupBlobFileNames = GetPackageBackupBlobFileNames();

            Log.Trace("Determining minimal sync set...");
            var packageFileNamesToRestore = packagesToBackUp.Keys.Except(packageBackupBlobFileNames).ToList();

            var totalCount = packageFileNamesToRestore.Count;
            var processedCount = 0;
            Log.Trace(
                    "Backing up {0} packages on storage account '{1}'.",
                    totalCount,
                    StorageAccountName);

            Parallel.ForEach(packageFileNamesToRestore, new ParallelOptions { MaxDegreeOfParallelism = 10 }, packageFileNameToRestore =>
            {
                var package = packagesToBackUp[packageFileNameToRestore];

                try
                {
                    if (!WhatIf)
                    {
                        var downloadPath = DownloadPackage(package);

                        UploadPackageBackup(
                            package.Id,
                            package.Version,
                            package.Hash,
                            downloadPath);

                        File.Delete(downloadPath);
                    }

                    Interlocked.Increment(ref processedCount);
                    Log.Info(
                        "Backed Up '{0}.{1}' ({2} of {3}).",
                        package.Id,
                        package.Version,
                        processedCount,
                        totalCount);

                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref processedCount);
                    Log.Error(
                        "Error Backing Up '{0}.{1}' ({2} of {3}): {4}",
                        package.Id,
                        package.Version,
                        processedCount,
                        totalCount,
                        ex.Message);
                }
            });
        }
        
        string DownloadPackage(Package package)
        {
            var cloudClient = CreateBlobClient();

            var packagesBlobContainer = Util.GetPackagesBlobContainer(cloudClient);

            var packageFileName = Util.GetPackageFileName(package.Id, package.Version);

            var downloadPath = Path.Combine(_tempFolder, packageFileName);

            var blob = packagesBlobContainer.GetBlockBlobReference(packageFileName);
            blob.DownloadToFile(downloadPath);

            return downloadPath;
        }

        IEnumerable<string> GetPackageBackupBlobFileNames()
        {
            var blobClient = CreateBlobClient();

            var packageBackupsBlobContainer = Util.GetPackageBackupsBlobContainer(blobClient);

            return packageBackupsBlobContainer.ListBlobs().Select(bi => bi.Uri.Segments.Last());
        }

        IDictionary<string, Package> GetPackagesToBackUp()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                var galleryPackages = dbExecutor.Query<Package>(@"
                    SELECT pr.Id, p.Version, p.Hash 
                    FROM Packages p 
                        JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey 
                    WHERE p.ExternalPackageUrl IS NULL
                    ORDER BY Id, Version, Hash");

                return galleryPackages.ToDictionary(p => Util.GetPackageBackupFileName(p.Id, p.Version, p.Hash));
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

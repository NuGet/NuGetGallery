// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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
    [Command("restorepackages", "Restore Packages from Backup", AltName = "rps")]
    public class RestorePackagesTask : DatabaseAndStorageTask
    {
        private readonly string _tempFolder;

        public RestorePackagesTask()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "NuGetGalleryOps");
            Directory.CreateDirectory(_tempFolder);
        }

        string DownloadPackageBackup(
            string id,
            string version,
            string hash)
        {
            var blobClient = CreateBlobClient();
            var packageBackupsBlobContainer = Util.GetPackageBackupsBlobContainer(blobClient);
            var packageBackupFileName = Util.GetPackageBackupFileName(
                id,
                version,
                hash);
            var packageBackupBlob = packageBackupsBlobContainer.GetBlockBlobReference(packageBackupFileName);
            var downloadPath = Path.Combine(_tempFolder, packageBackupFileName);
            packageBackupBlob.DownloadToFile(downloadPath);
            return downloadPath;
        }

        public override void ExecuteCommand()
        {
            Log.Info("Getting list of packages to restore; this will take some time.");
            var packages = GetPackages();
            var packageBlobFileNames = GetPackageBlobFileNames();
            var packageFileNamesToRestore = packages.Keys.Except(packageBlobFileNames).ToList();

            var totalCount = packageFileNamesToRestore.Count;
            var processedCount = 0;
            Log.Info(
                "Restoring {0} packages in storage account '{1}'.",
                totalCount,
                StorageAccountName);

            Parallel.ForEach(packageFileNamesToRestore, new ParallelOptions { MaxDegreeOfParallelism = 10 }, packageFileNameToRestore =>
            {
                var package = packages[packageFileNameToRestore];
                try
                {
                    var downloadPath = DownloadPackageBackup(
                        package.Id,
                        package.Version,
                        package.Hash);
                    UploadPackage(
                        package.Id,
                        package.Version,
                        downloadPath);
                    File.Delete(downloadPath);
                    Interlocked.Increment(ref processedCount);
                    Log.Info(
                        "Restored package '{0}.{1}' ({2} of {3}).",
                        package.Id,
                        package.Version,
                        processedCount,
                        totalCount);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref processedCount);
                    Log.Info(
                        "Error restoring package '{0}.{1}' ({2} of {3}): {4}.",
                        package.Id,
                        package.Version,
                        processedCount,
                        totalCount,
                        ex.Message);
                }
            });
        }

        private IEnumerable<string> GetPackageBlobFileNames()
        {
            var blobClient = CreateBlobClient();
            var packagesBlobContainer = Util.GetPackagesBlobContainer(blobClient);
            return packagesBlobContainer.ListBlobs().Select(bi => bi.Uri.Segments.Last());
        }

        IDictionary<string, Package> GetPackages()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();
                var packages = dbExecutor.Query<Package>(@"
                    SELECT pr.Id, p.Version, p.Hash 
                    FROM Packages p 
                        JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey 
                    WHERE p.ExternalPackageUrl IS NULL
                    ORDER BY Id, Version, Hash");
                return packages.ToDictionary(p => Util.GetPackageFileName(p.Id, p.Version));
            }
        }

        void UploadPackage(
            string id,
            string version,
            string downloadPath)
        {
            var blobClient = CreateBlobClient();
            var packagesBlobContainer = Util.GetPackagesBlobContainer(blobClient);
            var packageFileName = Util.GetPackageFileName(
                id,
                version);
            var packageBlob = packagesBlobContainer.GetBlockBlobReference(packageFileName);
            packageBlob.UploadFile(downloadPath);
            packageBlob.Properties.ContentType = "application/zip";
            packageBlob.SetProperties();
        }
    }
}

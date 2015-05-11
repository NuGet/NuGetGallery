// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.IO;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("backuppackagefile", "Back up a specific package file", AltName = "bpf", MaxArgs = 0)]
    public class BackupPackageFileTask : PackageVersionTask
    {
        [Option("The destination storage account for the backups", AltName = "d")]
        public CloudStorageAccount BackupStorage { get; set; }

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
            var client = CreateBlobClient();
            var backupClient = BackupStorage.CreateCloudBlobClient();

            var backupBlobs = backupClient.GetContainerReference("package-backups");
            var packageBlobs = client.GetContainerReference("packages");
            if (!WhatIf)
            {
                backupBlobs.CreateIfNotExists();
            }

            var backupFileName = Util.GetPackageBackupFileName(
                PackageId,
                PackageVersion,
                PackageHash);
            var backupPackageBlob = backupBlobs.GetBlockBlobReference(backupFileName);
            if (backupPackageBlob.Exists())
            {
                Log.Info("Skipped {0} {1}: backup already exists", PackageId, PackageVersion);
                return;
            }

            var packageFileBlob = Util.GetPackageFileBlob(
                packageBlobs,
                PackageId,
                PackageVersion);
            var packageFileName = Util.GetPackageFileName(
                PackageId,
                PackageVersion);
            var downloadedPackageFilePath = Path.Combine(Util.GetTempFolder(), packageFileName);

            // Why are we still downloading/uploading instead of using Async Blob Copy?
            // Because it feels a little safer to ensure we know the copy is truely complete before continuing.
            // I could be convinced otherwise though 
            //  - anurse
            Log.Trace("Downloading package file '{0}' to temporary file '{1}'.", packageFileName, downloadedPackageFilePath);
            if (!WhatIf)
            {
                packageFileBlob.DownloadToFile(downloadedPackageFilePath);
            }

            Log.Trace("Uploading package file backup '{0}' from temporary file '{1}'.", backupFileName, downloadedPackageFilePath);
            if (!WhatIf)
            {
                backupPackageBlob.UploadFile(downloadedPackageFilePath);
                backupPackageBlob.Properties.ContentType = "application/zip";
                backupPackageBlob.SetProperties();
            }

            Log.Trace("Deleting temporary file '{0}'.", downloadedPackageFilePath);
            if (!WhatIf)
            {
                File.Delete(downloadedPackageFilePath);
            }
            Log.Info("Backed Up {0} {1}", PackageId, PackageVersion);
        }
    }
}

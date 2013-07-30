using System.IO;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("backuppackagefile", "Back up a specific package file", AltName = "bpf", MaxArgs = 0)]
    public class BackupPackageFileTask : PackageVersionTask
    {
        public override void ExecuteCommand()
        {
            var blobClient = CreateBlobClient();

            var packageBackupsBlobContainer = Util.GetPackageBackupsBlobContainer(blobClient);
            var backupFileName = Util.GetPackageBackupFileName(
                PackageId,
                PackageVersion,
                PackageHash);
            var backupPackageBlob = packageBackupsBlobContainer.GetBlockBlobReference(backupFileName);
            if (backupPackageBlob.Exists())
            {
                Log.Info("Skipped {0} {1}: backup already exists", PackageId, PackageVersion);
                return;
            }

            var packagesBlobContainer = Util.GetPackagesBlobContainer(blobClient);
            var packageFileBlob = Util.GetPackageFileBlob(
                packagesBlobContainer,
                PackageId,
                PackageVersion);
            var packageFileName = Util.GetPackageFileName(
                PackageId,
                PackageVersion);
            var downloadedPackageFilePath = Path.Combine(Util.GetTempFolder(), packageFileName);

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

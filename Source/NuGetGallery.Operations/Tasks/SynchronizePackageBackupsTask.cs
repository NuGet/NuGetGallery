using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery.Operations
{
    [Command("syncpackagebackups", "Transfers package backups from the source storage server to the destination storage server", AltName = "spb")]
    public class SynchronizePackageBackupsTask : OpsTask
    {
        [Option("Connection string to the source storage server", AltName = "ss")]
        public CloudStorageAccount SourceStorage { get; set; }

        [Option("Connection string to the destination storage server", AltName = "ds")]
        public CloudStorageAccount DestinationStorage { get; set; }

        private readonly string _tempFolder;

        public SynchronizePackageBackupsTask()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "NuGetGalleryOps");
            Directory.CreateDirectory(_tempFolder);
        }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (CurrentEnvironment != null)
            {
                if (SourceStorage == null)
                {
                    SourceStorage = CurrentEnvironment.BackupSourceStorage;
                }
                if (DestinationStorage == null)
                {
                    DestinationStorage = CurrentEnvironment.MainStorage;
                }
            }
        }

        string DownloadPackageBackupFromSource(string packageBackupBlobFileName)
        {
            var cloudClient = SourceStorage.CreateCloudBlobClient();

            var sourcePackageBackupsBlobContainer = Util.GetPackageBackupsBlobContainer(cloudClient);

            var downloadPath = Path.Combine(_tempFolder, packageBackupBlobFileName);

            var blob = sourcePackageBackupsBlobContainer.GetBlockBlobReference(packageBackupBlobFileName);
            blob.DownloadToFile(downloadPath);

            return downloadPath;
        }

        IEnumerable<string> GetDestinationPackageBackupBlobFileNames()
        {
            var destinationBlobClient = DestinationStorage.CreateCloudBlobClient();

            var destinationPackageBackupsBlobContainer = Util.GetPackageBackupsBlobContainer(destinationBlobClient);

            return destinationPackageBackupsBlobContainer.ListBlobs().Select(bi => bi.Uri.Segments.Last());
        }
        
        IEnumerable<string> GetSourcePackageBackupBlobFileNames()
        {
            var sourceBlobClient = SourceStorage.CreateCloudBlobClient();

            var sourcePackageBackupsBlobContainer = Util.GetPackageBackupsBlobContainer(sourceBlobClient);

            return sourcePackageBackupsBlobContainer.ListBlobs().Select(bi => bi.Uri.Segments.Last());
        }
        
        public override void ExecuteCommand()
        {
            var sourcePackageBackupBlobFileNames = GetSourcePackageBackupBlobFileNames();
            var destinationPackageBackupBlobFileNames = GetDestinationPackageBackupBlobFileNames();

            Log.Trace("Getting source package backups to sync; this will take some time.");
            var sourcePackageBackupBlobFileNamesToBackUp = sourcePackageBackupBlobFileNames.Except(destinationPackageBackupBlobFileNames).ToList();

            var totalCount = sourcePackageBackupBlobFileNamesToBackUp.Count;
            var processedCount = 0;
            Log.Trace(
                "Sync'ing {0} package backups from storage account '{1}' to storage account '{2}'.",
                totalCount,
                SourceStorage.Credentials.AccountName,
                DestinationStorage.Credentials.AccountName);

            Parallel.ForEach(sourcePackageBackupBlobFileNamesToBackUp, new ParallelOptions { MaxDegreeOfParallelism = 20 }, packageBackupBlobFileName =>
            {
                try
                {
                    var downloadPath = DownloadPackageBackupFromSource(packageBackupBlobFileName);

                    UploadPackageBackupToDestination(
                        packageBackupBlobFileName,
                        downloadPath);

                    File.Delete(downloadPath);

                    Interlocked.Increment(ref processedCount);
                    Log.Info(
                        "Copied package backup '{0}' ({1} of {2}).",
                        packageBackupBlobFileName,
                        processedCount,
                        totalCount);

                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref processedCount);
                    Log.Error(
                        "Error copying package backup '{0}' ({1} of {2}): {3}",
                        packageBackupBlobFileName,
                        processedCount,
                        totalCount,
                        ex.Message);
                }
            });
        }

        void UploadPackageBackupToDestination(
            string packageBackupBlobFileName,
            string downloadPath)
        {
            var client = DestinationStorage.CreateCloudBlobClient();

            var destinationPackageBackupsBlobContainer = Util.GetPackageBackupsBlobContainer(client);

            var blob = destinationPackageBackupsBlobContainer.GetBlockBlobReference(packageBackupBlobFileName);
            
            blob.UploadFile(downloadPath);
            blob.Properties.ContentType = "application/zip";
            blob.SetProperties();
        }
    }
}

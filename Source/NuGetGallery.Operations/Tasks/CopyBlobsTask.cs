using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("copyblobs", "Copies blobs from a source to a destination", AltName = "cpb")]
    public class CopyBlobsTask : OpsTask
    {
        [Option("Connection string to the source storage server", AltName = "ss")]
        public CloudStorageAccount SourceStorage { get; set; }

        [Option("Container containing the blobs to copy", AltName = "sc")]
        public string SourceContainer { get; set; }

        [Option("Connection string to the destination storage server", AltName = "ds")]
        public CloudStorageAccount DestinationStorage { get; set; }

        [Option("Container to copy the blobs to", AltName = "dc")]
        public string DestinationContainer { get; set; }

        [Option("Prefix of source blobs to copy. If not specified, copies all blobs", AltName = "p")]
        public string Prefix { get; set; }

        [Option("If specified, overwrite existing blobs. Otherwise, blobs that exist will be ignored (this is a name check ONLY)", AltName = "w")]
        public bool Overwrite { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            ArgCheck.Required(SourceStorage, "SourceStorage");
            ArgCheck.Required(SourceContainer, "SourceContainer");
            ArgCheck.Required(DestinationStorage, "DestinationStorage");
            ArgCheck.Required(DestinationContainer, "DestinationContainer");
        }

        public override void ExecuteCommand()
        {
            var sourceClient = SourceStorage.CreateCloudBlobClient();
            var sourceContainer = sourceClient.GetContainerReference(SourceContainer);
            if (!sourceContainer.Exists())
            {
                Log.Warn("No blobs in container!");
                return;
            }

            var destClient = DestinationStorage.CreateCloudBlobClient();
            var destContainer = destClient.GetContainerReference(DestinationContainer);
            destContainer.CreateIfNotExists();

            Log.Info("Collecting blob names in {0} to copy to {1}", SourceStorage.Credentials.AccountName, DestinationStorage.Credentials.AccountName);
            var blobs = sourceContainer.ListBlobs(Prefix ?? String.Empty, useFlatBlobListing: true, blobListingDetails: BlobListingDetails.None)
                                       .OfType<CloudBlockBlob>()
                                       .ToList();
            var count = blobs.Count;
            int index = 0;
            foreach (var blob in blobs)
            {
                index++;
                var destBlob = destContainer.GetBlockBlobReference(blob.Name);

                var percentage = (int)(((double)index / (double)count) * 100);
                if (!destBlob.Exists() || Overwrite)
                {
                    Log.Info("[{1}/{2}] ({3}%) Started Async Copy of {0}.", blob.Name, index, count, percentage);
                    if (!WhatIf)
                    {
                        destBlob.StartCopyFromBlob(blob);
                    }
                }
                else
                {
                    Log.Info("[{1}/{2}] ({3}%) Skipped {0}.", blob.Name, index, count, percentage);
                }
            }

            Log.Info("Copies started. Run checkblobcopies with similar parameters to wait on blob copy completion");
        }
    }
}

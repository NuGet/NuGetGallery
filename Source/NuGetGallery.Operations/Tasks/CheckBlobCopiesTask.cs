using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("checkblobcopies", "Copies blobs from a source to a destination", AltName = "chkcp")]
    public class CheckBlobCopiesTask : OpsTask
    {
        [Option("Connection string to the destination storage server", AltName = "ds")]
        public CloudStorageAccount DestinationStorage { get; set; }

        [Option("Container to copy the blobs to", AltName = "dc")]
        public string DestinationContainer { get; set; }

        [Option("Prefix of source blobs to copy. If not specified, copies all blobs", AltName = "p")]
        public string Prefix { get; set; }

        [Option("Set this switch to wait for the copies to complete and continue to report status")]
        public bool Wait { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            ArgCheck.Required(DestinationStorage, "DestinationStorage");
            ArgCheck.Required(DestinationContainer, "DestinationContainer");
        }
        
        public override void ExecuteCommand()
        {
            var destClient = DestinationStorage.CreateCloudBlobClient();
            var destContainer = destClient.GetContainerReference(DestinationContainer);

            while (!destContainer.ListBlobs(Prefix, useFlatBlobListing: true, blobListingDetails: BlobListingDetails.Copy)
                                 .OfType<CloudBlockBlob>()
                                 .All(b => ReportStatus(b)) || !Wait) {
                Log.Info("Sleeping for 5 seconds");
                Thread.Sleep(5 * 1000);
            }

            Log.Info("Copies started. Run checkblobcopy with the same parameters to wait on blob copy completion");
        }

        private bool ReportStatus(CloudBlockBlob blob)
        {
            if (blob.CopyState.Status != CopyStatus.Success)
            {
                var percentComplete = (int)(((double)blob.CopyState.BytesCopied / (double)blob.CopyState.TotalBytes) * 100);
                Log.Info("Copy of {0} is {1}% complete", blob.Name, percentComplete);
            }
            return blob.CopyState.Status == CopyStatus.Success;
        }
    }
}

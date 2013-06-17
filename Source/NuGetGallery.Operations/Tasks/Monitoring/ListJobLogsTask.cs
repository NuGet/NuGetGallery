using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Infrastructure;

namespace NuGetGallery.Operations.Tasks.Monitoring
{
    [Command("listjoblogs", "Lists available job logs", AltName="ljl")]
    public class ListJobLogsTask : OpsTask
    {
        [Option("The storage account containing job log blobs", AltName="s")]
        public CloudStorageAccount StorageAccount { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (StorageAccount == null && CurrentEnvironment != null)
            {
                StorageAccount = CurrentEnvironment.BackupStorage;
            }
        }

        public override void ExecuteCommand()
        {
            // List available blobs in "wad-joblogs" container
            var client = StorageAccount.CreateCloudBlobClient();
            var container = client.GetContainerReference("wad-joblogs");
            var groups = container
                .ListBlobs(useFlatBlobListing: true)
                .OfType<CloudBlockBlob>()
                .Select(b => new JobLogBlob(b))
                .GroupBy(b => b.JobName);

            // Create Job Log info
            var joblogs = groups.Select(g => new JobLog(g.Key, g.ToList()));

            // List logs!
            foreach (var log in joblogs)
            {
                Log.Info("* {0}", log.JobName);
            }
        }
    }
}

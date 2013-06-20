using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Model;

namespace NuGetGallery.Operations.Tasks.Backups
{
    [Command("cleanofflinedatabasebackups", "Lists the available offline database backups", AltName = "cloffback")]
    public class CleanOfflineDatabaseBackupsTask : BackupStorageTask
    {
        public override void ExecuteCommand()
        {
            var today = DateTime.UtcNow.Date;

            // List available backups
            var toDelete = new HashSet<CloudBlockBlob>();
            var client = CreateBlobClient();
            var container = client.GetContainerReference("database-backups");
            var backups = container
                .ListBlobs(useFlatBlobListing: true, blobListingDetails: BlobListingDetails.Metadata)
                .Cast<CloudBlockBlob>()
                .Where(b => OfflineDatabaseBackup.IsOfflineBackup(b))
                .Select(b => new OfflineDatabaseBackup(b))
                .ToList();

            // Group by days
            var days = backups.GroupBy(b => b.Timestamp.Date).OrderByDescending(g => g.Key);

            // Find any days with multiple backups and delete the extras
            foreach (var backup in days.SelectMany(d => d.OrderByDescending(b => b.Timestamp).Skip(1)))
            {
                toDelete.Add(backup.Blob);
            }

            // Skip until a week before today
            var olderThanAWeek = days.Where(g => g.Key < (today.AddDays(-7)));

            // Delete any of the older ones from the current month
            foreach (var backup in olderThanAWeek.Where(b => b.Key.Month == today.Month).SelectMany(b => b))
            {
                toDelete.Add(backup.Blob);
            }
            
            // Group previous backups in to months
            var months = backups
                .GroupBy(b => new { b.Timestamp.Month, b.Timestamp.Year })
                .Where(g => g.Key.Month != today.Month || g.Key.Year != today.Year);

            // Keep the last chronological backup for each month
            foreach(var backup in months.SelectMany(g => g.OrderByDescending(b => b.Timestamp).Skip(1)))
            {
                toDelete.Add(backup.Blob);
            }

            // Finally, delete anything older than a year
            foreach (var backup in backups.Where(b => b.Timestamp < today.AddYears(-1)))
            {
                toDelete.Add(backup.Blob);
            }

            foreach (var backup in toDelete)
            {
                DeleteBackup(backup);
            }

            Log.Info("Finished cleaning backups!");
        }

        private void DeleteBackup(CloudBlockBlob blob)
        {
            Log.Info("Deleting Blob: {0}", blob.Uri.AbsoluteUri);
            if (!WhatIf)
            {
                blob.DeleteIfExists(DeleteSnapshotsOption.IncludeSnapshots, accessCondition: AccessCondition.GenerateIfMatchCondition(blob.Properties.ETag));
            }
        }
    }
}

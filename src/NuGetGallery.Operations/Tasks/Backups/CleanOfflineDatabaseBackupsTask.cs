// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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
            var toKeep = new HashSet<string>();
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

            // Keep the last backup of each day for the past 7 days where we have backups
            foreach (var backup in days.Take(7).Select(g => g.OrderByDescending(b => b.Timestamp).First()))
            {
                if (backup.Timestamp < today.AddDays(-7))
                {
                    Log.Warn("Backup '{0}' is in the last 7 days of backups but is older than 7 days!", backup.Blob.Name);
                }
                toKeep.Add(backup.Blob.Name);
            }

            // Group previous backups in to months
            var months = backups
                .GroupBy(b => new { b.Timestamp.Month, b.Timestamp.Year })
                .Where(g => g.Key.Month != today.Month || g.Key.Year != today.Year);

            // Keep the last chronological backup for each month
            foreach(var backup in months.Select(g => g.OrderByDescending(b => b.Timestamp).First()))
            {
                toKeep.Add(backup.Blob.Name);
            }

            // Remove anything older than a year
            foreach (var backup in backups.Where(b => b.Timestamp < today.AddYears(-1) && toKeep.Contains(b.Blob.Name)))
            {
                toKeep.Remove(backup.Blob.Name);
            }

            foreach (var backup in backups)
            {
                if (!toKeep.Contains(backup.Blob.Name))
                {
                    DeleteBackup(backup.Blob);
                }
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

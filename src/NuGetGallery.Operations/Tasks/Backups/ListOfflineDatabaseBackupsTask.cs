// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Model;

namespace NuGetGallery.Operations.Tasks.Backups
{
    [Command("listofflinedatabasebackups", "Lists the available offline database backups", AltName="loffback")]
    public class ListOfflineDatabaseBackupsTask : BackupStorageTask
    {
        public override void ExecuteCommand()
        {
            // List available backups
            var client = CreateBlobClient();
            var container = client.GetContainerReference("database-backups");
            var backups = container
                .ListBlobs(useFlatBlobListing: true)
                .Cast<CloudBlockBlob>()
                .Where(b => OfflineDatabaseBackup.IsOfflineBackup(b))
                .Select(b => new OfflineDatabaseBackup(b))
                .ToList();

            Log.Info("Available Backups:");
            foreach (var backup in backups)
            {
                Log.Info("* {0} ({1} Local, {2} UTC)", backup.Blob.Name, backup.Timestamp.ToLocalTime().ToFriendlyDateTimeString(), backup.Timestamp.ToUniversalTime().ToFriendlyDateTimeString());
            }
        }
    }
}

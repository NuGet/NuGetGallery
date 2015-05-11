// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Operations
{
    [Command("deletebrokenpackagebackups", "Delete Package Backups which are broken", AltName = "dbpb", IsSpecialPurpose = true)]
    public class DeleteBrokenPackageBackupsTask : StorageTask
    {
        public override void ExecuteCommand()
        {
            var storageName = StorageAccountName;

            Log.Trace("Getting all broken package backup files on storage account '{0}'.", storageName);
            
            var blobItems = GetPackageBackupBlobItems().ToList();

            var blobDirectories = blobItems
                .Select(bi => bi as CloudBlobDirectory)
                .Where(directory => directory != null)
                .ToList();

            var totalCount = blobDirectories.Count;
            var processedCount = 0;
            Log.Trace(
                "Deleting {0} broken package backup files (out of {1} total blob items) on storage account '{2}'.",
                totalCount,
                blobItems.Count,
                storageName);
            
            Parallel.ForEach(blobDirectories, blobDirectory =>
            {
                try
                {
                    if (!WhatIf)
                    {
                        DeleteBlobDirectory(blobDirectory);
                    }
                    Interlocked.Increment(ref processedCount);
                    Log.Info(string.Format("Deleted broken package backup root directory '{0}' ({1} of {2}).", blobDirectory.Uri.Segments.Last(), processedCount, totalCount));
                }
                catch(Exception ex)
                {
                    Interlocked.Increment(ref processedCount);
                    Log.Error(
                            "Error deleting broken package backup root directory '{0}': {1} ({2} of {3}).", 
                            blobDirectory.Uri.Segments.Last(), processedCount, totalCount, ex.Message);
                }
            });
        }

        IEnumerable<IListBlobItem> GetPackageBackupBlobItems()
        {
            var blobClient = CreateBlobClient();

            var packageBackupsBlobContainer = Util.GetPackageBackupsBlobContainer(blobClient);

            return packageBackupsBlobContainer.ListBlobs();
        }

        static void DeleteBlobDirectory(CloudBlobDirectory blobDirectory)
        {
            foreach(var blobItem in blobDirectory.ListBlobs())
            {
                var subDirectory = blobItem as CloudBlobDirectory;
                if (subDirectory != null)
                    DeleteBlobDirectory(subDirectory);

                var blob = blobItem as ICloudBlob;
                if (blob != null)
                    blob.Delete();
            }
        }
    }
}

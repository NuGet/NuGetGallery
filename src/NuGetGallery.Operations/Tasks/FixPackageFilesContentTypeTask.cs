// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Operations
{
    [Command("fixcontenttypes", "Fixes the content type of package files in the storage server", AltName = "fct", IsSpecialPurpose = true)]
    public class FixPackageFilesContentTypeTask : StorageTask
    {
        public override void ExecuteCommand()
        {
            var blobClient = CreateBlobClient();

            var packagesBlobContainer = Util.GetPackagesBlobContainer(blobClient);

            Log.Info("Listing all blobs...");
            var blobs = packagesBlobContainer.ListBlobs();
            Log.Info("Looking for broken blobs");
            ConcurrentBag<CloudBlockBlob> broken = new ConcurrentBag<CloudBlockBlob>();
            Parallel.ForEach(blobs, blob =>
            {
                var packageFileBlob = packagesBlobContainer.GetBlockBlobReference(blob.Uri.ToString());
                packageFileBlob.FetchAttributes();
                if (packageFileBlob.Properties.ContentType != "application/zip")
                {
                    broken.Add(packageFileBlob);
                }
            });
            Log.Info("Fixing {0} broken blobs...");
            int totalCount = broken.Count;
            int processedCount = 0;
            Parallel.ForEach(broken, packageFileBlob =>
            {
                if (!WhatIf)
                {
                    packageFileBlob.Properties.ContentType = "application/zip";
                    packageFileBlob.SetProperties();
                }
                Log.Info("Fixed '{0}' ({1} of {2}).", packageFileBlob.Uri.Segments.Last(), Interlocked.Increment(ref processedCount), totalCount);
            });
        }
    }
}

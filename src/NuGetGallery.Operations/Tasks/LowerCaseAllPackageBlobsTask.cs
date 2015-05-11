// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks
{
    [Command("lowercaseallpackageblobs", "Standardize all blob names as lowercase so that retrieving a package doesn't need to be case insensitive or disambiguated with use of the database", AltName = "lca", MaxArgs = 0, IsSpecialPurpose = true)]
    public class LowerCaseAllPackageBlobsTask : StorageTask
    {
        public override void ExecuteCommand()
        {
            var blobContainer = Util.GetPackagesBlobContainer(CreateBlobClient());
            var allBlobsLazy = blobContainer.ListBlobs();
            Parallel.ForEach(allBlobsLazy, blob =>
            {
                int p = blob.Uri.AbsolutePath.LastIndexOf('/');
                string blobName = blob.Uri.AbsolutePath.Substring(p);
                string lowerCaseName = blobName.ToLowerInvariant();
                if (string.Equals(blobName, lowerCaseName, StringComparison.Ordinal))
                {
                    Log.Info("already lower case: " + blobName);
                }
                else
                {
                    var newBlob = blobContainer.GetBlockBlobReference(lowerCaseName);
                    if (newBlob.Exists())
                    {
                        Log.Info("already converted: " + lowerCaseName);
                    }
                    else
                    {
                        Log.Info("async blob copy" + blobName + " => " + lowerCaseName);
                        {
                            newBlob.StartCopyFromBlob(blob.Uri);
                        }
                    }
                }
            });

            Log.Info("Finished processing all blobs");
        }
    }
}

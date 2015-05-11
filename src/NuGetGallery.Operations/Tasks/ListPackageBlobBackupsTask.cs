// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks
{
    [Command("listpackageblobbackups", "List the URLS of all the backup blobs for a package whose nupkgs are backed up", AltName = "lpbb", MaxArgs = 0)]
    public class ListPackageBlobBackupsTask : BackupStorageTask
    {
        [Option("Id of the package - Id ending in * will do a wildcarded prefix search on the package Id.")]
        public string PackageId { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(PackageId, "PackageId");
        }

        public override void ExecuteCommand()
        {
            var blobClient = CreateBlobClient();
            var storageName = StorageAccountName;

            Log.Trace("Getting all package backup files for package id '{1}' on storage account '{0}'.", storageName, PackageId);
            var packageBackupsBlobContainer = Util.GetPackageBackupsBlobContainer(blobClient);
            Log.Trace("Container name is '{0}'", packageBackupsBlobContainer.Name);

            var packageIdIsh = PackageId.EndsWith("*", StringComparison.Ordinal) ? PackageId.TrimEnd('*') : PackageId + "/";
            var allBlobs = packageBackupsBlobContainer.ListBlobs(prefix: packageIdIsh);
            bool empty = true;
            foreach (var blob in allBlobs)
            {
                empty = false;
                Log.Trace(blob.Uri);
            }

            if (empty)
            {
                Log.Trace("No matching blobs found");
            }
        }
    }
}

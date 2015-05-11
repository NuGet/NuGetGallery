// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.IO;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("uploadpackage", "Upload a package to the storage server", AltName = "up")]
    public class UploadPackageTask : StorageTask
    {
        [Option("The ID of the package", AltName = "p")]
        public string PackageId { get; set; }

        [Option("The Version of the package", AltName = "v")]
        public string PackageVersion { get; set; }

        [Option("The file to upload", AltName = "u")]
        public Stream PackageFile { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(PackageId, "PackageId");
            ArgCheck.Required(PackageVersion, "PackageVersion");
            ArgCheck.Required(PackageFile, "PackageFile");
        }
        
        public override void ExecuteCommand()
        {
            var client = CreateBlobClient();

            var container = client.GetContainerReference("packages");

            var fileName = string.Format(
                "{0}.{1}{2}",
                PackageId,
                PackageVersion,
                ".nupkg");

            var blob = container.GetBlockBlobReference(fileName);
            if (!WhatIf)
            {
                blob.DeleteIfExists();
                blob.UploadFromStream(PackageFile);
                blob.Properties.ContentType = "application/zip";
                blob.SetProperties();
            }
            Log.Info("Uploaded new package blob: {0}", blob.Name);
        }
    }
}
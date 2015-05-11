// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("syncpackagebackups", "Transfers package backups from the source storage server to the destination storage server", AltName = "spb")]
    public class SynchronizePackageBackupsTask : OpsTask
    {
        [Option("Connection string to the source storage server", AltName = "ss")]
        public CloudStorageAccount SourceStorage { get; set; }

        [Option("Connection string to the destination storage server", AltName = "ds")]
        public CloudStorageAccount DestinationStorage { get; set; }

        private readonly string _tempFolder;

        public SynchronizePackageBackupsTask()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "NuGetGalleryOps");
            Directory.CreateDirectory(_tempFolder);
        }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (CurrentEnvironment != null)
            {
                if (DestinationStorage == null)
                {
                    DestinationStorage = CurrentEnvironment.MainStorage;
                }
            }
            ArgCheck.Required(SourceStorage, "SourceStorage");
            ArgCheck.RequiredOrConfig(DestinationStorage, "DestinationStorage");
        }
        
        public override void ExecuteCommand()
        {
            new CopyBlobsTask() {
                SourceStorage = SourceStorage,
                SourceContainer = "packagebackups",
                DestinationStorage = DestinationStorage,
                DestinationContainer = "packagebackups",
                WhatIf = WhatIf,
                Overwrite = false
            }.Execute();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("checkblobcopies", "Copies blobs from a source to a destination", AltName = "chkcp")]
    public class CheckBlobCopiesTask : OpsTask
    {
        [Option("Connection string to the destination storage server", AltName = "ds")]
        public CloudStorageAccount DestinationStorage { get; set; }

        [Option("Container to copy the blobs to", AltName = "dc")]
        public string DestinationContainer { get; set; }

        [Option("Prefix of source blobs to copy. If not specified, copies all blobs", AltName = "p")]
        public string Prefix { get; set; }

        [Option("Set this switch to wait for the copies to complete and continue to report status")]
        public bool Wait { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            ArgCheck.Required(DestinationStorage, "DestinationStorage");
            ArgCheck.Required(DestinationContainer, "DestinationContainer");
        }
        
        public override void ExecuteCommand()
        {
            var destClient = DestinationStorage.CreateCloudBlobClient();
            var destContainer = destClient.GetContainerReference(DestinationContainer);

            // Iterate through the blobs
            int index = 0;
            var blobs = Util.EnumerateBlobs(Log, destContainer, Prefix ?? String.Empty);
            Parallel.ForEach(blobs, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, blob =>
            {
                Interlocked.Increment(ref index);
                if (blob.CopyState.Status != CopyStatus.Pending)
                {
                    int counter = 0;
                    while (blob.CopyState.Status == CopyStatus.Pending)
                    {
                        Thread.Sleep(1000);
                        counter++;
                        blob.FetchAttributes();

                        if (counter % 5 == 0)
                        {
                            Log.Info("{1}Waiting on {0} ...", blob.Name, counter > 5 ? "Still " : "");
                        }
                    }
                    if (counter > 2)
                    {
                        Log.Info("Copy of {0} has finished!", blob.Name);
                    }
                }
                index++;
            });

            Log.Info("{0} Copies Complete!", index);
        }
    }
}

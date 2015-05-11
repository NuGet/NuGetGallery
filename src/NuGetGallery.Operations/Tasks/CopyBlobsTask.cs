// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("copyblobs", "Copies blobs from a source to a destination", AltName = "cpb")]
    public class CopyBlobsTask : OpsTask
    {
        [Option("Connection string to the source storage server", AltName = "ss")]
        public CloudStorageAccount SourceStorage { get; set; }

        [Option("Container containing the blobs to copy", AltName = "sc")]
        public string SourceContainer { get; set; }

        [Option("Connection string to the destination storage server", AltName = "ds")]
        public CloudStorageAccount DestinationStorage { get; set; }

        [Option("Container to copy the blobs to", AltName = "dc")]
        public string DestinationContainer { get; set; }

        [Option("Prefix of source blobs to copy. If not specified, copies all blobs", AltName = "p")]
        public string Prefix { get; set; }

        [Option("If specified, overwrite existing blobs. Otherwise, blobs that exist will be ignored (this is a name check ONLY)", AltName = "w")]
        public bool Overwrite { get; set; }

        [Option("If specified, adds checks to ensure the process only copies valid package blobs (i.e. no '/' prefix and all lowercase names)")]
        public bool PackageBlobsOnly { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            ArgCheck.Required(SourceStorage, "SourceStorage");
            ArgCheck.Required(SourceContainer, "SourceContainer");
            ArgCheck.Required(DestinationStorage, "DestinationStorage");
            ArgCheck.Required(DestinationContainer, "DestinationContainer");
        }

        public override void ExecuteCommand()
        {
            var sourceClient = SourceStorage.CreateCloudBlobClient();
            var sourceContainer = sourceClient.GetContainerReference(SourceContainer);
            if (!sourceContainer.Exists())
            {
                Log.Warn("No blobs in container!");
                return;
            }

            var destClient = DestinationStorage.CreateCloudBlobClient();
            var destContainer = destClient.GetContainerReference(DestinationContainer);
            destContainer.CreateIfNotExists();

            Log.Info("Collecting blob names in {0}...", SourceStorage.Credentials.AccountName);
            var sourceBlobs = Util.CollectBlobs(
                Log, 
                sourceContainer, 
                Prefix ?? String.Empty, 
                condition: b => (!PackageBlobsOnly || (!b.Name.StartsWith("/", StringComparison.Ordinal) && String.Equals(b.Name.ToLowerInvariant(), b.Name, StringComparison.Ordinal))),
                countEstimate: 250000);

            Log.Info("Collecting blob names in {0}...", DestinationStorage.Credentials.AccountName);
            var destBlobs = Util.CollectBlobs(
                Log,
                destContainer,
                Prefix ?? String.Empty,
                condition: b => (!PackageBlobsOnly || (!b.Name.StartsWith("/", StringComparison.Ordinal) && String.Equals(b.Name.ToLowerInvariant(), b.Name, StringComparison.Ordinal))),
                countEstimate: 250000);
            var count = sourceBlobs.Count;
            int index = 0;

            Parallel.ForEach(sourceBlobs, new ParallelOptions { MaxDegreeOfParallelism = 10 }, blob =>
            {
                int currentIndex = Interlocked.Increment(ref index);
                var percentage = (((double)currentIndex / (double)count) * 100);
                var destBlob = destContainer.GetBlockBlobReference(blob.Name);
                
                try
                {
                
                    if (!destBlob.Exists() || Overwrite)
                    {
                        Log.Info("[{1:000000}/{2:000000}] ({3:000.00}%) Started Async Copy of {0}.", blob.Name, currentIndex, count, percentage);
                        if (!WhatIf)
                        {
                            destBlob.StartCopyFromBlob(blob);
                        }
                    }
                    else
                    {
                        Log.Info("[{1:000000}/{2:000000}] ({3:000.00}%) Skipped {0}. Blob already Exists", blob.Name, index, count, percentage);
                    }
                }
                catch (StorageException stex)
                {
                    if (stex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.Conflict && 
                        stex.RequestInformation.ExtendedErrorInformation.ErrorCode == BlobErrorCodeStrings.PendingCopyOperation)
                    {
                        Log.Info("[{1:000000}/{2:000000}] ({3:000.00}%) Skipped {0}. Already being copied", blob.Name, index, count, percentage);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Error processing {0}: {1}", blob.Name, ex.ToString());
                    throw;
                }
            });

            Log.Info("Copies started. Run checkblobcopies with similar parameters to wait on blob copy completion");
        }
    }
}

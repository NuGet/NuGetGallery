// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;

namespace SnapshotAzureBlob
{
    public class SnapshotAzureBlobJob : JobBase
    {
        private string _connectionString;
        private string _container;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            _connectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.SnapshotAzureBlobJob_ConnectionString);
            _container = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.SnapshotAzureBlobJob_Container);
        }

        public string GetUsage()
        {
            return "Usage: SnapshotAzureBlobJob "
                   + $"-{ArgumentNames.SnapshotAzureBlobJob_ConnectionString} <connectionString> "
                   + $"-{ArgumentNames.SnapshotAzureBlobJob_Container} <container> "
                   + $"-{JobArgumentNames.InstrumentationKey} <intrumentationKey> ";
        }

        public override async Task Run()
        {
            var storageAccount = CloudStorageAccount.Parse(_connectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            await EnsureOneSnapshotAsync(_container, blobClient);
        }

        private async Task EnsureOneSnapshotAsync(string containerName, CloudBlobClient client)
        {
            var container = client.GetContainerReference(containerName);
            var blobs = await ListAllBlobsAsync(container, prefix: null, blobListingDetails: BlobListingDetails.None);
            var snapshotCount = 0;
            var sw = new Stopwatch();
            sw.Start();

            foreach (var item in blobs)
            {
                try
                {
                    var blob = item as CloudBlockBlob;
                    if (blob != null)
                    {
                        //because the query is filtered by the blob prefix
                        //the list count will be bounded by the count of blobs snapshots taken; 
                        //this count is expected to be small
                        var expandedList = await ListAllBlobsAsync(container, prefix: blob.Name,
                                                               blobListingDetails: BlobListingDetails.Snapshots);
                        if (expandedList.Count == 1)
                        {
                            Interlocked.Increment(ref snapshotCount);
                            await blob.SnapshotAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogCritical(LogEvents.SnaphotFailed, ex, "The snapshot failed for blob {Blob}.", item.Uri);
                }
            }

            sw.Stop();
            Logger.LogInformation("Created {snapshotCount} snapshots in {timeInMilliseconds} milliseconds", snapshotCount, sw.ElapsedMilliseconds);
        }

        private static async Task<List<IListBlobItem>> ListAllBlobsAsync(
            CloudBlobContainer container, string prefix, BlobListingDetails blobListingDetails)
        {
            var results = new List<IListBlobItem>();
            BlobContinuationToken continuationToken = null;
            do
            {
                var segment = await container.ListBlobsSegmentedAsync(
                    prefix, true, blobListingDetails, null, continuationToken, null, null);
                results.AddRange(segment.Results);
                continuationToken = segment.ContinuationToken;
            }
            while (continuationToken != null);
            return results;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace NuGet.Services.Storage
{
    internal static class CloudBlobStorageExtensions
    {
        public static async Task<IEnumerable<BlobItem>> ListBlobsAsync(
            this BlobContainerClient directory,
            bool getMetadata,
            CancellationToken cancellationToken)
        {
            var items = new List<BlobItem>();
            var segment = directory.GetBlobsAsync(
                traits:
                    BlobTraits.CopyStatus
                    | BlobTraits.ImmutabilityPolicy
                    | BlobTraits.LegalHold
                    | (getMetadata ? BlobTraits.None : BlobTraits.Metadata)
                    | BlobTraits.Tags,
                states:
                    BlobStates.Deleted
                    | BlobStates.DeletedWithVersions
                    | BlobStates.Snapshots
                    | BlobStates.Uncommitted
                    | BlobStates.Version);

            await foreach (var blob in segment)
            {
                items.Add(blob);
            }

            return items;
        }
    }
}
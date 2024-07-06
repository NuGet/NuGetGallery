// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace NuGet.Services.Metadata.Catalog
{
    internal static class BlobStorageExtensions
    {
        public static async Task<IEnumerable<BlobHierarchyItem>> ListBlobsAsync(
            this BlobContainerClient containerClient, string prefix, CancellationToken cancellationToken)
        {
            var items = new List<BlobHierarchyItem>();
            var resultSegment = containerClient.GetBlobsByHierarchyAsync(prefix: prefix).AsPages();

            await foreach (Azure.Page<BlobHierarchyItem> blobPage in resultSegment.WithCancellation(cancellationToken))
            {
                foreach (BlobHierarchyItem blobItem in blobPage.Values)
                {
                    items.Add(blobItem);
                }
            }

            return items;
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.Metadata.Catalog
{
    internal static class CloudBlobStorageExtensions
    {
        public static async Task<IEnumerable<IListBlobItem>> ListBlobsAsync(this CloudBlobContainer container, CancellationToken cancellationToken, string prefix = null, bool useFlatBlobListing = false, BlobListingDetails blobListingDetails = BlobListingDetails.None, long? maxResults = null)
        {
            List<IListBlobItem> items = new List<IListBlobItem>();
            BlobContinuationToken token = null;
            do
            {
                var seg = await container.ListBlobsSegmentedAsync(prefix, useFlatBlobListing, blobListingDetails, null, token, null, null);
                token = seg.ContinuationToken;
                items.AddRange(seg.Results);

                if (maxResults.HasValue && items.Count > maxResults.Value)
                {
                    break;
                }
            }
            while (token != null && !cancellationToken.IsCancellationRequested);
            return items;
        }


        public static async Task<IEnumerable<IListBlobItem>> ListBlobsAsync(this CloudBlobDirectory directory, CancellationToken cancellationToken, bool useFlatBlobListing = false, BlobListingDetails blobListingDetails = BlobListingDetails.None, long? maxResults = null)
        {
            List<IListBlobItem> items = new List<IListBlobItem>();
            BlobContinuationToken token = null;
            do
            {
                var seg = await directory.ListBlobsSegmentedAsync(useFlatBlobListing, blobListingDetails, null, token, null, null);
                token = seg.ContinuationToken;
                items.AddRange(seg.Results);

                if (maxResults.HasValue && items.Count > maxResults.Value)
                {
                    break;
                }
            }
            while (token != null && !cancellationToken.IsCancellationRequested);
            return items;
        }
    }
}
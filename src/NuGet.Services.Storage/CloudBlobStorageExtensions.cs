// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.Storage
{
    internal static class CloudBlobStorageExtensions
    {
        public static async Task<IEnumerable<IListBlobItem>> ListBlobsAsync(
            this CloudBlobDirectory directory,
            bool getMetadata,
            CancellationToken cancellationToken)
        {
            var items = new List<IListBlobItem>();
            BlobContinuationToken continuationToken = null;
            do
            {
                var segment = await directory.ListBlobsSegmentedAsync(
                    useFlatBlobListing: true,
                    blobListingDetails: getMetadata ? BlobListingDetails.Metadata : BlobListingDetails.None,
                    maxResults: null,
                    currentToken:  continuationToken,
                    options: null,
                    operationContext: null,
                    cancellationToken: cancellationToken);

                continuationToken = segment.ContinuationToken;
                items.AddRange(segment.Results);
            }
            while (continuationToken != null && !cancellationToken.IsCancellationRequested);

            return items;
        }
    }
}
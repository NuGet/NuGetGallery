// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services
{
    public class InMemoryCloudBlobContainer : ICloudBlobContainer
    {
        private readonly object _lock = new object();

        public SortedDictionary<string, InMemoryCloudBlob> Blobs { get; } = new SortedDictionary<string, InMemoryCloudBlob>();

        public Task CreateAsync(bool enablePublicAccess)
        {
            throw new NotImplementedException();
        }

        public Task CreateIfNotExistAsync(bool enablePublicAccess)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteIfExistsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsAsync(CloudBlobLocationMode? cloudBlobLocationMode)
        {
            throw new NotImplementedException();
        }

        public ISimpleCloudBlob GetBlobReference(string blobAddressUri)
        {
            lock (_lock)
            {
                InMemoryCloudBlob blob;
                if (!Blobs.TryGetValue(blobAddressUri, out blob))
                {
                    blob = new InMemoryCloudBlob();
                    Blobs[blobAddressUri] = blob;
                }

                return blob;
            }
        }

        public Task<ISimpleBlobResultSegment> ListBlobsSegmentedAsync(
            string prefix,
            bool useFlatBlobListing,
            ListingDetails blobListingDetails,
            int? maxResults,
            BlobListContinuationToken blobContinuationToken,
            TimeSpan? requestTimeout,
            CloudBlobLocationMode? cloudBlobLocationMode,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetPermissionsAsync()
        {
            throw new NotImplementedException();
        }
    }
}

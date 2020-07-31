// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery;

namespace NuGet.Services
{
    public class InMemoryCloudBlobContainer : ICloudBlobContainer
    {
        private readonly object _lock = new object();

        public SortedDictionary<string, InMemoryCloudBlob> Blobs { get; } = new SortedDictionary<string, InMemoryCloudBlob>();

        public Task CreateAsync()
        {
            throw new NotImplementedException();
        }

        public Task CreateIfNotExistAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteIfExistsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsAsync(BlobRequestOptions options = null, OperationContext operationContext = null)
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
            BlobListingDetails blobListingDetails,
            int? maxResults,
            BlobContinuationToken blobContinuationToken,
            BlobRequestOptions options,
            OperationContext operationContext,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetPermissionsAsync(BlobContainerPermissions permissions)
        {
            throw new NotImplementedException();
        }
    }
}

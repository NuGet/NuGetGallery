// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    public class CloudBlobContainerWrapper : ICloudBlobContainer
    {
        private readonly CloudBlobContainer _blobContainer;

        public CloudBlobContainerWrapper(CloudBlobContainer blobContainer)
        {
            _blobContainer = blobContainer;
        }

        public async Task<ISimpleBlobResultSegment> ListBlobsSegmentedAsync(
            string prefix,
            bool useFlatBlobListing,
            BlobListingDetails blobListingDetails,
            int? maxResults,
            BlobContinuationToken blobContinuationToken,
            BlobRequestOptions options,
            OperationContext operationContext,
            CancellationToken cancellationToken)
        {
            var segment = await _blobContainer.ListBlobsSegmentedAsync(
                prefix,
                useFlatBlobListing,
                blobListingDetails,
                maxResults,
                blobContinuationToken,
                options,
                operationContext,
                cancellationToken);

            return new BlobResultSegmentWrapper(segment);
        }

        public Task CreateIfNotExistAsync()
        {
            return Task.Factory.FromAsync<bool>(
                _blobContainer.BeginCreateIfNotExists(null, null), 
                _blobContainer.EndCreateIfNotExists);
        }

        public Task SetPermissionsAsync(BlobContainerPermissions permissions)
        {
            return Task.Factory.FromAsync(
                _blobContainer.BeginSetPermissions(permissions, null, null),
                _blobContainer.EndSetPermissions);
        }

        public ISimpleCloudBlob GetBlobReference(string blobAddressUri)
        {
            return new CloudBlobWrapper(_blobContainer.GetBlockBlobReference(blobAddressUri));
        }

        public Task<bool> ExistsAsync(BlobRequestOptions blobRequestOptions, OperationContext context)
        {
            return _blobContainer.ExistsAsync(blobRequestOptions, context);
        }

        public async Task<bool> DeleteIfExistsAsync()
        {
            return await _blobContainer.DeleteIfExistsAsync();
        }

        public async Task CreateAsync()
        {
            await _blobContainer.CreateAsync();
        }
    }
}

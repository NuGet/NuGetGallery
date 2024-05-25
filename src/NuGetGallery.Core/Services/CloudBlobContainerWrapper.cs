// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
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
            ListingDetails blobListingDetails,
            int? maxResults,
            IBlobListContinuationToken blobContinuationToken,
            TimeSpan? requestTimeout,
            CloudBlobLocationMode? cloudBlobLocationMode,
            CancellationToken cancellationToken)
        {
            BlobContinuationToken continuationToken = null;
            if (blobContinuationToken != null)
            {
                if (blobContinuationToken is BlobListContinuationToken token)
                {
                    continuationToken = token.ContinuationToken;
                }
                else
                {
                    throw new ArgumentException();
                }
            }

            BlobRequestOptions options = null;
            if (requestTimeout.HasValue || cloudBlobLocationMode.HasValue)
            {
                options = new BlobRequestOptions();
                if (requestTimeout.HasValue)
                {
                    options.ServerTimeout = requestTimeout.Value;
                }
                if (cloudBlobLocationMode.HasValue)
                {
                    options.LocationMode = CloudWrapperHelpers.GetSdkRetryPolicy(cloudBlobLocationMode.Value);
                }
            }

            var segment = await CloudWrapperHelpers.WrapStorageException(() =>
                _blobContainer.ListBlobsSegmentedAsync(
                    prefix,
                    useFlatBlobListing,
                    CloudWrapperHelpers.GetSdkBlobListingDetails(blobListingDetails),
                    maxResults,
                    continuationToken,
                    options,
                    operationContext: null,
                    cancellationToken: cancellationToken));

            return new BlobResultSegmentWrapper(segment);
        }
        
        public async Task CreateIfNotExistAsync(bool enablePublicAccess)
        {
            var accessType = enablePublicAccess ? BlobContainerPublicAccessType.Blob : BlobContainerPublicAccessType.Off;

            await CloudWrapperHelpers.WrapStorageException(() =>
                _blobContainer.CreateIfNotExistsAsync(accessType, options: null, operationContext: null));
        }

        public ISimpleCloudBlob GetBlobReference(string blobAddressUri)
        {
            return new CloudBlobWrapper(_blobContainer.GetBlockBlobReference(blobAddressUri));
        }

        public async Task<bool> ExistsAsync(CloudBlobLocationMode? cloudBlobLocationMode)
        {
            BlobRequestOptions options = null;
            if (cloudBlobLocationMode.HasValue)
            {
                options = new BlobRequestOptions
                {
                    LocationMode = CloudWrapperHelpers.GetSdkRetryPolicy(cloudBlobLocationMode.Value),
                };
            }
            return await CloudWrapperHelpers.WrapStorageException(() =>
                _blobContainer.ExistsAsync(options, operationContext: null));
        }

        public async Task<bool> DeleteIfExistsAsync()
        {
            return await CloudWrapperHelpers.WrapStorageException(() =>
                _blobContainer.DeleteIfExistsAsync());
        }

        public async Task CreateAsync(bool enablePublicAccess)
        {
            var accessType = enablePublicAccess ? BlobContainerPublicAccessType.Blob : BlobContainerPublicAccessType.Off;

            await CloudWrapperHelpers.WrapStorageException(() =>
                _blobContainer.CreateAsync(accessType, options: null, operationContext: null));
        }
    }
}

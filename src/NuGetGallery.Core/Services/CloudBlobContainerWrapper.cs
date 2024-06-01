// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace NuGetGallery
{
    public class CloudBlobContainerWrapper : ICloudBlobContainer
    {
        private readonly CloudBlobClientWrapper _account;
        private readonly BlobContainerClient _blobContainer;

        public CloudBlobContainerWrapper(BlobContainerClient blobContainer, CloudBlobClientWrapper account)
        {
            _blobContainer = blobContainer ?? throw new ArgumentNullException(nameof(blobContainer));
            _account = account ?? throw new ArgumentNullException(nameof(account));
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
            string continuationToken = null;
            if (blobContinuationToken != null)
            {
                if (blobContinuationToken is BlobListContinuationToken token)
                {
                    continuationToken = token.ContinuationToken;
                }
                else
                {
                    throw new ArgumentException("Unsupported continuation token type", nameof(blobContinuationToken));
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

            BlobTraits traits = CloudWrapperHelpers.GetSdkBlobTraits(blobListingDetails);
            BlobStates states = CloudWrapperHelpers.GetSdkBlobStates(blobListingDetails);
            var enumerable = _blobContainer
                .GetBlobsAsync(traits: traits, states: states, prefix: prefix, cancellationToken: cancellationToken)
                .AsPages(continuationToken, maxResults);

            var enumerator = enumerable.GetAsyncEnumerator(cancellationToken);
            try
            {
                // TODO: make another WrapStorageExceptionAsync overload with ValueTask?..
                if (await CloudWrapperHelpers.WrapStorageExceptionAsync(() => enumerator.MoveNextAsync().AsTask()))
                {
                    var page = enumerator.Current;
                    var nextContinuationToken = string.IsNullOrEmpty(page.ContinuationToken) ? null : page.ContinuationToken;
                    return new BlobResultSegmentWrapper(page.Values, nextContinuationToken);
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            return new BlobResultSegmentWrapper(new List<BlobItem>(), null);
        }
        
        public async Task CreateIfNotExistAsync(bool enablePublicAccess)
        {
            var accessType = enablePublicAccess ? PublicAccessType.Blob : PublicAccessType.None;

            await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blobContainer.CreateIfNotExistsAsync(accessType));
        }

        public ISimpleCloudBlob GetBlobReference(string blobAddressUri)
        {
            return new CloudBlobWrapper(_blobContainer.GetBlockBlobClient(blobAddressUri));
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
            return (await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blobContainer.ExistsAsync())).Value;
        }

        public async Task<bool> DeleteIfExistsAsync()
        {
            return await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blobContainer.DeleteIfExistsAsync());
        }

        public async Task CreateAsync(bool enablePublicAccess)
        {
            var accessType = enablePublicAccess ? PublicAccessType.Blob : PublicAccessType.None;

            await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blobContainer.CreateAsync(accessType));
        }

        internal CloudBlobClientWrapper Account => _account;
    }
}

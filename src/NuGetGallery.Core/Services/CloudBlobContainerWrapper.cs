// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
            BlobListContinuationToken blobContinuationToken,
            TimeSpan? requestTimeout,
            CloudBlobLocationMode? cloudBlobLocationMode,
            CancellationToken cancellationToken)
        {
            string continuationToken = blobContinuationToken?.ContinuationToken;

            BlobContainerClient blobContainerClient = _blobContainer;
            if (cloudBlobLocationMode.HasValue)
            {
                blobContainerClient = _account.CreateBlobContainerClient(cloudBlobLocationMode.Value, _blobContainer.Name, requestTimeout) ?? blobContainerClient;
            }
            else if (requestTimeout.HasValue)
            {
                blobContainerClient = _account.CreateBlobContainerClient(_blobContainer.Name, requestTimeout.Value);
            }

            BlobTraits traits = CloudWrapperHelpers.GetSdkBlobTraits(blobListingDetails);
            BlobStates states = CloudWrapperHelpers.GetSdkBlobStates(blobListingDetails);
            var enumerable = blobContainerClient
                .GetBlobsAsync(traits: traits, states: states, prefix: prefix, cancellationToken: cancellationToken)
                .AsPages(continuationToken, maxResults);

            var enumerator = enumerable.GetAsyncEnumerator(cancellationToken);
            try
            {
                if (await CloudWrapperHelpers.WrapStorageExceptionAsync(() => enumerator.MoveNextAsync().AsTask()))
                {
                    var page = enumerator.Current;
                    var nextContinuationToken = string.IsNullOrEmpty(page.ContinuationToken) ? null : page.ContinuationToken;
                    return new BlobResultSegmentWrapper(page.Values.Select(x => GetBlobReference(x)).ToList(), nextContinuationToken);
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            return new BlobResultSegmentWrapper(new List<ISimpleCloudBlob>(), null);
        }
        
        public async Task CreateIfNotExistAsync(bool enablePublicAccess)
        {
            var accessType = enablePublicAccess ? PublicAccessType.Blob : PublicAccessType.None;

            await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blobContainer.CreateIfNotExistsAsync(accessType));
        }

        public ISimpleCloudBlob GetBlobReference(string blobAddressUri)
        {
            return new CloudBlobWrapper(_blobContainer.GetBlockBlobClient(blobAddressUri), this);
        }

        private ISimpleCloudBlob GetBlobReference(BlobItem item)
        {
            return new CloudBlobWrapper(_blobContainer.GetBlockBlobClient(item.Name), item, this);
        }

        public async Task<bool> ExistsAsync(CloudBlobLocationMode? cloudBlobLocationMode)
        {
            BlobContainerClient containerClient = _blobContainer;
            if (cloudBlobLocationMode.HasValue)
            {
                containerClient = _account.CreateBlobContainerClient(cloudBlobLocationMode.Value, _blobContainer.Name) ?? containerClient;
            }
            return (await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                containerClient.ExistsAsync())).Value;
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

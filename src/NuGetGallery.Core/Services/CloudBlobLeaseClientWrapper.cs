// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Specialized;

namespace NuGetGallery
{
    public class CloudBlobLeaseClientWrapper : ICloudBlobLeaseClient
    {
        private BlobLeaseClient _blobLeaseClient;

        internal CloudBlobLeaseClientWrapper(BlobLeaseClient blobLeaseClient)
        {
            _blobLeaseClient = blobLeaseClient ?? throw new ArgumentNullException(nameof(blobLeaseClient));
        }

        public async Task<ICloudBlobLease> AcquireLeaseAsync(TimeSpan timeSpan)
        {
            var response = await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blobLeaseClient.AcquireAsync(timeSpan));
            if (!response.HasValue)
            {
                throw new InvalidOperationException("Failed to acquire lease.");
            }

            return new CloudBlobLeaseWrapper(response.Value);
        }

        public async Task<ICloudBlobLease> RenewLeaseAsync()
        {
            var response = await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blobLeaseClient.RenewAsync());
            if (!response.HasValue)
            {
                throw new InvalidOperationException("Failed to renew lease.");
            }

            return new CloudBlobLeaseWrapper(response.Value);
        }
    }
}

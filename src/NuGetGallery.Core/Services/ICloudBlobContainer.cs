// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface ICloudBlobContainer
    {
        Task CreateIfNotExistAsync(bool enablePublicAccess);
        ISimpleCloudBlob GetBlobReference(string blobAddressUri);
        Task<bool> ExistsAsync(CloudBlobLocationMode? cloudBlobLocationMode);
        Task<bool> DeleteIfExistsAsync();
        Task CreateAsync(bool enablePublicAccess);
        Task<ISimpleBlobResultSegment> ListBlobsSegmentedAsync(
            string prefix,
            bool useFlatBlobListing,
            ListingDetails blobListingDetails,
            int? maxResults,
            BlobListContinuationToken blobContinuationToken,
            TimeSpan? requestTimeout,
            CloudBlobLocationMode? cloudBlobLocationMode,
            CancellationToken cancellationToken);
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace NuGetGallery
{
    internal static class CloudWrapperHelpers
    {
        public static LocationMode GetSdkRetryPolicy(CloudBlobLocationMode locationMode)
        {
            switch (locationMode)
            {
                case CloudBlobLocationMode.PrimaryOnly:
                    return LocationMode.PrimaryOnly;
                case CloudBlobLocationMode.PrimaryThenSecondary:
                    return LocationMode.PrimaryThenSecondary;
                case CloudBlobLocationMode.SecondaryOnly:
                    return LocationMode.SecondaryOnly;
                case CloudBlobLocationMode.SecondaryThenPrimary:
                    return LocationMode.SecondaryThenPrimary;
                default:
                    throw new ArgumentOutOfRangeException(nameof(locationMode));
            }
        }

        public static BlobListingDetails GetBlobListingDetails(ListingDetails listingDetails) => (BlobListingDetails)listingDetails;

        public static CloudBlobCopyStatus GetBlobCopyStatus(CopyStatus status)
        {
            switch (status)
            {
                case CopyStatus.Invalid:
                    return CloudBlobCopyStatus.Invalid;
                case CopyStatus.Pending:
                    return CloudBlobCopyStatus.Pending;
                case CopyStatus.Success:
                    return CloudBlobCopyStatus.Success;
                case CopyStatus.Aborted:
                    return CloudBlobCopyStatus.Aborted;
                case CopyStatus.Failed:
                    return CloudBlobCopyStatus.Failed;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
        }
    }
}

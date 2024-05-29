// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
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

        public static BlobListingDetails GetSdkBlobListingDetails(ListingDetails listingDetails) => (BlobListingDetails)listingDetails;

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

        public static AccessCondition GetSdkAccessCondition(IAccessCondition accessCondition)
        {
            if (accessCondition == null)
            {
                return null;
            }

            return new AccessCondition { IfMatchETag = accessCondition.IfMatchETag, IfNoneMatchETag = accessCondition.IfNoneMatchETag };
        }

        public static SharedAccessBlobPermissions GetSdkSharedAccessPermissions(FileUriPermissions permissions)
            => (SharedAccessBlobPermissions)permissions;

        public static async Task<TResult> WrapStorageExceptionAsync<TResult>(Func<Task<TResult>> @delegate)
        {
            try
            {
                return await @delegate();
            }
            catch (StorageException ex) when (ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == BlobErrorCodeStrings.ContainerNotFound)
            {
                throw new CloudBlobContainerNotFoundException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation.ExtendedErrorInformation?.ErrorCode == BlobErrorCodeStrings.BlobNotFound)
            {
                throw new CloudBlobNotFoundException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                throw new CloudBlobGenericNotFoundException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                throw new CloudBlobConflictException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
            {
                throw new CloudBlobPreconditionFailedException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotModified)
            {
                throw new CloudBlobNotModifiedException(ex);
            }
            catch (StorageException ex)
            {
                throw new CloudBlobStorageException(ex);
            }
        }

        public static async Task WrapStorageExceptionAsync(Func<Task> @delegate)
        {
            try
            {
                await @delegate();
            }
            catch (StorageException ex) when (ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == BlobErrorCodeStrings.ContainerNotFound)
            {
                throw new CloudBlobContainerNotFoundException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation.ExtendedErrorInformation?.ErrorCode == BlobErrorCodeStrings.BlobNotFound)
            {
                throw new CloudBlobNotFoundException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                throw new CloudBlobGenericNotFoundException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                throw new CloudBlobConflictException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
            {
                throw new CloudBlobPreconditionFailedException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotModified)
            {
                throw new CloudBlobNotModifiedException(ex);
            }
            catch (StorageException ex)
            {
                throw new CloudBlobStorageException(ex);
            }
        }
    }
}

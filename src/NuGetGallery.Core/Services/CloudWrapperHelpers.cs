// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

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

        public static BlobTraits GetSdkBlobTraits(ListingDetails listingDetails)
        {
            BlobTraits traits = BlobTraits.None;
            if (listingDetails.HasFlag(ListingDetails.Metadata))
            {
                traits |= BlobTraits.Metadata;
            }
            if (listingDetails.HasFlag(ListingDetails.Copy))
            {
                traits |= BlobTraits.CopyStatus;
            }
            return traits;
        }

        public static BlobStates GetSdkBlobStates(ListingDetails listingDetails)
        {
            BlobStates states = BlobStates.None;
            if (listingDetails.HasFlag(ListingDetails.Snapshots))
            {
                states |= BlobStates.Snapshots;
            }
            if (listingDetails.HasFlag(ListingDetails.UncommittedBlobs))
            {
                states |= BlobStates.Uncommitted;
            }
            if (listingDetails.HasFlag(ListingDetails.Deleted))
            {
                states |= BlobStates.Deleted;
            }
            return states;
        }

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

        public static BlobRequestConditions GetSdkAccessCondition(IAccessCondition accessCondition)
        {
            if (accessCondition == null)
            {
                return null;
            }

            return new BlobRequestConditions
            {
                IfMatch = string.IsNullOrEmpty(accessCondition.IfMatchETag) ? (ETag?)null : new Azure.ETag(accessCondition.IfMatchETag),
                IfNoneMatch = string.IsNullOrEmpty(accessCondition.IfNoneMatchETag) ? (ETag?)null : new Azure.ETag(accessCondition.IfNoneMatchETag),
            };
        }

        public static BlobAccountSasPermissions GetSdkSharedAccessPermissions(FileUriPermissions permissions)
        {
            BlobAccountSasPermissions convertedPermissions = (BlobAccountSasPermissions)0;
            if (permissions.HasFlag(FileUriPermissions.Read))
            {
                convertedPermissions |= BlobAccountSasPermissions.Read;
            }
            if (permissions.HasFlag(FileUriPermissions.Write))
            {
                convertedPermissions |= BlobAccountSasPermissions.Write;
            }
            if (permissions.HasFlag(FileUriPermissions.Delete))
            {
                convertedPermissions |= BlobAccountSasPermissions.Delete;
            }
            if (permissions.HasFlag(FileUriPermissions.List))
            {
                convertedPermissions |= BlobAccountSasPermissions.List;
            }
            if (permissions.HasFlag(FileUriPermissions.Add))
            {
                convertedPermissions |= BlobAccountSasPermissions.Add;
            }
            if (permissions.HasFlag(FileUriPermissions.Create))
            {
                convertedPermissions |= BlobAccountSasPermissions.Create;
            }
            return convertedPermissions;
        }

        public static async Task<TResult> WrapStorageExceptionAsync<TResult>(Func<Task<TResult>> @delegate)
        {
            try
            {
                return await @delegate();
            }
            catch (StorageException ex) when (ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == BlobErrorCodeStrings.ContainerNotFound || ex.RequestInformation?.ErrorCode == BlobErrorCodeStrings.ContainerNotFound)
            {
                throw new CloudBlobContainerNotFoundException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == BlobErrorCodeStrings.BlobNotFound || ex.RequestInformation?.ErrorCode == BlobErrorCodeStrings.BlobNotFound)
            {
                throw new CloudBlobNotFoundException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                throw new CloudBlobGenericNotFoundException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                throw new CloudBlobConflictException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
            {
                throw new CloudBlobPreconditionFailedException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotModified)
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
            catch (StorageException ex) when (ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == BlobErrorCodeStrings.ContainerNotFound || ex.RequestInformation?.ErrorCode == BlobErrorCodeStrings.ContainerNotFound)
            {
                throw new CloudBlobContainerNotFoundException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == BlobErrorCodeStrings.BlobNotFound || ex.RequestInformation?.ErrorCode == BlobErrorCodeStrings.BlobNotFound)
            {
                throw new CloudBlobNotFoundException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                throw new CloudBlobGenericNotFoundException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                throw new CloudBlobConflictException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
            {
                throw new CloudBlobPreconditionFailedException(ex);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotModified)
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

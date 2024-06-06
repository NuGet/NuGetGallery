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

        public static CloudBlobCopyStatus GetBlobCopyStatus(CopyStatus? status)
        {
            if (!status.HasValue)
            {
                return CloudBlobCopyStatus.None;
            }
            switch (status.Value)
            {
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
            catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ContainerNotFound)
            {
                throw new CloudBlobContainerNotFoundException(ex);
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
            {
                throw new CloudBlobNotFoundException(ex);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                throw new CloudBlobGenericNotFoundException(ex);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                throw new CloudBlobConflictException(ex);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
            {
                throw new CloudBlobPreconditionFailedException(ex);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotModified)
            {
                throw new CloudBlobNotModifiedException(ex);
            }
            catch (RequestFailedException ex)
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
            catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ContainerNotFound)
            {
                throw new CloudBlobContainerNotFoundException(ex);
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
            {
                throw new CloudBlobNotFoundException(ex);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                throw new CloudBlobGenericNotFoundException(ex);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                throw new CloudBlobConflictException(ex);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
            {
                throw new CloudBlobPreconditionFailedException(ex);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotModified)
            {
                throw new CloudBlobNotModifiedException(ex);
            }
            catch (RequestFailedException ex)
            {
                throw new CloudBlobStorageException(ex);
            }
        }
    }
}

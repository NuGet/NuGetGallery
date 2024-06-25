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
            if ((listingDetails & ListingDetails.Metadata) != 0)
            {
                traits |= BlobTraits.Metadata;
            }
            if ((listingDetails & ListingDetails.Copy) != 0)
            {
                traits |= BlobTraits.CopyStatus;
            }
            return traits;
        }

        public static BlobStates GetSdkBlobStates(ListingDetails listingDetails)
        {
            BlobStates states = BlobStates.None;
            if ((listingDetails & ListingDetails.Snapshots) != 0)
            {
                states |= BlobStates.Snapshots;
            }
            if ((listingDetails & ListingDetails.UncommittedBlobs) != 0)
            {
                states |= BlobStates.Uncommitted;
            }
            if ((listingDetails & ListingDetails.Deleted) != 0)
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
            if ((permissions & FileUriPermissions.Read) != 0)
            {
                convertedPermissions |= BlobAccountSasPermissions.Read;
            }
            if ((permissions & FileUriPermissions.Write) != 0)
            {
                convertedPermissions |= BlobAccountSasPermissions.Write;
            }
            if ((permissions & FileUriPermissions.Delete) != 0)
            {
                convertedPermissions |= BlobAccountSasPermissions.Delete;
            }
#pragma warning disable CS0612
            if ((permissions & FileUriPermissions.List) != 0)
#pragma warning restore CS0612
            {
                convertedPermissions |= BlobAccountSasPermissions.List;
            }
#pragma warning disable CS0612
            if ((permissions & FileUriPermissions.Add) != 0)
#pragma warning restore CS0612
            {
                convertedPermissions |= BlobAccountSasPermissions.Add;
            }
            if ((permissions & FileUriPermissions.Create) != 0)
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

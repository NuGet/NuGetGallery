// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace NuGet.Services.Storage
{
    /// <summary>
    /// This implementation uses Azure Blob leases which have a minimum lease duration of 15 seconds and a maximum
    /// duration of 60 seconds. For more information about Azure Storage blob leasing, refer to the documentation:
    /// https://docs.microsoft.com/en-us/rest/api/storageservices/lease-blob
    /// </summary>
    public class BlobLeaseService : IBlobLeaseService
    {
        private static readonly TimeSpan MinLeaseTime = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan MaxLeaseTime = TimeSpan.FromSeconds(60);

        private readonly BlobContainerClient _containerClient;
        private readonly string _basePath;

        public BlobLeaseService(BlobServiceClient blobServiceClient, string containerName, string basePath)
        {
            if (blobServiceClient == null)
            {
                throw new ArgumentNullException(nameof(blobServiceClient));
            }
            if (string.IsNullOrEmpty(containerName))
            {
                throw new ArgumentException("The container name must be provided.", nameof(containerName));
            }
            if (string.IsNullOrEmpty(basePath))
            {
                throw new ArgumentException("The base path must be provided.", nameof(basePath));
            }

            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            _basePath = string.IsNullOrEmpty(basePath) ? string.Empty : basePath.TrimEnd('/') + '/';
        }

        public async Task<BlobLeaseResult> TryAcquireAsync(string resourceName, TimeSpan leaseTime, CancellationToken cancellationToken)
        {
            var blob = GetBlob(resourceName);

            try
            {
                return await TryAcquireAsync(blob, leaseTime, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                // The lease file does not exist. Try to create it and lease it.
                return await TryCreateAndAcquireAsync(blob, leaseTime, cancellationToken);
            }
        }

        public async Task<bool> ReleaseAsync(string resourceName, string leaseId, CancellationToken cancellationToken)
        {
            try
            {
                var blob = GetBlob(resourceName);
                var leaseClient = blob.GetBlobLeaseClient(leaseId);
                await leaseClient.ReleaseAsync(conditions: null, cancellationToken: cancellationToken);

                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                return false;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
            {
                // There was no lease to release--swallow the exception and fail gracefully.
                return true;
            }
        }

        public async Task<BlobLeaseResult> RenewAsync(string resourceName, string leaseId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(leaseId))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leaseId),
                    "The lease Id must be provided for renewing the lease.");
            }

            var blob = GetBlob(resourceName);
            var leaseClient = blob.GetBlobLeaseClient(leaseId);

            try
            {
                var lease = await leaseClient.RenewAsync(conditions: null, cancellationToken: cancellationToken);
                return BlobLeaseResult.Success(lease);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict || ex.Status == (int)HttpStatusCode.PreconditionFailed)
            {
                // We need to return a failure here for conflicts as well as precondition failure (i.e. the leaseId doesn't exist)--for precondition
                // failure, we can't fail silently because the renew will have thrown.
                return BlobLeaseResult.Failure();
            }
        }

        private BlobClient GetBlob(string resourceName) => _containerClient.GetBlobClient($"{_basePath}{resourceName}");

        private async Task<BlobLeaseResult> TryCreateAndAcquireAsync(BlobClient blob, TimeSpan leaseTime, CancellationToken cancellationToken)
        {
            try
            {
                // Use an empty blob for the lease blob. The contents are not important. Only the lease state (managed
                // by Azure Blob Storage) is important.
                await blob.UploadAsync(new BinaryData(Array.Empty<byte>()), cancellationToken: cancellationToken);
                return await TryAcquireAsync(blob, leaseTime, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
            {
                // The file has already created and leased by someone else.
                return BlobLeaseResult.Failure();
            }
        }

        private async Task<BlobLeaseResult> TryAcquireAsync(BlobClient blob, TimeSpan leaseTime, CancellationToken cancellationToken)
        {
            if (leaseTime < MinLeaseTime || leaseTime > MaxLeaseTime)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leaseTime),
                    "The lease time must be between 15 and 60 seconds, inclusive.");
            }

            try
            {
                var leaseClient = blob.GetBlobLeaseClient();
                var blobLease = await leaseClient.AcquireAsync(leaseTime, conditions: null, cancellationToken: cancellationToken);
                return BlobLeaseResult.Success(blobLease);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                // The lease has already been acquired by someone else.
                return BlobLeaseResult.Failure();
            }
        }
    }
}

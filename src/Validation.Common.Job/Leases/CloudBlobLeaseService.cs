// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Blobs.Models;

namespace NuGet.Jobs.Validation.Leases
{
    /// <summary>
    /// This implementation uses Azure Blob leases which have a minimum lease duration of 15 seconds and a maximum
    /// duration of 60 seconds. For more information about Azure Storage blob leasing, refer to the documentation:
    /// https://docs.microsoft.com/en-us/rest/api/storageservices/lease-blob
    /// </summary>
    public class CloudBlobLeaseService : ILeaseService
    {
        private static readonly TimeSpan MinLeaseTime = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan MaxLeaseTime = TimeSpan.FromSeconds(60);

        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;
        private readonly string _basePath;

        public CloudBlobLeaseService(BlobServiceClient blobServiceClient, string containerName, string basePath)
        {
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
            _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
            _basePath = string.IsNullOrEmpty(basePath) ? string.Empty : basePath.TrimEnd('/') + '/';
        }

        public async Task<LeaseResult> TryAcquireAsync(string resourceName, TimeSpan leaseTime, CancellationToken cancellationToken)
        {
            BlobClient blob = GetBlob(resourceName);
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

        private BlobClient GetBlob(string resourceName)
        {
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            return containerClient.GetBlobClient($"{_basePath}{resourceName}");
        }

        public async Task<bool> ReleaseAsync(string resourceName, string leaseId, CancellationToken cancellationToken)
        {
            BlobClient blob = GetBlob(resourceName);
            try
            {
                BlobLeaseClient leaseClient = blob.GetBlobLeaseClient(leaseId);
                await leaseClient.ReleaseAsync(cancellationToken: cancellationToken);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                return false;
            }
        }

        public async Task<LeaseResult> RenewAsync(string resourceName, string leaseId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(leaseId))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leaseId),
                    "The lease Id must be provided for renewing the lease.");
            }

            BlobClient blob = GetBlob(resourceName);
            BlobLeaseClient leaseClient = blob.GetBlobLeaseClient(leaseId);

            try
            {
                await leaseClient.RenewAsync(cancellationToken: cancellationToken);
                return LeaseResult.Success(leaseId);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                return LeaseResult.Failure();
            }
        }

        private async Task<LeaseResult> TryCreateAndAcquireAsync(BlobClient blob, TimeSpan leaseTime, CancellationToken cancellationToken)
        {
            try
            {
                // Use an empty blob for the lease blob. The contents are not important. Only the lease state (managed
                // by Azure Blob Storage) is important.
                await blob.UploadAsync(
                    new BinaryData(Array.Empty<byte>()),
                    overwrite: true,
                    cancellationToken: cancellationToken);

                return await TryAcquireAsync(blob, leaseTime, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
            {
                // The file has already been created and leased by someone else.
                return LeaseResult.Failure();
            }
        }

        private async Task<LeaseResult> TryAcquireAsync(BlobClient blob, TimeSpan leaseTime, CancellationToken cancellationToken)
        {
            if (leaseTime < MinLeaseTime || leaseTime > MaxLeaseTime)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leaseTime),
                    "The lease time must be between 15 and 60 seconds, inclusive.");
            }

            try
            {
                BlobLeaseClient leaseClient = blob.GetBlobLeaseClient();
                Response<BlobLease> leaseResponse = await leaseClient.AcquireAsync(
                    duration: leaseTime,
                    cancellationToken: cancellationToken);

                return LeaseResult.Success(leaseResponse.Value.LeaseId);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                // The lease has already been acquired by someone else.
                return LeaseResult.Failure();
            }
        }
    }
}

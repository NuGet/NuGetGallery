// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

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

        private readonly CloudBlobClient _cloudBlobClient;
        private readonly string _containerName;
        private readonly string _basePath;

        public CloudBlobLeaseService(CloudBlobClient cloudBlobClient, string containerName, string basePath)
        {
            _cloudBlobClient = cloudBlobClient ?? throw new ArgumentNullException(nameof(cloudBlobClient));
            _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
            _basePath = string.IsNullOrEmpty(basePath) ? string.Empty : basePath.TrimEnd('/') + '/';
        }

        public async Task<LeaseResult> TryAcquireAsync(string resourceName, TimeSpan leaseTime, CancellationToken cancellationToken)
        {
            var blob = GetBlob(resourceName);
            try
            {
                return await TryAcquireAsync(blob, leaseTime, cancellationToken);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                // The lease file does not exist. Try to create it and lease it.
                return await TryCreateAndAcquireAsync(blob, leaseTime, cancellationToken);
            }
        }

        private CloudBlockBlob GetBlob(string resourceName)
        {
            var container = _cloudBlobClient.GetContainerReference(_containerName);
            return container.GetBlockBlobReference($"{_basePath}{resourceName}");
        }

        public async Task<bool> ReleaseAsync(string resourceName, string leaseId, CancellationToken cancellationToken)
        {
            var blob = GetBlob(resourceName);
            try
            {
                await blob.ReleaseLeaseAsync(
                    AccessCondition.GenerateLeaseCondition(leaseId),
                    options: null,
                    operationContext: null,
                    cancellationToken);
                return true;
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
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

            var blob = GetBlob(resourceName);

            try
            {
                await blob.RenewLeaseAsync(
                    AccessCondition.GenerateLeaseCondition(leaseId),
                    options: null,
                    operationContext: null,
                    cancellationToken);

                return LeaseResult.Success(leaseId); ;
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                return LeaseResult.Failure(); ;
            }
        }

        private async Task<LeaseResult> TryCreateAndAcquireAsync(CloudBlockBlob blob, TimeSpan leaseTime, CancellationToken cancellationToken)
        {
            try
            {
                // Use an empty blob for the lease blob. The contents are not important. Only the lease state (managed
                // by Azure Blob Storage) is important.
                await blob.UploadFromByteArrayAsync(
                    Array.Empty<byte>(),
                    index: 0,
                    count: 0,
                    accessCondition: null,
                    options: null,
                    operationContext: null,
                    cancellationToken: cancellationToken);

                return await TryAcquireAsync(blob, leaseTime, cancellationToken);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
            {
                // The file has already created and leased by someone else.
                return LeaseResult.Failure();
            }
        }

        private async Task<LeaseResult> TryAcquireAsync(CloudBlockBlob blob, TimeSpan leaseTime, CancellationToken cancellationToken)
        {
            if (leaseTime < MinLeaseTime || leaseTime > MaxLeaseTime)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leaseTime),
                    "The lease time must be between 15 and 60 seconds, inclusive.");
            }

            try
            {
                var leaseId = await blob.AcquireLeaseAsync(
                    leaseTime: leaseTime,
                    proposedLeaseId: null,
                    accessCondition: null,
                    options: null,
                    operationContext: null,
                    cancellationToken: cancellationToken);

                return LeaseResult.Success(leaseId);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                // The lease has already been acquired by someone else.
                return LeaseResult.Failure();
            }
        }
    }
}

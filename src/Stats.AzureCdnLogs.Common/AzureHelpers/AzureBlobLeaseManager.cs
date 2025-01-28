// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Stats.AzureCdnLogs.Common.Collect;
using NuGet.Services.Storage;
using Azure.Storage.Blobs.Specialized;
//using Microsoft.WindowsAzure.Storage;
//using Microsoft.WindowsAzure.Storage.Blob;

namespace Stats.AzureCdnLogs.Common
{
    /// <summary>
    /// Manages lease acquisition on Azure Blobs.
    /// A lease, once is acquired, it will be continuously renewed every 60 minutes.
    /// </summary>
    public class AzureBlobLeaseManager
    {
        public const int MaxRenewPeriodInSeconds = 60;
        // The lease will be renewed with a short interval before the the lease expires
        public const int OverlapRenewPeriodInSeconds = 20;
        //private BlobRequestOptions _blobRequestOptions;
        private readonly ILogger<AzureBlobLeaseManager> _logger;
        private BlobLeaseService _blobLeaseService;

        public AzureBlobLeaseManager(ILogger<AzureBlobLeaseManager> logger, BlobServiceClient blobServiceClient, string containerName, string basePath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (blobServiceClient == null) throw new ArgumentNullException(nameof(blobServiceClient));

            if (string.IsNullOrEmpty(containerName))
            {
                if (containerName == null) throw new ArgumentNullException(nameof(containerName));
                else throw new ArgumentException(nameof(containerName));
            }


            _blobLeaseService = new BlobLeaseService(blobServiceClient, containerName, basePath, logger);
        }

        /// <summary>
        /// Try to acquire a lease on the blob. If the acquire is successful the lease will be renewed at every 60 seconds. 
        /// In order to stop the renew task the <see cref="Stats.AzureCdnLogs.Common.AzureBlobLeaseManager.TryReleaseLease(CloudBlob)"/> needs to be invoked
        /// or the token to be cancelled.
        /// </summary>
        /// <param name="blob">The blob to acquire the lease on.</param>
        /// <param name="token">A token to cancel the operation.</param>
        /// <param name="renewStatusTask">The renew task.</param>
        /// <returns>True if the lease was acquired. </returns>
        public async Task<AzureBlobLockResult> AcquireLease(BlobClient blob)
        {
            try
            {
                var leaseResult = await _blobLeaseService.TryAcquireAsync(blob.Name, TimeSpan.FromSeconds(MaxRenewPeriodInSeconds), CancellationToken.None);
                if (!leaseResult.IsSuccess)
                {
                    _logger.LogInformation("AcquireLease: The operation was cancelled or the blob lease is already taken. Blob {BlobUri}.", blob.Uri.AbsoluteUri);
                    return AzureBlobLockResult.FailedLockResult(blob);
                }
                else
                {
                    _logger.LogInformation("AcquireLease: Lease was acquired for BlobUri {BlobUri}.", blob.Uri.AbsoluteUri);
                }

                    var leaseId = leaseResult.LeaseId;
                var lockResult = new AzureBlobLockResult(blob, true, leaseId, CancellationToken.None);
                var blob1 = lockResult.Blob;
                // Start a task that will renew the lease until the token is cancelled or the Release method is invoked
                _ = Task.Run(async () =>
                {
                        while (!lockResult.BlobOperationToken.Token.IsCancellationRequested)
                        {
                            try
                            {
                            var delay1 = TimeSpan.FromSeconds(MaxRenewPeriodInSeconds - OverlapRenewPeriodInSeconds);
                                await Task.Delay(TimeSpan.FromSeconds(MaxRenewPeriodInSeconds - OverlapRenewPeriodInSeconds), lockResult.BlobOperationToken.Token);
                            if (! await blob1.ExistsAsync())
                            {
                                break;
                            }
                            await _blobLeaseService.RenewAsync(blob.Name, leaseId, CancellationToken.None);
                                _logger.LogInformation("RenewLeaseTask: Lease was renewed for BlobUri {BlobUri} and LeaseId {LeaseId}.",
                                    blob.Uri.AbsoluteUri,
                                    leaseId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "RenewLeaseTask: The Lease could not be renewed for BlobUri {BlobUri}. LeaseId {LeaseId}.",
                                    blob.Uri.AbsoluteUri,
                                    leaseId);
                                lockResult.BlobOperationToken.Cancel();
                                break;
                            }
                        }
                    
                    
                   
                }, lockResult.BlobOperationToken.Token);
                return lockResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AcquireLeaseAsync: Failed to acquire lease for BlobUri {BlobUri}.", blob.Uri.AbsoluteUri);
                return AzureBlobLockResult.FailedLockResult(blob);
            }

        }

        public async Task<AsyncOperationResult> TryReleaseLockAsync(AzureBlobLockResult releaseLock)
        {
            try
            {
                if(await releaseLock.Blob.ExistsAsync())
                {
                    bool releaseResult = await _blobLeaseService.ReleaseAsync(releaseLock.Blob.Name, releaseLock.LeaseId, releaseLock.BlobOperationToken.Token);
                    if (releaseResult)
                    {
                        _logger.LogInformation("ReleaseLockAsync: ReleaseLeaseStatus: {LeaseReleased} on the {BlobUri}.", true, releaseLock.Blob.Uri);
                        return new AsyncOperationResult(true, null);
                    }
                    else
                    {
                        return new AsyncOperationResult(false, null);
                    }
                }
                else
                {
                    return new AsyncOperationResult(false, null);
                }
            }
            catch (Exception exception)
            {
                // If it  fails do not do anything - the lease will be released in 1 minute anyway
                _logger.LogWarning(LogEvents.FailedBlobReleaseLease, exception, "ReleaseLockAsync: Release lease failed for {BlobUri}.", releaseLock.Blob.Uri);
                return new AsyncOperationResult(null, exception);
            }
        }
    }
}

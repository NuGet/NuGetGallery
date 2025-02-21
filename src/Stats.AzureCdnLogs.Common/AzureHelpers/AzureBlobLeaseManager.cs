// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using NuGet.Services.Storage;
using Stats.AzureCdnLogs.Common.Collect;

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
        private readonly ILogger<AzureBlobLeaseManager> _logger;

        public AzureBlobLeaseManager(ILogger<AzureBlobLeaseManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Try to acquire a lease on the blob. If the acquire is successful the lease will be renewed at every 60 seconds. 
        /// In order to stop the renew task the <see cref="Stats.AzureCdnLogs.Common.AzureBlobLeaseManager.TryReleaseLockAsync(AzureBlobLockResult)"/> needs to be invoked
        /// or the token to be cancelled.
        /// </summary>
        /// <param name="blob">The blob to acquire the lease on.</param>
        /// <returns>An <see cref="AzureBlobLockResult"/> indicating the result of the lease acquisition. 
        /// If the lease is successfully acquired, the result will contain the lease ID and a cancellation token 
        /// source that can be used to stop the lease renewal task.</returns>
        public async Task<AzureBlobLockResult> AcquireLease(BlobClient blob, CancellationToken token)
        {
            try
            {
                var leaseClient = blob.GetBlobLeaseClient();
                var leaseResponse = await leaseClient.AcquireAsync(TimeSpan.FromSeconds(MaxRenewPeriodInSeconds));
                string leaseId = leaseResponse.Value.LeaseId;
                var lockResult = new AzureBlobLockResult(blob, lockIsTaken: true, leaseId, token);
                BlobClient leasedBlob = lockResult.Blob;
                // Start a task that will renew the lease until the token is cancelled or the Release method is invoked
                _ = Task.Run(async () =>
                {

                    int sleepBeforeRenewInSeconds = MaxRenewPeriodInSeconds - OverlapRenewPeriodInSeconds < 0 ? MaxRenewPeriodInSeconds : MaxRenewPeriodInSeconds - OverlapRenewPeriodInSeconds;

                    while (!lockResult.BlobOperationToken.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(sleepBeforeRenewInSeconds));
                            if (!await leasedBlob.ExistsAsync())
                            {
                                break;
                            }
                            await leaseClient.RenewAsync();
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
                if (await releaseLock.Blob.ExistsAsync())
                {
                    var leaseClient = releaseLock.Blob.GetBlobLeaseClient(releaseLock.LeaseId);
                    await leaseClient.ReleaseAsync();
                    _logger.LogInformation("ReleaseLockAsync: ReleaseLeaseStatus: {LeaseReleased} on the {BlobUri}.", true, releaseLock.Blob.Uri);
                    return new AsyncOperationResult(true, null);
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

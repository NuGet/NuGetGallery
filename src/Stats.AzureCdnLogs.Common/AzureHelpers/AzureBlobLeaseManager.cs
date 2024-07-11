// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

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
        private BlobRequestOptions _blobRequestOptions;
        private readonly ILogger<AzureBlobLeaseManager> _logger;

        public AzureBlobLeaseManager(ILogger<AzureBlobLeaseManager> logger, BlobRequestOptions blobRequestOptions = null)
        {
            _blobRequestOptions = blobRequestOptions;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        public AzureBlobLockResult AcquireLease(CloudBlob blob, CancellationToken token)
        {
            blob.FetchAttributes();
            if (token.IsCancellationRequested || blob.Properties.LeaseStatus == LeaseStatus.Locked)
            {
                _logger.LogInformation("AcquireLease: The operation was cancelled or the blob lease is already taken. Blob {BlobUri}, Cancellation status {IsCancellationRequested}, BlobLeaseStatus {BlobLeaseStatus}.",
                    blob.Uri.AbsoluteUri,
                    token.IsCancellationRequested,
                    blob.Properties.LeaseStatus);
                return AzureBlobLockResult.FailedLockResult(blob);
            }
            var proposedLeaseId = Guid.NewGuid().ToString();
            var leaseId = blob.AcquireLease(TimeSpan.FromSeconds(MaxRenewPeriodInSeconds), proposedLeaseId);
            var lockResult = new AzureBlobLockResult(blob: blob, lockIsTaken: true, leaseId: leaseId, linkToken: token);

            //start a task that will renew the lease until the token is cancelled or the Release methods was invoked
            var renewStatusTask = new Task( (lockresult) =>
                {
                    var blobLockResult = (AzureBlobLockResult)lockresult;
                    _logger.LogInformation("RenewLeaseTask: Started for BlobUri {BlobUri}. ThreadId {ThreadId}. IsCancellationRequested {IsCancellationRequested}. LeaseId {LeaseId}", 
                        blob.Uri.AbsoluteUri,
                        Thread.CurrentThread.ManagedThreadId,
                        blobLockResult.BlobOperationToken.IsCancellationRequested,
                        blobLockResult.LeaseId);

                    int sleepBeforeRenewInSeconds = MaxRenewPeriodInSeconds - OverlapRenewPeriodInSeconds < 0 ? MaxRenewPeriodInSeconds : MaxRenewPeriodInSeconds - OverlapRenewPeriodInSeconds;
                    if (!blobLockResult.BlobOperationToken.IsCancellationRequested)
                    {
                        while (!blobLockResult.BlobOperationToken.Token.IsCancellationRequested)
                        {
                            Thread.Sleep(sleepBeforeRenewInSeconds * 1000);

                            //it will renew the lease only if the lease was not explicitly released 
                            try
                            {
                                if (!blobLockResult.Blob.Exists())
                                {
                                    blobLockResult.BlobOperationToken.Cancel();
                                    break;
                                }
                                AccessCondition acc = new AccessCondition { LeaseId = blobLockResult.LeaseId };
                                blob.RenewLease(accessCondition: acc, options: _blobRequestOptions, operationContext: null);
                                _logger.LogInformation("RenewLeaseTask: Lease was renewed for BlobUri {BlobUri} and LeaseId {LeaseId}.",
                                    blob.Uri.AbsoluteUri,
                                    blobLockResult.LeaseId);
                            }
                            catch (StorageException exception)
                            {
                                _logger.LogWarning(LogEvents.FailedBlobLease, exception, "RenewLeaseTask: The Lease could not be renewed for BlobUri {BlobUri}. ExpectedLeaseId {LeaseId}. CurrentLeaseId {CurrentLeaseId}.",
                                    blob.Uri.AbsoluteUri,
                                    leaseId,
                                    blobLockResult.LeaseId);
                                blobLockResult.BlobOperationToken.Cancel();
                                break;
                            }
                        }
                    }
                }, lockResult, TaskCreationOptions.LongRunning);
            renewStatusTask.Start();
            return lockResult;
        }

        public async Task<AsyncOperationResult> TryReleaseLockAsync(AzureBlobLockResult releaseLock)
        {
            try
            {
                AccessCondition acc = new AccessCondition();
                acc.LeaseId = releaseLock.LeaseId;
                if(await releaseLock.Blob.ExistsAsync())
                {
                    await releaseLock.Blob.ReleaseLeaseAsync(acc, options: _blobRequestOptions, operationContext: null);
                    releaseLock.BlobOperationToken.Cancel();
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

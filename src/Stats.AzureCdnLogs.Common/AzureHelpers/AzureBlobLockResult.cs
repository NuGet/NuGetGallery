// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Stats.AzureCdnLogs.Common
{
    public class AzureBlobLockResult : IDisposable
    {
        public bool LockIsTaken { get; }

        public string LeaseId { get; }

        public BlobClient Blob { get; }

        public BlobProperties BlobProperties { get; }

        /// <summary>
        /// It will be cancelled when the renew task could not renew the lease.
        /// Operations can listen to this cancellation to stop execution once the lease could not be renewed.
        /// </summary>
        public CancellationTokenSource BlobOperationToken { get; }

        public AzureBlobLockResult(BlobClient blob, bool lockIsTaken, string leaseId, CancellationToken linkToken)
            :this(blob, blobProperties: null, lockIsTaken, leaseId, linkToken)
        {
        }

        public AzureBlobLockResult(BlobClient blob, BlobProperties blobProperties, bool lockIsTaken, string leaseId, CancellationToken linkToken)
        {
            Blob = blob ?? throw new ArgumentNullException(nameof(blob));
            LockIsTaken = lockIsTaken;
            BlobOperationToken = CancellationTokenSource.CreateLinkedTokenSource(linkToken);
            // null is acceptable
            LeaseId = leaseId;
            BlobProperties = blobProperties;
        }

        public static AzureBlobLockResult FailedLockResult(BlobClient blob)
        {
            return new AzureBlobLockResult(blob: blob, lockIsTaken: false, leaseId: null, linkToken: CancellationToken.None);
        }

        public void Dispose()
        {
            BlobOperationToken.Dispose();
        }
    }
}

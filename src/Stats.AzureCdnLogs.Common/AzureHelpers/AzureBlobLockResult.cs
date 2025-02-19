// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Stats.AzureCdnLogs.Common
{
    public class AzureBlobLockResult : IDisposable
    {
        public bool LockIsTaken { get; }

        public string LeaseId { get; }

        public CloudBlob Blob { get; }

        /// <summary>
        /// It will be cancelled when the renew task could not renew the lease.
        /// Operations can listen to this cancellation to stop execution once the lease could not be renewed.
        /// </summary>
        public CancellationTokenSource BlobOperationToken { get; }

        public AzureBlobLockResult(CloudBlob blob, bool lockIsTaken, string leaseId, CancellationToken linkToken)
        {
            Blob = blob ?? throw new ArgumentNullException(nameof(blob));
            LockIsTaken = lockIsTaken;
            BlobOperationToken = CancellationTokenSource.CreateLinkedTokenSource(linkToken);
            // null is acceptable
            LeaseId = leaseId;
        }

        public static AzureBlobLockResult FailedLockResult(CloudBlob blob)
        {
            return new AzureBlobLockResult(blob: blob, lockIsTaken: false, leaseId: null, linkToken: CancellationToken.None);
        }

        public void Dispose()
        {
            BlobOperationToken.Dispose();
        }
    }
}

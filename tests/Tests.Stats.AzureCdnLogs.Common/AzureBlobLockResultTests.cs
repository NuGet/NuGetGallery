// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.WindowsAzure.Storage.Blob;
using Stats.AzureCdnLogs.Common;
using Xunit;

namespace Tests.Stats.AzureCdnLogs.Common
{
    public class AzureBlobLockResultTests
    {

        [Fact]
        public void ConstructorNullArgumentsTest()
        {
            Assert.Throws<ArgumentNullException>(() => new AzureBlobLockResult(blob: null, lockIsTaken: false,leaseId: string.Empty, linkToken: CancellationToken.None));
        }

        [Fact]
        public void VerifyThatTheTokenRespectsExternalCancellation()
        {
            // Arrange 
            var cts = new CancellationTokenSource();
            var testAzureBlobLockResult = new AzureBlobLockResult(blob: new CloudBlob(new Uri("https://test")), lockIsTaken: false, leaseId: string.Empty, linkToken: cts.Token);

            // Act
            cts.Cancel();

            // Verify
            Assert.True(testAzureBlobLockResult.BlobOperationToken.IsCancellationRequested);
        }

        [Fact]
        public void VerifyThatTheTokenCancellationDoesNotAffectExternalLinkedToken()
        {
            // Arrange 
            var cts = new CancellationTokenSource();
            var externalToken = cts.Token;
            var testAzureBlobLockResult = new AzureBlobLockResult(blob: new CloudBlob(new Uri("https://test")), lockIsTaken: false, leaseId: string.Empty, linkToken: externalToken);

            // Act
            testAzureBlobLockResult.BlobOperationToken.Cancel();

            // Verify
            Assert.False(externalToken.IsCancellationRequested);
        }
    }
}

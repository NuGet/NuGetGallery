﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using NuGet.Jobs.Validation.Leases;
using Xunit;
using Xunit.Abstractions;

namespace Validation.Common.Job.Tests.Leases
{
    [Collection(nameof(BlobStorageCollection))]
    public class CloudBlobLeaseServiceIntegrationTests
    {
        public CloudBlobLeaseServiceIntegrationTests(BlobStorageFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            Output = output ?? throw new ArgumentNullException(nameof(output));

            BlobServiceClient = new BlobServiceClient(Fixture.ConnectionString);
            LeaseTime = TimeSpan.FromSeconds(60);
            Token = CancellationToken.None;

            Target = new CloudBlobLeaseService(BlobServiceClient, fixture.TestRunId, basePath: null);
        }

        public BlobStorageFixture Fixture { get; }
        public ITestOutputHelper Output { get; }
        public BlobServiceClient BlobServiceClient { get; }
        public TimeSpan LeaseTime { get; }
        public CancellationToken Token { get; }
        public CloudBlobLeaseService Target { get; }

        [BlobStorageFact]
        public async Task AllowsSingleThreadToAcquireReleaseAcquire()
        {
            var resource = Guid.NewGuid().ToString();

            var acquireA = await Target.TryAcquireAsync(resource, LeaseTime, Token);
            Assert.True(acquireA.IsSuccess, "The lease should be acquired.");

            var released = await Target.ReleaseAsync(resource, acquireA.LeaseId, Token);
            Assert.True(released, "The lease should have been gracefully released.");

            var acquireB = await Target.TryAcquireAsync(resource, LeaseTime, Token);
            Assert.True(acquireB.IsSuccess, "The lease should be re-acquired.");
        }

        [BlobStorageFact]
        public async Task DoesNotAllowTwoLeasesAtOnce()
        {
            var resource = Guid.NewGuid().ToString();

            var acquireA = await Target.TryAcquireAsync(resource, LeaseTime, Token);
            Assert.True(acquireA.IsSuccess, "The lease should be acquired.");

            var acquireB = await Target.TryAcquireAsync(resource, LeaseTime, Token);
            Assert.False(acquireB.IsSuccess, "The lease should be not be acquired.");
        }

        [BlobStorageFact]
        public async Task AllowsTwoDifferentResourcesToBeLeasedInParallel()
        {
            var resourceA = Guid.NewGuid().ToString();
            var resourceB = Guid.NewGuid().ToString();

            var acquireA = await Target.TryAcquireAsync(resourceA, LeaseTime, Token);
            Assert.True(acquireA.IsSuccess, "The lease should be acquired.");

            var acquireB = await Target.TryAcquireAsync(resourceB, LeaseTime, Token);
            Assert.True(acquireB.IsSuccess, "The lease should be re-acquired.");
        }

        [BlobStorageFact]
        public async Task AllowsAcquireAfterLeaseTimeIsExpired()
        {
            var resource = Guid.NewGuid().ToString();

            var leaseTime = TimeSpan.FromSeconds(15);

            var acquireA = await Target.TryAcquireAsync(resource, leaseTime, Token);
            Assert.True(acquireA.IsSuccess, "The lease should be acquired.");

            await Task.Delay(leaseTime);

            var acquireB = await Target.TryAcquireAsync(resource, LeaseTime, Token);
            Assert.True(acquireB.IsSuccess, "The lease should be re-acquired.");
        }

        [BlobStorageFact]
        public async Task AllowsRenewAfterLeaseTimeIsExpired()
        {
            var resource = Guid.NewGuid().ToString();

            var leaseTime = TimeSpan.FromSeconds(15);

            var acquireA = await Target.TryAcquireAsync(resource, leaseTime, Token);
            Assert.True(acquireA.IsSuccess, "The lease should be acquired.");

            await Task.Delay(leaseTime);

            var acquireB = await Target.RenewAsync(resource, acquireA.LeaseId, Token);
            Assert.True(acquireB.IsSuccess, "The lease should be re-acquired.");
            Assert.Equal(acquireA.LeaseId, acquireB.LeaseId);
        }

        [BlobStorageFact]
        public async Task AllowsRenewBeforeLeaseTimeIsExpired()
        {
            var resource = Guid.NewGuid().ToString();

            var leaseTime = TimeSpan.FromSeconds(15);

            var acquireA = await Target.TryAcquireAsync(resource, leaseTime, Token);
            Assert.True(acquireA.IsSuccess, "The lease should be acquired.");

            var waitTime = TimeSpan.FromSeconds(10);
            await Task.Delay(leaseTime);

            var acquireB = await Target.RenewAsync(resource, acquireA.LeaseId, Token);
            Assert.True(acquireB.IsSuccess, "The lease should be re-acquired.");
            Assert.Equal(acquireA.LeaseId, acquireB.LeaseId);
        }

        [BlobStorageFact]
        public async Task DoesNotAllowsRenewAfterLeaseIsAcquiredAgain()
        {
            var resource = Guid.NewGuid().ToString();

            var leaseTime = TimeSpan.FromSeconds(15);

            var acquireA = await Target.TryAcquireAsync(resource, leaseTime, Token);
            Assert.True(acquireA.IsSuccess, "The lease should be acquired.");

            await Task.Delay(leaseTime);

            var acquireB = await Target.TryAcquireAsync(resource, leaseTime, Token);
            Assert.True(acquireA.IsSuccess, "The lease should be acquired.");

            var acquireC = await Target.RenewAsync(resource, acquireA.LeaseId, Token);
            Assert.False(acquireC.IsSuccess, "The lease should not be acquired.");
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.Leases;
using Xunit;

namespace NuGet.Services.Staging.Promotion.Tests
{
    /// <summary>
    /// Exercises distributed ownership, background renewal, and recovery after lease loss.
    /// </summary>
    public class StagingPromotionLeaseFacts
    {
        [Fact]
        public async Task FailedWorkReleasesOwnershipForRetry()
        {
            var leases = new TestLeaseService();
            var expected = new InvalidOperationException("Publication failed.");

            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateLease(leases).RunAsync("package/11", () => Task.FromException<bool>(expected))));

            Assert.True(await CreateLease(leases).RunAsync("package/11", () => Task.FromResult(true)));
        }

        [Fact]
        public async Task LostLeaseCannotAcknowledgeWorkOrReleaseNewOwnersLease()
        {
            var leases = new TestLeaseService();
            var entered = Signal();
            var finishOld = Signal();
            var finishNew = Signal();
            var first = CreateLease(leases).RunAsync("package/11", async () =>
            {
                entered.SetResult(true);
                await finishOld.Task;
                return true;
            });
            await entered.Task;
            leases.Expire("package/11");
            var second = CreateLease(leases).RunAsync("package/11", async () =>
            {
                await finishNew.Task;
                return true;
            });

            finishOld.SetResult(true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => first);
            Assert.False(await CreateLease(leases).RunAsync("package/11", () => Task.FromResult(true)));
            finishNew.SetResult(true);
            Assert.True(await second);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RenewsDuringBlockedWorkAndSurfacesRenewalFailure(bool failRenewal)
        {
            var leases = new TestLeaseService();
            var renewalObserved = Signal();
            var finish = Signal();
            var calls = 0;
            leases.OnRenew = name =>
            {
                if (Interlocked.Increment(ref calls) > 1)
                {
                    if (failRenewal)
                    {
                        leases.Expire(name);
                    }

                    renewalObserved.TrySetResult(true);
                }
            };
            var work = CreateLease(leases).RunAsync("package/11", async () =>
            {
                await finish.Task;
                return true;
            });

            var observed = await Task.WhenAny(renewalObserved.Task, Task.Delay(TimeSpan.FromSeconds(45)));
            finish.TrySetResult(true);
            Assert.Same(renewalObserved.Task, observed);
            if (failRenewal)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => work);
            }
            else
            {
                Assert.True(await work);
            }
        }

        [Fact]
        public async Task ReleaseFailureDoesNotHideThePublicationError()
        {
            var leases = new Mock<ILeaseService>();
            leases.Setup(service => service.TryAcquireAsync("package/11", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LeaseResult.Success("lease"));
            leases.Setup(service => service.RenewAsync("package/11", "lease", It.IsAny<CancellationToken>()))
                .ReturnsAsync(LeaseResult.Success("lease"));
            leases.Setup(service => service.ReleaseAsync("package/11", "lease", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Release failed."));
            var expected = new InvalidOperationException("Publication failed.");

            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateLease(leases.Object).RunAsync("package/11", () => Task.FromException<bool>(expected))));
        }

        private static StagingPromotionLease CreateLease(ILeaseService leases)
        {
            return new StagingPromotionLease(leases, Mock.Of<ILogger<StagingPromotionLease>>());
        }

        private static TaskCompletionSource<bool> Signal()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}

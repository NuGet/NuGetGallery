// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.Wrappers;
using NuGet.Services.KeyVault;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SecretRefresherFacts
    {
        public class RefreshContinuouslyAsync : Facts
        {
            public RefreshContinuouslyAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task CanBeCancelledImmediately()
            {
                TokenSource.Cancel();

                await Target.RefreshContinuouslyAsync(TokenSource.Token);

                Factory.Verify(x => x.RefreshAsync(It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task SetsLastRefreshOnSuccess()
            {
                CancelAfter(reloads: 1);

                var before = Target.LastRefresh;
                await Task.Delay(1);
                await Target.RefreshContinuouslyAsync(TokenSource.Token);
                var after = DateTimeOffset.UtcNow;

                Assert.NotEqual(before, Target.LastRefresh);
                Assert.InRange(Target.LastRefresh, before, after);
            }

            [Fact]
            public async Task ReloadsUntilCancelled()
            {
                CancelAfter(reloads: 5);
                Config.SecretRefreshFrequency = TimeSpan.Zero;

                await Target.RefreshContinuouslyAsync(TokenSource.Token);

                Factory.Verify(x => x.RefreshAsync(TokenSource.Token), Times.Exactly(5));
                Factory.Verify(x => x.RefreshAsync(It.IsAny<CancellationToken>()), Times.Exactly(5));
            }

            [Fact]
            public async Task UsesReloadFrequencyOnSuccess()
            {
                FailAndCancelAfter(2);

                var before = Target.LastRefresh;
                await Target.RefreshContinuouslyAsync(TokenSource.Token);
                var after = Target.LastRefresh;

                Assert.Equal(before, Target.LastRefresh);
                Assert.Equal(after, Target.LastRefresh);
            }

            [Fact]
            public async Task UsesReloadFailureRetryFrequencyOnSuccess()
            {
                FailAndCancelAfter(2);
                Config.SecretRefreshFrequency = TimeSpan.Zero;
                Config.SecretRefreshFailureRetryFrequency = TimeSpan.FromMilliseconds(100);

                await Target.RefreshContinuouslyAsync(TokenSource.Token);

                Factory.Verify(x => x.RefreshAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
                SystemTime.Verify(x => x.Delay(Config.SecretRefreshFailureRetryFrequency, TokenSource.Token), Times.Once);
                SystemTime.Verify(x => x.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                Factory = new Mock<IRefreshableSecretReaderFactory>();
                SystemTime = new Mock<ISystemTime>();
                Config = new SearchServiceConfiguration();
                Options = new Mock<IOptionsSnapshot<SearchServiceConfiguration>>();
                Logger = output.GetLogger<SecretRefresher>();

                TokenSource = new CancellationTokenSource();

                // Default test behavior is to cancel after the first invocation otherwise it is very easy to loop
                // forever, which is annoying for the person writing the tests.
                CancelAfter(reloads: 1);

                Config.SecretRefreshFrequency = TimeSpan.FromMilliseconds(100);
                Config.SecretRefreshFailureRetryFrequency = TimeSpan.FromMilliseconds(20);
                Options.Setup(x => x.Value).Returns(() => Config);

                Target = new SecretRefresher(
                    Factory.Object,
                    SystemTime.Object,
                    Options.Object,
                    Logger);
            }

            public Mock<IRefreshableSecretReaderFactory> Factory { get; }
            public Mock<ISystemTime> SystemTime { get; }
            public SearchServiceConfiguration Config { get; }
            public Mock<IOptionsSnapshot<SearchServiceConfiguration>> Options { get; }
            public RecordingLogger<SecretRefresher> Logger { get; }
            public CancellationTokenSource TokenSource { get; }
            public SecretRefresher Target { get; }

            protected void FailAndCancelAfter(int reloads)
            {
                int count = 0;
                Factory
                    .Setup(x => x.RefreshAsync(It.IsAny<CancellationToken>()))
                    .Returns(() =>
                    {
                        count++;
                        if (count >= reloads)
                        {
                            TokenSource.Cancel();
                        }

                        throw new InvalidOperationException("Please retry later.");
                    });
            }

            protected void CancelAfter(int reloads)
            {
                int count = 0;
                Factory
                    .Setup(x => x.RefreshAsync(It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() =>
                    {
                        count++;
                        if (count >= reloads)
                        {
                            TokenSource.Cancel();
                        }
                    });
            }
        }
    }
}

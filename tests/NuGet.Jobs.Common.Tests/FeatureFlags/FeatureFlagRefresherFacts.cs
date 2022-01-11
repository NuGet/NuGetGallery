// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Configuration;
using NuGet.Services.FeatureFlags;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Jobs.Common.Tests.FeatureFlags
{
    public class FeatureFlagRefresherFacts
    {
        public class StartIfConfiguredAsync : Facts
        {
            public StartIfConfiguredAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task DoesNotInitializeCacheServiceIfNoConfig()
            {
                Config.ConnectionString = null;

                await Target.StartIfConfiguredAsync();

                Assert.False(IsCacheServiceInitialized, "The cache service should not have been initialized.");
            }

            [Fact]
            public async Task PerformsOperationsInCorrectOrderWhenThereIsNoCache()
            {
                FeatureFlags = null;

                await Target.StartIfConfiguredAsync();

                Assert.Equal(
                    new[]
                    {
                        nameof(IFeatureFlagCacheService.GetLatestFlagsOrNull),
                        nameof(IFeatureFlagCacheService.RefreshAsync),
                        nameof(IFeatureFlagCacheService.RunAsync),
                    },
                    Operations.ToArray());
            }

            [Fact]
            public async Task PerformsOperationsInCorrectOrderWhenThereIsCache()
            {
                await Target.StartIfConfiguredAsync();

                Assert.Equal(
                    new[]
                    {
                        nameof(IFeatureFlagCacheService.GetLatestFlagsOrNull),
                        nameof(IFeatureFlagCacheService.RunAsync),
                    },
                    Operations.ToArray());
            }

            [Fact]
            public async Task DoesNothingIfAlreadyStarted()
            {
                await Target.StartIfConfiguredAsync();
                ClearOperations();

                await Target.StartIfConfiguredAsync();

                Assert.Empty(Operations);
            }
        }

        public class StopAndWaitAsync : Facts
        {
            public StopAndWaitAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task DoesNothingIfNotStarted()
            {
                await Target.StopAndWaitAsync();

                Assert.False(Cancelled, "There should have been no running task to cancel.");
                Assert.Empty(Operations);
            }

            [Fact]
            public async Task CancelsTheRunningTask()
            {
                await Target.StartIfConfiguredAsync();
                ClearOperations();

                await Target.StopAndWaitAsync();

                Assert.True(Cancelled, "The running task should have been cancelled.");
                Assert.Empty(Operations);
            }
        }

        public abstract class Facts : IAsyncLifetime
        {
            public Facts(ITestOutputHelper output)
            {
                Options = new Mock<IOptionsSnapshot<FeatureFlagConfiguration>>();
                CacheService = new Mock<IFeatureFlagCacheService>();
                Logger = new LoggerFactory().AddXunit(output).CreateLogger<FeatureFlagRefresher>();

                IsCacheServiceInitialized = false;
                Config = new FeatureFlagConfiguration
                {
                    ConnectionString = "some-connection-string",
                    RefreshInternal = TimeSpan.FromSeconds(5),
                };
                FeatureFlags = new NuGet.Services.FeatureFlags.FeatureFlags(
                    new Dictionary<string, FeatureStatus>(),
                    new Dictionary<string, Flight>());

                Options.Setup(x => x.Value).Returns(() => Config);
                LazyCacheService = new Lazy<IFeatureFlagCacheService>(() =>
                {
                    IsCacheServiceInitialized = true;
                    return CacheService.Object;
                });

                Operations = new ConcurrentQueue<string>();
                CacheService
                    .Setup(x => x.GetLatestFlagsOrNull())
                    .Returns(() => FeatureFlags)
                    .Callback(() => Operations.Enqueue(nameof(IFeatureFlagCacheService.GetLatestFlagsOrNull)));
                CacheService
                    .Setup(x => x.RefreshAsync())
                    .Returns(Task.CompletedTask)
                    .Callback(() => Operations.Enqueue(nameof(IFeatureFlagCacheService.RefreshAsync)));
                CacheService
                    .Setup(x => x.RunAsync(It.IsAny<CancellationToken>()))
                    .Returns<CancellationToken>(async token =>
                    {
                        // We use a delay here to simulate a real long running task. We don't want to delay for too
                        // long so that if there is a test or product bug we don't want to hang. This is enough time
                        // for test logic to complete.
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(30), token);
                        }
                        catch (OperationCanceledException)
                        {
                            Cancelled = true;
                        }
                    })
                    .Callback(() => Operations.Enqueue(nameof(IFeatureFlagCacheService.RunAsync)));

                Target = new FeatureFlagRefresher(
                    Options.Object,
                    LazyCacheService,
                    Logger);
            }

            public Mock<IOptionsSnapshot<FeatureFlagConfiguration>> Options { get; }
            public Mock<IFeatureFlagCacheService> CacheService { get; }
            public ILogger<FeatureFlagRefresher> Logger { get; }
            public bool IsCacheServiceInitialized { get; set; }
            public FeatureFlagConfiguration Config { get; }
            public NuGet.Services.FeatureFlags.FeatureFlags FeatureFlags { get; set; }
            public Lazy<IFeatureFlagCacheService> LazyCacheService { get; }
            public ConcurrentQueue<string> Operations { get; }
            public FeatureFlagRefresher Target { get; }
            public bool Cancelled { get; set; }

            public void ClearOperations()
            {
                while (Operations.TryDequeue(out var _))
                {
                }
            }

            public Task InitializeAsync()
            {
                return Task.CompletedTask;
            }

            public async Task DisposeAsync()
            {
                await Target.StopAndWaitAsync();
            }
        }
    }
}

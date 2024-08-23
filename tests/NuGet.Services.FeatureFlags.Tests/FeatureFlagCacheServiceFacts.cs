// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGet.Services.FeatureFlags.Tests
{
    public class FeatureFlagCacheServiceFacts
    {
        public class RefreshAsync : FactsBase
        {
            [Fact]
            public void ReturnsNullOnUnitialized()
            {
                var flags = _target.GetLatestFlagsOrNull();
                var lastRefresh = _target.GetRefreshTimeOrNull();

                Assert.Null(flags);
                Assert.Null(lastRefresh);

                _storage.Verify(s => s.GetAsync(), Times.Never);
            }

            [Fact]
            public async Task ReturnsLatestFeatureFlags()
            {
                // Arrange
                _storage
                    .Setup(s => s.GetAsync())
                    .ReturnsAsync(_latestFlags);

                await _target.RefreshAsync();

                // Act
                var flags = _target.GetLatestFlagsOrNull();
                var lastRefresh = _target.GetRefreshTimeOrNull();

                // Assert
                Assert.Same(_latestFlags, flags);
                Assert.NotNull(lastRefresh);

                _storage.Verify(s => s.GetAsync(), Times.Once);
            }

            [Fact]
            public async Task DoesntCatchExceptions()
            {
                // Arrange
                var exception = new Exception("Hello world");

                _storage
                    .Setup(s => s.GetAsync())
                    .ThrowsAsync(exception);

                var result = await Assert.ThrowsAsync<Exception>(() => _target.RefreshAsync());
                
                Assert.Same(exception, result);
                _storage.Verify(s => s.GetAsync(), Times.Once());
            }
        }

        public class RunAsync : FactsBase
        {
            [Fact]
            public async Task RefreshesUntilCancelled()
            {
                // Arrange
                await RunCacheAsync(refreshes: 2);

                // Act
                var result = _target.GetLatestFlagsOrNull();

                // Assert
                Assert.Same(_latestFlags, result);

                _storage.Verify(s => s.GetAsync(), Times.Exactly(2));
                _telemetry.Verify(t => t.TrackFeatureFlagStaleness(It.IsAny<TimeSpan>()), Times.Exactly(2));
            }

            [Fact]
            public async Task WhenRunNeverSucceeds_LatestFlagsReturnsNull()
            {
                // Arrange - flags are never loaded because storage implementation is broken.
                await RunCacheAsync(refreshes: 3, callback: i => throw new Exception("Hello world"));

                // Act
                var result = _target.GetLatestFlagsOrNull();

                // Assert
                Assert.Null(result);
                Assert.True(_stalenessMetrics.All(s => s == TimeSpan.MaxValue));

                _storage.Verify(s => s.GetAsync(), Times.Exactly(3));
                _telemetry.Verify(t => t.TrackFeatureFlagStaleness(It.IsAny<TimeSpan>()), Times.Exactly(3));
            }

            [Fact]
            public async Task WhenRunFails_StalenessMetricIncreases()
            {
                // Arrange - flags load once successfully and then fail forever.
                await RunCacheAsync(refreshes: 4, callback: i =>
                {
                    if (i > 1)
                    {
                        throw new Exception("Hello world");
                    }
                });

                // Act
                var result = _target.GetLatestFlagsOrNull();

                // Assert - Metrics 0 and 1 are both ~100ms but cannot be compared in a testable way.
                Assert.Same(_latestFlags, result);
                Assert.True(_stalenessMetrics[2] > _stalenessMetrics[1]);
                Assert.True(_stalenessMetrics[3] > _stalenessMetrics[2]);
            }

            [Fact]
            public async Task AllowsNullTelemetryService()
            {
                // Arrange
                var target = new FeatureFlagCacheService(
                    () => _storage.Object,
                    _options,
                    telemetryService: null,
                    logger: Mock.Of<ILogger<FeatureFlagCacheService>>());

                await RunCacheAsync(target, refreshes: 2);

                // Act
                var result = target.GetLatestFlagsOrNull();

                // Assert
                Assert.Same(_latestFlags, result);

                _storage.Verify(s => s.GetAsync(), Times.Exactly(2));
            }

            private async Task RunCacheAsync(int refreshes, Action<int> callback = null)
            {
                await RunCacheAsync(_target, refreshes, callback);
            }

            private async Task RunCacheAsync(FeatureFlagCacheService target, int refreshes, Action<int> callback = null)
            {
                var count = 0;
                _options.RefreshInterval = TimeSpan.FromMilliseconds(100);

                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    _storage
                        .Setup(s => s.GetAsync())
                        .Callback(() =>
                        {
                            count++;
                            if (count == refreshes)
                            {
                                cancellationTokenSource.Cancel();
                            }

                            callback?.Invoke(count);
                        })
                        .ReturnsAsync(_latestFlags);

                    // Act
                    await target.RunAsync(cancellationTokenSource.Token);
                }
            }
        }

        public class FactsBase
        {
            protected readonly Mock<IFeatureFlagStorageService> _storage;
            protected readonly Mock<IFeatureFlagTelemetryService> _telemetry;
            protected readonly FeatureFlagOptions _options;
            protected readonly FeatureFlagCacheService _target;

            protected readonly FeatureFlags _latestFlags;
            protected readonly List<TimeSpan> _stalenessMetrics;

            public FactsBase()
            {
                _storage = new Mock<IFeatureFlagStorageService>();
                _telemetry = new Mock<IFeatureFlagTelemetryService>();
                _options = new FeatureFlagOptions();

                _target = new FeatureFlagCacheService(
                    () => _storage.Object,
                    _options,
                    _telemetry.Object,
                    Mock.Of<ILogger<FeatureFlagCacheService>>());

                _latestFlags = FeatureFlagStateBuilder
                    .Create()
                    .WithFeature("Foo", FeatureStatus.Enabled)
                    .Build();

                _stalenessMetrics = new List<TimeSpan>();
                _telemetry
                    .Setup(t => t.TrackFeatureFlagStaleness(It.IsAny<TimeSpan>()))
                    .Callback((TimeSpan staleness) =>
                    {
                        _stalenessMetrics.Add(staleness);
                    });
            }
        }
    }
}

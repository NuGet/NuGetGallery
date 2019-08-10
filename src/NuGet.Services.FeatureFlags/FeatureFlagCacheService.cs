// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.FeatureFlags
{
    public class FeatureFlagCacheService : IFeatureFlagCacheService
    {
        private readonly IFeatureFlagStorageService _storage;
        private readonly FeatureFlagOptions _options;
        private readonly IFeatureFlagTelemetryService _telemetryServiceOrNull;
        private readonly ILogger<FeatureFlagCacheService> _logger;

        private FeatureFlagsAndRefreshTime _latestFlags;

        public FeatureFlagCacheService(
            IFeatureFlagStorageService storage,
            FeatureFlagOptions options,
            ILogger<FeatureFlagCacheService> logger)
          : this(storage, options, telemetryService: null, logger: logger)
        {
        }

        public FeatureFlagCacheService(
            IFeatureFlagStorageService storage,
            FeatureFlagOptions options,
            IFeatureFlagTelemetryService telemetryService,
            ILogger<FeatureFlagCacheService> logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _telemetryServiceOrNull = telemetryService;
            _latestFlags = null;
        }

        public FeatureFlags GetLatestFlagsOrNull()
        {
            return _latestFlags?.FeatureFlags;
        }

        public DateTimeOffset? GetRefreshTimeOrNull()
        {
            return _latestFlags?.RefreshStartTime;
        }

        public async Task RefreshAsync()
        {
            var refreshTime = DateTimeOffset.UtcNow;
            var latestFlags = await _storage.GetAsync();

            _latestFlags = new FeatureFlagsAndRefreshTime(latestFlags, refreshTime);
        }

        public async Task RunAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Refreshing the feature flags...");

                    await RefreshAsync();

                    _logger.LogInformation("Refreshed the feature flags");
                }
                catch (Exception e)
                {
                    _logger.LogError(0, e, "Unable to refresh the feature flags due to exception");
                }

                // Report the feature flags' time time since the flags were last successfully refreshed, aka their "staleness".
                var staleness = (_latestFlags == null)
                    ? TimeSpan.MaxValue
                    : DateTimeOffset.UtcNow - _latestFlags.RefreshStartTime;

                _telemetryServiceOrNull?.TrackFeatureFlagStaleness(staleness);

                _logger.LogInformation(
                    "Feature flags were last refreshed {Staleness} ago. Sleeping for {SleepDuration} before next refresh...",
                    staleness,
                    _options.RefreshInterval);

                try
                {
                    await Task.Delay(_options.RefreshInterval, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Swallow the cancelled delay operation to allow quiet shutdown of the refresh loop. A cancelled
                    // delay operation is harmless to the system.
                }
            }
        }

        private class FeatureFlagsAndRefreshTime
        {
            public FeatureFlagsAndRefreshTime(FeatureFlags featureFlags, DateTimeOffset refreshTime)
            {
                FeatureFlags = featureFlags;
                RefreshStartTime = refreshTime;
            }

            public FeatureFlags FeatureFlags { get; }
            public DateTimeOffset RefreshStartTime { get; }
        }
    }
}

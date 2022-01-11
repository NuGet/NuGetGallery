// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Configuration;
using NuGet.Services.FeatureFlags;

namespace NuGet.Jobs
{
    public class FeatureFlagRefresher : IFeatureFlagRefresher
    {
        private readonly IOptionsSnapshot<FeatureFlagConfiguration> _options;
        private readonly Lazy<IFeatureFlagCacheService> _lazyCacheService;
        private readonly ILogger<FeatureFlagRefresher> _logger;

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private CancellationTokenSource _cancellationTokenSource;
        private Task _runTask;

        public FeatureFlagRefresher(
            IOptionsSnapshot<FeatureFlagConfiguration> options,
            Lazy<IFeatureFlagCacheService> lazyCacheService,
            ILogger<FeatureFlagRefresher> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _lazyCacheService = lazyCacheService ?? throw new ArgumentNullException(nameof(lazyCacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private IFeatureFlagCacheService CacheService => _lazyCacheService.Value;

        public async Task StartIfConfiguredAsync()
        {
            if (string.IsNullOrEmpty(_options.Value.ConnectionString))
            {
                _logger.LogInformation("No feature flag connection string is configured. The refresh will not start.");
                return;
            }

            _logger.LogInformation("The feature flag refresher lock will be acquired for the start operation.");
            await _lock.WaitAsync();
            try
            {
                if (_runTask != null)
                {
                    _logger.LogInformation("The feature flag refresh task is already started.");
                    return;
                }

                if (CacheService.GetLatestFlagsOrNull() == null)
                {
                    _logger.LogInformation("Loading the initial feature flag state.");
                    await CacheService.RefreshAsync();
                }

                _logger.LogInformation("Starting the feature flag refresh task.");
                _cancellationTokenSource = new CancellationTokenSource();
                _runTask = CacheService.RunAsync(_cancellationTokenSource.Token);
            }
            finally
            {
                _lock.Release();
                _logger.LogInformation("The feature flag refresher lock has been released after the start operation.");
            }
        }

        public async Task StopAndWaitAsync()
        {
            _logger.LogInformation("The feature flag refresher lock will be acquired for the stop operation.");
            await _lock.WaitAsync();
            try
            {
                if (_runTask == null)
                {
                    _logger.LogInformation("The feature flag refresh task is not running and therefore cannot be stopped.");
                    return;
                }

                _logger.LogInformation("Stopping the feature flag refresh task.");
                _cancellationTokenSource.Cancel();

                try
                {
                    await _runTask;
                    _logger.LogInformation("The feature flag refresh task stopped gracefully.");
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogWarning(0, ex, "The feature flag refresh task stopped with a cancelled exception.");
                }

                _runTask = null;
            }
            finally
            {
                _lock.Release();
                _logger.LogInformation("The feature flag refresher lock has been released after the stop operation.");
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.AzureSearch.Wrappers;
using NuGet.Services.KeyVault;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SecretRefresher : ISecretRefresher
    {
        private readonly IRefreshableSecretReaderFactory _factory;
        private readonly ISystemTime _systemTime;
        private readonly IOptionsSnapshot<SearchServiceConfiguration> _options;
        private readonly ILogger<SecretRefresher> _logger;

        public SecretRefresher(
            IRefreshableSecretReaderFactory factory,
            ISystemTime systemTime,
            IOptionsSnapshot<SearchServiceConfiguration> options,
            ILogger<SecretRefresher> logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _systemTime = systemTime ?? throw new ArgumentNullException(nameof(systemTime));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// We can initialize the "last refresh" time to the current time since secrets are loaded as the app is
        /// starting.
        /// </summary>
        public DateTimeOffset LastRefresh { get; private set; } = DateTimeOffset.UtcNow;

        public async Task RefreshContinuouslyAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _logger.LogInformation("Trying to refresh the secrets.");

                TimeSpan delay;
                try
                {
                    await _factory.RefreshAsync(token);
                    delay = _options.Value.SecretRefreshFrequency;
                    LastRefresh = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(0, ex, "An exception was thrown while refreshing the secrets.");

                    delay = _options.Value.SecretRefreshFailureRetryFrequency;
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                _logger.LogInformation(
                    "Waiting {Duration} before attempting to refresh the secrets again.",
                    delay);
                await _systemTime.Delay(delay, token);
            }
        }
    }
}

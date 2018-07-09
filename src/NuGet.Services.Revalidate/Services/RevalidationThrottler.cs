// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Revalidate
{
    public class RevalidationThrottler : IRevalidationThrottler
    {
        private readonly RevalidationConfiguration _config;
        private readonly ILogger<RevalidationThrottler> _logger;

        public RevalidationThrottler(RevalidationConfiguration config, ILogger<RevalidationThrottler> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<bool> IsThrottledAsync()
        {
            // TODO:
            // Calculate desired event rate
            // Calculate current event rate (# of revalidations + Gallery actions)
            // Compare desired event rate to configured event rate. If configured rate is higher, update desired event rate.
            // If current event rate is greater than or equal to desired event rate, return true;
            return Task.FromResult(false);
        }

        public Task ResetCapacityAsync()
        {
            return Task.CompletedTask;
        }

        public Task IncreaseCapacityAsync()
        {
            return Task.CompletedTask;
        }

        public async Task DelayUntilNextRevalidationAsync()
        {
            // TODO: Calculate sleep duration to achieve desired event rate.
            _logger.LogInformation("Delaying until next revalidation by sleeping for 5 minutes...");

            await Task.Delay(TimeSpan.FromMinutes(5));
        }

        public async Task DelayUntilRevalidationRetryAsync()
        {
            _logger.LogInformation(
                "Delaying for revalidation retry by sleeping for {SleepDuration}",
                _config.RetryLaterSleep);

            await Task.Delay(_config.RetryLaterSleep);
        }
    }
}

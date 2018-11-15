// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Revalidate
{
    public class RevalidationThrottler : IRevalidationThrottler
    {
        private static readonly TimeSpan MinimumDelayUntilNextRevalidations = TimeSpan.FromSeconds(5);

        private readonly IRevalidationJobStateService _jobState;
        private readonly IPackageRevalidationStateService _packageState;
        private readonly IGalleryService _gallery;
        private readonly RevalidationConfiguration _config;
        private readonly ILogger<RevalidationThrottler> _logger;

        public RevalidationThrottler(
            IRevalidationJobStateService jobState,
            IPackageRevalidationStateService packageState,
            IGalleryService gallery,
            RevalidationConfiguration config,
            ILogger<RevalidationThrottler> logger)
        {
            _jobState = jobState ?? throw new ArgumentNullException(nameof(jobState));
            _packageState = packageState ?? throw new ArgumentNullException(nameof(packageState));
            _gallery = gallery ?? throw new ArgumentNullException(nameof(gallery));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> IsThrottledAsync()
        {
            var desiredRate = await _jobState.GetDesiredPackageEventRateAsync();
            var recentGalleryEvents = await _gallery.CountEventsInPastHourAsync();
            var recentRevalidations = await _packageState.CountRevalidationsEnqueuedInPastHourAsync();

            var revalidationQuota = desiredRate - recentRevalidations - recentGalleryEvents;

            if (revalidationQuota <= 0)
            {
                _logger.LogInformation(
                    "Throttling revalidations. Desired rate: {DesiredRate}, gallery events: {GalleryEvents}, recent revalidations: {RecentRevalidations}",
                    desiredRate,
                    recentGalleryEvents,
                    recentRevalidations);

                return true;
            }
            else
            {
                _logger.LogInformation(
                    "Allowing revalidations. Desired rate: {DesiredRate}, gallery events: {GalleryEvents}, recent revalidations: {RecentRevalidations}",
                    desiredRate,
                    recentGalleryEvents,
                    recentRevalidations);

                return false;
            }
        }

        public async Task DelayUntilNextRevalidationAsync(int revalidationsStarted, TimeSpan startDuration)
        {
            var desiredHourlyRate = await _jobState.GetDesiredPackageEventRateAsync();

            // Calculate the time to sleep. If this batch started 50 revalidations in 30 seconds and we would like
            // to achieve 1,000 revalidations per hour, we should sleep for 2.5 minutes:
            //
            // (50/1000) * 60 - (30/60) = 2.5
            var sleepDuration = TimeSpan.FromHours((float)revalidationsStarted / desiredHourlyRate) - startDuration;
            if (sleepDuration < MinimumDelayUntilNextRevalidations)
            {
                _logger.LogWarning($"The delay until next revalidation is too small, overriding it to {MinimumDelayUntilNextRevalidations}!");
                sleepDuration = MinimumDelayUntilNextRevalidations;
            }

            _logger.LogInformation("Delaying until next revalidation by sleeping for {SleepDuration}...", sleepDuration);

            await Task.Delay(sleepDuration);
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

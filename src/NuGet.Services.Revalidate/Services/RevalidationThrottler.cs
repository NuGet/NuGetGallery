// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Revalidate
{
    public class RevalidationThrottler : IRevalidationThrottler
    {
        private readonly IRevalidationJobStateService _jobState;
        private readonly IPackageRevalidationStateService _packageState;
        private readonly RevalidationConfiguration _config;
        private readonly ILogger<RevalidationThrottler> _logger;

        public RevalidationThrottler(
            IRevalidationJobStateService jobState,
            IPackageRevalidationStateService packageState,
            RevalidationConfiguration config,
            ILogger<RevalidationThrottler> logger)
        {
            _jobState = jobState ?? throw new ArgumentNullException(nameof(jobState));
            _packageState = packageState ?? throw new ArgumentNullException(nameof(packageState));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> IsThrottledAsync()
        {
            var desiredRate = await _jobState.GetDesiredPackageEventRateAsync();
            var recentGalleryEvents = await CountGalleryEventsInPastHourAsync();
            var recentRevalidations = await _packageState.CountRevalidationsEnqueuedInPastHourAsync();

            var revalidationQuota = desiredRate - recentRevalidations - recentGalleryEvents;

            return (revalidationQuota <= 0);
        }

        public async Task DelayUntilNextRevalidationAsync()
        {
            var desiredHourlyRate = await _jobState.GetDesiredPackageEventRateAsync();
            var sleepDuration = TimeSpan.FromHours(1.0 / desiredHourlyRate);

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

        private Task<int> CountGalleryEventsInPastHourAsync()
        {
            // TODO: Count the number of package pushes, lists, and unlists.
            // Run this AI query:
            //
            //   customMetrics | where name == "PackagePush" or name == "PackageUnlisted" or name == "PackageListed" | summarize sum(value)
            //
            // Using this HTTP request:
            //
            // GET /v1/apps/46f13c7d-635f-42c3-8120-593edeaad426/query?timespan=P1D&query=customMetrics%20%7C%20where%20name%20%3D%3D%20%22PackagePush%22%20or%20name%20%3D%3D%20%22PackageUnlisted%22%20or%20name%20%3D%3D%20%22PackageListed%22%20%7C%20summarize%20sum(value)%20 HTTP/1.1
            // Host: api.applicationinsights.io
            // x-api-key: my-super-secret-api-key
            //
            // See: https://dev.applicationinsights.io/quickstart
            return Task.FromResult(0);
        }
    }
}

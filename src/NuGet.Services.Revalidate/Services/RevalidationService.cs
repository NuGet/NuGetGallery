// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;

namespace NuGet.Services.Revalidate
{
    public class RevalidationService : IRevalidationService
    {
        private readonly IRevalidationStateService _state;
        private readonly ISingletonService _singletonService;
        private readonly IRevalidationThrottler _throttler;
        private readonly IHealthService _healthService;
        private readonly IRevalidationQueue _revalidationQueue;
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly RevalidationConfiguration _config;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<RevalidationService> _logger;

        public RevalidationService(
            IRevalidationStateService state,
            ISingletonService singletonService,
            IRevalidationThrottler throttler,
            IHealthService healthService,
            IRevalidationQueue revalidationQueue,
            IPackageValidationEnqueuer validationEnqueuer,
            RevalidationConfiguration config,
            ITelemetryService telemetryService,
            ILogger<RevalidationService> logger)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _singletonService = singletonService ?? throw new ArgumentNullException(nameof(singletonService));
            _throttler = throttler ?? throw new ArgumentNullException(nameof(throttler));
            _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
            _revalidationQueue = revalidationQueue ?? throw new ArgumentNullException(nameof(revalidationQueue));
            _validationEnqueuer = validationEnqueuer ?? throw new ArgumentNullException(nameof(validationEnqueuer));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RunAsync()
        {
            var runTime = Stopwatch.StartNew();

            do
            {
                _logger.LogInformation("Starting next revalidation...");

                var result = await StartNextRevalidationAsync();

                switch (result)
                {
                    case RevalidationResult.RevalidationEnqueued:
                        _logger.LogInformation("Successfully enqueued revalidation");

                        await _throttler.DelayUntilNextRevalidationAsync();
                        break;

                    case RevalidationResult.RetryLater:
                        _logger.LogInformation("Could not start revalidation, retrying later");

                        await _throttler.DelayUntilRevalidationRetryAsync();
                        break;

                    case RevalidationResult.UnrecoverableError:
                    default:
                        _logger.LogCritical(
                            "Stopping revalidations due to unrecoverable or unknown result {Result}",
                            result);

                        return;
                }
            }
            while (runTime.Elapsed <= _config.ShutdownWaitInterval);

            _logger.LogInformation("Finished running after {ElapsedTime}", runTime.Elapsed);
        }

        public async Task<RevalidationResult> StartNextRevalidationAsync()
        {
            using (_telemetryService.TrackDurationToStartNextRevalidation())
            {
                // Don't start a revalidation if the job has been deactivated, if the ingestion pipeline is unhealthy,
                // or if we have reached our quota of desired revalidations.
                var checkResult = await CanStartRevalidationAsync();
                if (checkResult != null)
                {
                    _logger.LogInformation(
                        "Detected that a revalidation should not be started due to result {Result}",
                        checkResult.Value);

                    return checkResult.Value;
                }

                // Everything is in tip-top shape! Increase the throttling quota and start the next revalidation.
                await _throttler.IncreaseCapacityAsync();

                var revalidation = await _revalidationQueue.NextOrNullAsync();
                if (revalidation == null)
                {
                    _logger.LogInformation("Could not find a package to revalidate at this time, retry later...");

                    return RevalidationResult.RetryLater;
                }

                return await StartRevalidationAsync(revalidation);
            }
        }

        private async Task<RevalidationResult?> CanStartRevalidationAsync()
        {
            if (!await _singletonService.IsSingletonAsync())
            {
                _logger.LogCritical("Detected another instance of the revalidate job, cancelling revalidations!");

                return RevalidationResult.UnrecoverableError;
            }

            if (await _state.IsKillswitchActiveAsync())
            {
                _logger.LogWarning("Revalidation killswitch has been activated, retry later...");

                return RevalidationResult.RetryLater;
            }

            if (await _throttler.IsThrottledAsync())
            {
                _logger.LogInformation("Revalidations have reached the desired event rate, retry later...");

                return RevalidationResult.RetryLater;
            }

            if (!await _healthService.IsHealthyAsync())
            {
                _logger.LogWarning("Service appears to be unhealthy, resetting throttling capacity and retry later...");

                await _throttler.ResetCapacityAsync();

                return RevalidationResult.RetryLater;
            }

            if (await _state.IsKillswitchActiveAsync())
            {
                _logger.LogWarning("Revalidation killswitch has been activated after the throttle and health check, retry later...");

                return RevalidationResult.RetryLater;
            }

            return null;
        }

        private async Task<RevalidationResult> StartRevalidationAsync(PackageRevalidation revalidation)
        {
            var message = new PackageValidationMessageData(
                revalidation.PackageId,
                revalidation.PackageNormalizedVersion,
                revalidation.ValidationTrackingId.Value);

            await _validationEnqueuer.StartValidationAsync(message);
            await _state.MarkRevalidationAsEnqueuedAsync(revalidation);

            _telemetryService.TrackPackageRevalidationStarted(revalidation.PackageId, revalidation.PackageNormalizedVersion);

            return RevalidationResult.RevalidationEnqueued;
        }
    }
}

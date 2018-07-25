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
        private readonly IRevalidationJobStateService _jobState;
        private readonly IPackageRevalidationStateService _packageState;
        private readonly ISingletonService _singletonService;
        private readonly IRevalidationThrottler _throttler;
        private readonly IHealthService _healthService;
        private readonly IRevalidationQueue _revalidationQueue;
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly RevalidationConfiguration _config;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<RevalidationService> _logger;

        public RevalidationService(
            IRevalidationJobStateService jobState,
            IPackageRevalidationStateService packageState,
            ISingletonService singletonService,
            IRevalidationThrottler throttler,
            IHealthService healthService,
            IRevalidationQueue revalidationQueue,
            IPackageValidationEnqueuer validationEnqueuer,
            RevalidationConfiguration config,
            ITelemetryService telemetryService,
            ILogger<RevalidationService> logger)
        {
            _jobState = jobState ?? throw new ArgumentNullException(nameof(jobState));
            _packageState = packageState ?? throw new ArgumentNullException(nameof(packageState));
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
            if (!await _jobState.IsInitializedAsync())
            {
                _logger.LogError("The revalidation service must be initialized before running revalidations");

                throw new InvalidOperationException("The revalidation service must be initialized before running revalidations");
            }

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
            using (var operation = _telemetryService.TrackStartNextRevalidationOperation())
            {
                var result = await StartNextRevalidationInternalAsync();

                operation.Properties.Result = result;

                return result;
            }
        }

        public async Task<RevalidationResult> StartNextRevalidationInternalAsync()
        {
            try
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
                await _jobState.IncreaseDesiredPackageEventRateAsync();

                var revalidation = await _revalidationQueue.NextOrNullAsync();
                if (revalidation == null)
                {
                    _logger.LogInformation("Could not find a package to revalidate at this time, retry later...");

                    return RevalidationResult.RetryLater;
                }

                return await StartRevalidationAsync(revalidation);
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "Failed to start next validation due to exception, retry later...");

                return RevalidationResult.RetryLater;
            }
        }

        private async Task<RevalidationResult?> CanStartRevalidationAsync()
        {
            if (!await _singletonService.IsSingletonAsync())
            {
                _logger.LogCritical("Detected another instance of the revalidate job, cancelling revalidations!");

                return RevalidationResult.UnrecoverableError;
            }

            if (await _jobState.IsKillswitchActiveAsync())
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
                _logger.LogWarning("Service appears to be unhealthy, resetting the desired package event rate. Retry later...");

                await _jobState.ResetDesiredPackageEventRateAsync();

                return RevalidationResult.RetryLater;
            }

            if (await _jobState.IsKillswitchActiveAsync())
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
            await _packageState.MarkPackageRevalidationAsEnqueuedAsync(revalidation);

            _telemetryService.TrackPackageRevalidationStarted(revalidation.PackageId, revalidation.PackageNormalizedVersion);

            return RevalidationResult.RevalidationEnqueued;
        }
    }
}

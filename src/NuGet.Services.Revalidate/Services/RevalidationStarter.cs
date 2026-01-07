using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;

namespace NuGet.Services.Revalidate
{
    public class RevalidationStarter : IRevalidationStarter
    {
        private readonly IRevalidationJobStateService _jobState;
        private readonly IPackageRevalidationStateService _packageState;
        private readonly ISingletonService _singletonService;
        private readonly IRevalidationThrottler _throttler;
        private readonly IHealthService _healthService;
        private readonly IRevalidationQueue _revalidationQueue;
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<RevalidationStarter> _logger;

        public RevalidationStarter(
            IRevalidationJobStateService jobState,
            IPackageRevalidationStateService packageState,
            ISingletonService singletonService,
            IRevalidationThrottler throttler,
            IHealthService healthService,
            IRevalidationQueue revalidationQueue,
            IPackageValidationEnqueuer validationEnqueuer,
            ITelemetryService telemetryService,
            ILogger<RevalidationStarter> logger)
        {
            _jobState = jobState ?? throw new ArgumentNullException(nameof(jobState));
            _packageState = packageState ?? throw new ArgumentNullException(nameof(packageState));
            _singletonService = singletonService ?? throw new ArgumentNullException(nameof(singletonService));
            _throttler = throttler ?? throw new ArgumentNullException(nameof(throttler));
            _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
            _revalidationQueue = revalidationQueue ?? throw new ArgumentNullException(nameof(revalidationQueue));
            _validationEnqueuer = validationEnqueuer ?? throw new ArgumentNullException(nameof(validationEnqueuer));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StartRevalidationResult> StartNextRevalidationsAsync()
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

                    switch (checkResult.Value)
                    {
                        case StartRevalidationStatus.RetryLater:
                            return StartRevalidationResult.RetryLater;

                        case StartRevalidationStatus.UnrecoverableError:
                            return StartRevalidationResult.UnrecoverableError;

                        default:
                            throw new InvalidOperationException($"Unexpected status {checkResult.Value} from {nameof(CanStartRevalidationAsync)}");
                    }
                }

                // Everything is in tip-top shape! Increase the throttling quota and start the next revalidations.
                await _jobState.IncreaseDesiredPackageEventRateAsync();

                var revalidations = await _revalidationQueue.NextAsync();
                if (!revalidations.Any())
                {
                    _logger.LogInformation("Could not find packages to revalidate at this time, retry later...");

                    return StartRevalidationResult.RetryLater;
                }

                return await StartRevalidationsAsync(revalidations);
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "Failed to start next validation due to exception, retry later...");

                return StartRevalidationResult.RetryLater;
            }
        }

        private async Task<StartRevalidationStatus?> CanStartRevalidationAsync()
        {
            if (!await _singletonService.IsSingletonAsync())
            {
                _logger.LogCritical("Detected another instance of the revalidate job, cancelling revalidations!");

                return StartRevalidationStatus.UnrecoverableError;
            }

            if (await _jobState.IsKillswitchActiveAsync())
            {
                _logger.LogWarning("Revalidation killswitch has been activated, retry later...");

                return StartRevalidationStatus.RetryLater;
            }

            if (await _throttler.IsThrottledAsync())
            {
                _logger.LogInformation("Revalidations have reached the desired event rate, retry later...");

                return StartRevalidationStatus.RetryLater;
            }

            if (!await _healthService.IsHealthyAsync())
            {
                _logger.LogWarning("Service appears to be unhealthy, resetting the desired package event rate. Retry later...");

                await _jobState.ResetDesiredPackageEventRateAsync();

                return StartRevalidationStatus.RetryLater;
            }

            if (await _jobState.IsKillswitchActiveAsync())
            {
                _logger.LogWarning("Revalidation killswitch has been activated after the throttle and health check, retry later...");

                return StartRevalidationStatus.RetryLater;
            }

            return null;
        }

        private async Task<StartRevalidationResult> StartRevalidationsAsync(IReadOnlyList<PackageRevalidation> revalidations)
        {
            _logger.LogInformation("Starting {RevalidationCount} revalidations...", revalidations.Count);

            foreach (var revalidation in revalidations)
            {
                _logger.LogInformation(
                    "Starting revalidation for package {PackageId} {PackageVersion}...",
                    revalidation.PackageId,
                    revalidation.PackageNormalizedVersion);

                var message = PackageValidationMessageData.NewProcessValidationSet(
                    revalidation.PackageId,
                    revalidation.PackageNormalizedVersion,
                    revalidation.ValidationTrackingId.Value,
                    ValidatingType.Package,
                    entityKey: null);

                await _validationEnqueuer.SendMessageAsync(message);

                _telemetryService.TrackPackageRevalidationStarted(revalidation.PackageId, revalidation.PackageNormalizedVersion);
                _logger.LogInformation(
                    "Started revalidation for package {PackageId} {PackageVersion}",
                    revalidation.PackageId,
                    revalidation.PackageNormalizedVersion);
            }

            _logger.LogInformation("Started {RevalidationCount} revalidations, marking them as enqueued...", revalidations.Count);

            await _packageState.MarkPackageRevalidationsAsEnqueuedAsync(revalidations);

            _logger.LogInformation("Marked {RevalidationCount} revalidations as enqueued", revalidations.Count);

            return StartRevalidationResult.RevalidationsEnqueued(revalidations.Count);
        }
    }
}

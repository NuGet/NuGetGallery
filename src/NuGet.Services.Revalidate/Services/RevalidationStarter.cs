using System;
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

        public async Task<RevalidationResult> StartNextRevalidationAsync()
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

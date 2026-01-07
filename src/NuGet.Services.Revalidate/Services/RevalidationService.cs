// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Revalidate
{
    public class RevalidationService : IRevalidationService
    {
        private readonly IRevalidationJobStateService _jobState;
        private readonly IRevalidationThrottler _throttler;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RevalidationConfiguration _config;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<RevalidationService> _logger;

        public RevalidationService(
            IRevalidationJobStateService jobState,
            IRevalidationThrottler throttler,
            IServiceScopeFactory scopeFactory,
            RevalidationConfiguration config,
            ITelemetryService telemetryService,
            ILogger<RevalidationService> logger)
        {
            _jobState = jobState ?? throw new ArgumentNullException(nameof(jobState));
            _throttler = throttler ?? throw new ArgumentNullException(nameof(throttler));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
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

                var startRevalidationsStopwatch = Stopwatch.StartNew();
                var result = await StartNextRevalidationsAsync();
                startRevalidationsStopwatch.Stop();

                switch (result.Status)
                {
                    case StartRevalidationStatus.RevalidationsEnqueued:
                        _logger.LogInformation(
                            "Successfully enqueued {RevalidationsStarted} revalidations in {Duration}",
                            result.RevalidationsStarted,
                            startRevalidationsStopwatch.Elapsed);

                        await _throttler.DelayUntilNextRevalidationAsync(result.RevalidationsStarted, startRevalidationsStopwatch.Elapsed);
                        break;

                    case StartRevalidationStatus.RetryLater:
                        _logger.LogInformation("Could not start revalidation, retrying later");

                        await _throttler.DelayUntilRevalidationRetryAsync();
                        break;

                    case StartRevalidationStatus.UnrecoverableError:
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

        private async Task<StartRevalidationResult> StartNextRevalidationsAsync()
        {
            using (var operation = _telemetryService.TrackStartNextRevalidationOperation())
            using (var scope = _scopeFactory.CreateScope())
            {
                var starter = scope.ServiceProvider.GetRequiredService<IRevalidationStarter>();
                var result = await starter.StartNextRevalidationsAsync();

                operation.Properties.Result = result.Status;
                operation.Properties.Started = result.RevalidationsStarted;

                return result;
            }
        }
    }
}

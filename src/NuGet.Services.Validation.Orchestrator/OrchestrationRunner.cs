// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Controls the lifetime and proper shutdown of the Orchestrator instance
    /// </summary>
    public class OrchestrationRunner
    {
        /// <summary>
        /// The time to sleep in each iteration of the waiting for shutdown loop
        /// </summary>
        private static readonly TimeSpan ShutdownLoopSleepTime = TimeSpan.FromSeconds(1);

        private readonly ISubscriptionProcessor<PackageValidationMessageData> _subscriptionProcessor;
        private readonly OrchestrationRunnerConfiguration _configuration;
        private readonly IShutdownNotificationProvider _shutdownNotificationProvider;
        private readonly ILogger<OrchestrationRunner> _logger;

        public OrchestrationRunner(
            ISubscriptionProcessor<PackageValidationMessageData> subscriptionProcessor,
            IOptionsSnapshot<OrchestrationRunnerConfiguration> configurationAccessor,
            IShutdownNotificationProvider shutdownNotificationProvider,
            ILogger<OrchestrationRunner> logger)
        {
            _subscriptionProcessor = subscriptionProcessor  ?? throw new ArgumentNullException(nameof(subscriptionProcessor));
            configurationAccessor = configurationAccessor ?? throw new ArgumentNullException(nameof(configurationAccessor));
            _configuration = configurationAccessor.Value ?? throw new ArgumentException("Value property cannot be null", nameof(configurationAccessor));
            _shutdownNotificationProvider = shutdownNotificationProvider ?? throw new ArgumentNullException(nameof(shutdownNotificationProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RunOrchestrationAsync()
        {
            _logger.LogInformation("Starting up the orchestration");

            _subscriptionProcessor.Start();
            await Task.Delay(_configuration.ProcessRecycleInterval);

            _logger.LogInformation("Recycling the process...");
            _shutdownNotificationProvider.NotifyShutdownInitiated();
            var shutdownTask = _subscriptionProcessor.StartShutdownAsync();
            // make sure we don't block on waiting shutdownTask to finish
            if (await Task.WhenAny(shutdownTask, Task.Delay(_configuration.ShutdownWaitInterval)) != shutdownTask )
            {
                _logger.LogWarning("Failed to wait for shutdown initiation task to finish. Waited for {ShutdownWaitInterval}. Will proceed with task termination",
                    _configuration.ShutdownWaitInterval);
            }

            DateTimeOffset waitStart = DateTimeOffset.Now;

            while (DateTimeOffset.Now - waitStart < _configuration.ShutdownWaitInterval)
            {
                if (_subscriptionProcessor.NumberOfMessagesInProgress <= 0)
                {
                    break;
                }

                await Task.Delay(ShutdownLoopSleepTime);
            }

            int numStillRunning = _subscriptionProcessor.NumberOfMessagesInProgress;

            if (numStillRunning > 0)
            {
                _logger.LogWarning("There are still {StillRunningRequests} requests running after requesting shutdown and waiting for {ShutdownWaitInterval}",
                    numStillRunning,
                    _configuration.ShutdownWaitInterval);
            }
            else
            {
                _logger.LogInformation("All requests are finished");
            }
        }
    }
}

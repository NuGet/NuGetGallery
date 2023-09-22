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
        private readonly ILogger<OrchestrationRunner> _logger;

        public OrchestrationRunner(
            ISubscriptionProcessor<PackageValidationMessageData> subscriptionProcessor,
            IOptionsSnapshot<OrchestrationRunnerConfiguration> configurationAccessor,
            ILogger<OrchestrationRunner> logger)
        {
            _subscriptionProcessor = subscriptionProcessor  ?? throw new ArgumentNullException(nameof(subscriptionProcessor));
            configurationAccessor = configurationAccessor ?? throw new ArgumentNullException(nameof(configurationAccessor));
            _configuration = configurationAccessor.Value ?? throw new ArgumentException("Value property cannot be null", nameof(configurationAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RunOrchestrationAsync()
        {
            _logger.LogInformation("Starting up the orchestration");

            await _subscriptionProcessor.StartAsync(_configuration.MaxConcurrentCalls);
            await Task.Delay(_configuration.ProcessRecycleInterval);

            _logger.LogInformation("Recycling the process...");

            if (await _subscriptionProcessor.ShutdownAsync(_configuration.ShutdownWaitInterval))
            {
                _logger.LogInformation("Gracefully shutdown the Service Bus subscription processor");
            }
            else
            {
                _logger.LogWarning("Service Bus subscription processor did not shutdown gracefully");
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

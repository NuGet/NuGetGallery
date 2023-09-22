// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.ServiceBus;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class OrchestrationRunnerFacts
    {
        private const int DefaultMaxConcurrentCalls = 24;

        [Fact]
        public async Task StartsMessageProcessing()
        {
            var runner = CreateRunner();
            await runner.RunOrchestrationAsync();

            SubscriptionProcessorMock.Verify(o => o.StartAsync(DefaultMaxConcurrentCalls), Times.Once());
        }

        [Fact]
        public async Task ShutsDownMessageProcessing()
        {
            var startCalled = false;
            SubscriptionProcessorMock
                .Setup(o => o.StartAsync(DefaultMaxConcurrentCalls))
                .Returns(Task.CompletedTask)
                .Callback(() => startCalled = true);

            SubscriptionProcessorMock
                .Setup(o => o.ShutdownAsync(It.IsAny<TimeSpan>()))
                .Callback(() => Assert.True(startCalled))
                .Returns(Task.FromResult(true));
            var runner = CreateRunner();
            await runner.RunOrchestrationAsync();

            SubscriptionProcessorMock.Verify(o => o.ShutdownAsync(It.IsAny<TimeSpan>()), Times.Once());
        }

        [Fact(Skip = "Flaky test. Won't run it as part of CI.")]
        public async Task WaitsOrchestratorToShutDown()
        {
            SetupOptionsAccessorMock(TimeSpan.Zero, TimeSpan.FromSeconds(3), 2);

            int numberOfRequestsInProgress = 2;
            SubscriptionProcessorMock
                .SetupGet(o => o.NumberOfMessagesInProgress)
                .Returns(() => numberOfRequestsInProgress--);

            var runner = CreateRunner();
            await runner.RunOrchestrationAsync();

            SubscriptionProcessorMock.Verify(o => o.NumberOfMessagesInProgress, Times.AtLeast(3));
        }

        private Mock<IOptionsSnapshot<OrchestrationRunnerConfiguration>> SetupOptionsAccessorMock(
            TimeSpan processRecycleInterval,
            TimeSpan shutdownWaitInterval,
            int maxConcurrentCalls)
        {
            OrchestrationRunnerConfigurationAccessorMock = new Mock<IOptionsSnapshot<OrchestrationRunnerConfiguration>>();
            OrchestrationRunnerConfigurationAccessorMock
                .SetupGet(o => o.Value)
                .Returns(new OrchestrationRunnerConfiguration
                {
                    ProcessRecycleInterval = processRecycleInterval,
                    ShutdownWaitInterval = shutdownWaitInterval,
                    MaxConcurrentCalls = maxConcurrentCalls
                });
            return OrchestrationRunnerConfigurationAccessorMock;
        }

        private OrchestrationRunner CreateRunner()
            => new OrchestrationRunner(
                SubscriptionProcessorMock.Object,
                OrchestrationRunnerConfigurationAccessorMock.Object,
                LoggerMock.Object);

        public OrchestrationRunnerFacts()
        {
            SubscriptionProcessorMock = new Mock<ISubscriptionProcessor<PackageValidationMessageData>>();
            LoggerMock = new Mock<ILogger<OrchestrationRunner>>();
            SetupOptionsAccessorMock(TimeSpan.Zero, TimeSpan.Zero, DefaultMaxConcurrentCalls);
        }

        private Mock<ISubscriptionProcessor<PackageValidationMessageData>> SubscriptionProcessorMock { get; }
        private Mock<ILogger<OrchestrationRunner>> LoggerMock { get; }
        private Mock<IOptionsSnapshot<OrchestrationRunnerConfiguration>> OrchestrationRunnerConfigurationAccessorMock { get; set;  }
    }
}

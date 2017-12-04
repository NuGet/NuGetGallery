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
        [Fact]
        public async Task StartsMessageProcessing()
        {
            var subscriptionProcessor = new Mock<ISubscriptionProcessor<PackageValidationMessageData>>();
            var loggerMock = new Mock<ILogger<OrchestrationRunner>>();
            var optionsAccessorMock = CreateOptionsAccessorMock(TimeSpan.Zero, TimeSpan.Zero);

            var runner = new OrchestrationRunner(subscriptionProcessor.Object, optionsAccessorMock.Object, loggerMock.Object);
            await runner.RunOrchestrationAsync();

            subscriptionProcessor.Verify(o => o.Start(), Times.Once());
        }

        [Fact]
        public async Task ShutsDownMessageProcessing()
        {
            var orchestratorMock = new Mock<ISubscriptionProcessor<PackageValidationMessageData>>();
            var loggerMock = new Mock<ILogger<OrchestrationRunner>>();
            var optionsAccessorMock = CreateOptionsAccessorMock(TimeSpan.Zero, TimeSpan.Zero);

            var startCalled = false;
            orchestratorMock
                .Setup(o => o.Start())
                .Callback(() => startCalled = true);

            orchestratorMock
                .Setup(o => o.StartShutdownAsync())
                .Callback(() => Assert.True(startCalled))
                .Returns(Task.FromResult(0));
            var runner = new OrchestrationRunner(orchestratorMock.Object, optionsAccessorMock.Object, loggerMock.Object);
            await runner.RunOrchestrationAsync();

            orchestratorMock.Verify(o => o.StartShutdownAsync(), Times.Once());
        }

        [Fact(Skip = "Flaky test. Won't run it as part of CI.")]
        public async Task WaitsOrchestratorToShutDown()
        {
            var orchestratorMock = new Mock<ISubscriptionProcessor<PackageValidationMessageData>>();
            var loggerMock = new Mock<ILogger<OrchestrationRunner>>();
            var optionsAccessorMock = CreateOptionsAccessorMock(TimeSpan.Zero, TimeSpan.FromSeconds(3));

            int numberOfRequestsInProgress = 2;
            orchestratorMock
                .SetupGet(o => o.NumberOfMessagesInProgress)
                .Returns(() => numberOfRequestsInProgress--);

            var runner = new OrchestrationRunner(orchestratorMock.Object, optionsAccessorMock.Object, loggerMock.Object);
            await runner.RunOrchestrationAsync();

            orchestratorMock.Verify(o => o.NumberOfMessagesInProgress, Times.Exactly(3));
        }

        private static Mock<IOptionsSnapshot<OrchestrationRunnerConfiguration>> CreateOptionsAccessorMock(
            TimeSpan processRecycleInterval,
            TimeSpan shutdownWaitInterval)
        {
            var optionsAccessorMock = new Mock<IOptionsSnapshot<OrchestrationRunnerConfiguration>>();
            optionsAccessorMock
                .SetupGet(o => o.Value)
                .Returns(new OrchestrationRunnerConfiguration
                {
                    ProcessRecycleInterval = processRecycleInterval,
                    ShutdownWaitInterval = shutdownWaitInterval
                });
            return optionsAccessorMock;
        }
    }
}

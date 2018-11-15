// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Logging;
using NuGet.Services.Validation;
using Xunit;

namespace NuGet.Services.Revalidate.Tests.Services
{
    public class RevalidationServiceFacts
    {
        public class TheRunAsyncMethod : FactsBase
        {
            [Fact]
            public async Task ThrowsIfNotInitialized()
            {
                // Arrange
                _jobState
                    .Setup(s => s.IsInitializedAsync())
                    .ReturnsAsync(false);

                // Act
                var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.RunAsync());

                // Assert
                _telemetryService.Verify(t => t.TrackStartNextRevalidationOperation(), Times.Never);

                Assert.Equal("The revalidation service must be initialized before running revalidations", e.Message);
            }

            [Fact]
            public async Task OnUnrecoverableError_ShutsDown()
            {
                // Arrange
                // Configure the job to run for a very long time. The unrecoverable error causes the job to end after its first iteration.
                _config.ShutdownWaitInterval = TimeSpan.MaxValue;

                _jobState
                    .Setup(s => s.IsInitializedAsync())
                    .ReturnsAsync(true);

                _starter
                    .Setup(s => s.StartNextRevalidationsAsync())
                    .ReturnsAsync(StartRevalidationResult.UnrecoverableError);

                // Act
                await _target.RunAsync();

                // Assert
                _telemetryService.Verify(t => t.TrackStartNextRevalidationOperation(), Times.Once);
                _scopeFactory.Verify(f => f.CreateScope(), Times.Once);

                Assert.Equal(StartRevalidationStatus.UnrecoverableError, _operation.Properties.Result);
            }

            [Fact]
            public async Task OnRetryLater_CallsThrottlerCallback()
            {
                // Arrange
                _jobState
                    .Setup(s => s.IsInitializedAsync())
                    .ReturnsAsync(true);

                _starter
                    .Setup(s => s.StartNextRevalidationsAsync())
                    .ReturnsAsync(StartRevalidationResult.RetryLater);

                // Act & Assert
                await _target.RunAsync();

                _telemetryService.Verify(t => t.TrackStartNextRevalidationOperation(), Times.Once);
                _scopeFactory.Verify(f => f.CreateScope(), Times.Once);
                _throttler.Verify(t => t.DelayUntilRevalidationRetryAsync(), Times.Once);

                Assert.Equal(StartRevalidationStatus.RetryLater, _operation.Properties.Result);
            }

            [Fact]
            public async Task OnRevalidationEnqueued_CallsThrottlerCallback()
            {
                // Arrange
                _jobState
                    .Setup(s => s.IsInitializedAsync())
                    .ReturnsAsync(true);

                _starter
                    .Setup(s => s.StartNextRevalidationsAsync())
                    .ReturnsAsync(StartRevalidationResult.RevalidationsEnqueued(123));

                // Act & Assert
                await _target.RunAsync();

                _telemetryService.Verify(t => t.TrackStartNextRevalidationOperation(), Times.Once);
                _scopeFactory.Verify(f => f.CreateScope(), Times.Once);
                _throttler.Verify(t => t.DelayUntilNextRevalidationAsync(123, It.IsAny<TimeSpan>()), Times.Once);

                Assert.Equal(StartRevalidationStatus.RevalidationsEnqueued, _operation.Properties.Result);
            }
        }

        public class FactsBase
        {
            protected readonly Mock<IRevalidationJobStateService> _jobState;
            protected readonly Mock<IRevalidationThrottler> _throttler;
            protected readonly Mock<IServiceScopeFactory> _scopeFactory;
            protected readonly Mock<ITelemetryService> _telemetryService;

            protected readonly Mock<IRevalidationStarter> _starter;
            protected readonly RevalidationConfiguration _config;
            protected readonly PackageRevalidation _revalidation;

            protected readonly DurationMetric<StartNextRevalidationOperation> _operation;

            public RevalidationService _target;

            public FactsBase()
            {
                _jobState = new Mock<IRevalidationJobStateService>();
                _throttler = new Mock<IRevalidationThrottler>();
                _scopeFactory = new Mock<IServiceScopeFactory>();
                _telemetryService = new Mock<ITelemetryService>();

                _starter = new Mock<IRevalidationStarter>();

                var scope = new Mock<IServiceScope>();
                var provider = new Mock<IServiceProvider>();
                _scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
                scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
                provider.Setup(p => p.GetService(typeof(IRevalidationStarter))).Returns(_starter.Object);

                _operation = new DurationMetric<StartNextRevalidationOperation>(
                    Mock.Of<ITelemetryClient>(),
                    "Name",
                    new StartNextRevalidationOperation(),
                    Mock.Of<Func<StartNextRevalidationOperation, IDictionary<string, string>>>());

                _telemetryService
                    .Setup(t => t.TrackStartNextRevalidationOperation())
                    .Returns(_operation);

                _config = new RevalidationConfiguration
                {
                    ShutdownWaitInterval = TimeSpan.MinValue,
                };

                _revalidation = new PackageRevalidation
                {
                    PackageId = "Foo.Bar",
                    PackageNormalizedVersion = "1.2.3",
                    ValidationTrackingId = Guid.NewGuid()
                };

                _target = new RevalidationService(
                    _jobState.Object,
                    _throttler.Object,
                    _scopeFactory.Object,
                    _config,
                    _telemetryService.Object,
                    Mock.Of<ILogger<RevalidationService>>());
            }

            protected void Setup(
                bool isInitialized = true,
                bool killswitchActive = false,
                bool isThrottled = false,
                bool initializedThrows = false)
            {
                _jobState.Setup(s => s.IsInitializedAsync()).ReturnsAsync(isInitialized);
                _jobState.Setup(s => s.IsKillswitchActiveAsync()).ReturnsAsync(killswitchActive);
                _throttler.Setup(t => t.IsThrottledAsync()).ReturnsAsync(isThrottled);

                var exception = new Exception();

                if (initializedThrows) _jobState.Setup(s => s.IsInitializedAsync()).ThrowsAsync(exception);
            }
        }
    }
}

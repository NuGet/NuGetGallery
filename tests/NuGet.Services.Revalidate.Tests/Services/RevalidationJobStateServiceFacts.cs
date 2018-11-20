// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Revalidate.Tests.Services
{
    public class RevalidationJobStateServiceFacts
    {
        public class TheIsInitializedAsyncMethod : FactsBase
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsInitializedValue(bool isInitialized)
            {
                _state
                    .Setup(s => s.GetStateAsync())
                    .ReturnsAsync(new RevalidationState
                    {
                        IsInitialized = isInitialized
                    });

                Assert.Equal(isInitialized, await _target.IsInitializedAsync());

                _state.Verify(s => s.GetStateAsync(), Times.Once);
            }
        }

        public class TheMarkAsInitializedAsyncMethod : FactsBase
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task UpdatesState(bool isInitialized)
            {
                var result = new RevalidationState
                {
                    IsInitialized = isInitialized
                };

                _state
                    .Setup(s => s.UpdateStateAsync(It.IsAny<Action<RevalidationState>>()))
                    .Callback((Action<RevalidationState> a) => a(result))
                    .Returns(Task.CompletedTask);

                await _target.MarkAsInitializedAsync();

                _state.Verify(
                    s => s.UpdateStateAsync(It.IsAny<Action<RevalidationState>>()),
                    Times.Once);

                Assert.True(result.IsInitialized);
            }
        }

        public class TheIsKillswitchActiveAsyncMethod : FactsBase
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsIsKillswitchActiveValue(bool isKillswitchActive)
            {
                _state
                    .Setup(s => s.GetStateAsync())
                    .ReturnsAsync(new RevalidationState
                    {
                        IsKillswitchActive = isKillswitchActive
                    });

                Assert.Equal(isKillswitchActive, await _target.IsKillswitchActiveAsync());

                _state.Verify(s => s.GetStateAsync(), Times.Once);
            }
        }

        public class TheGetDesiredPackageEventRateAsyncMethod : FactsBase
        {
            [Fact]
            public async Task ReturnsDesiredPackageEventRateValue()
            {
                // Arrange
                bool? result = null;
                var state = new RevalidationState
                {
                    DesiredPackageEventRate = 123
                };

                _state
                    .Setup(s => s.MaybeUpdateStateAsync(It.IsAny<Func<RevalidationState, bool>>()))
                    .Callback((Func<RevalidationState, bool> a) =>
                    {
                        result = a(state);
                    })
                    .ReturnsAsync(state);

                // Act & Assert
                Assert.Equal(123, await _target.GetDesiredPackageEventRateAsync());
                Assert.False(result);

                _state.Verify(s => s.MaybeUpdateStateAsync(It.IsAny<Func<RevalidationState, bool>>()), Times.Once);
            }

            [Theory]
            [InlineData(0, 100)]
            [InlineData(600, 500)]
            public async Task UpdatesRateIfCurrentRateOutsideofConfiguredBounds(int storedRate, int expectedRate)
            {
                // Arrange
                bool? result = null;
                var state = new RevalidationState
                {
                    DesiredPackageEventRate = storedRate
                };

                _state
                    .Setup(s => s.MaybeUpdateStateAsync(It.IsAny<Func<RevalidationState, bool>>()))
                    .Callback((Func<RevalidationState, bool> a) =>
                    {
                        result = a(state);
                    })
                    .ReturnsAsync(state);

                // Act & Assert
                Assert.Equal(expectedRate, await _target.GetDesiredPackageEventRateAsync());
                Assert.True(result);

                _state.Verify(s => s.MaybeUpdateStateAsync(It.IsAny<Func<RevalidationState, bool>>()), Times.Once);
            }
        }

        public class TheResetDesiredPackageEventRateAsyncMethod : FactsBase
        {
            [Fact]
            public async Task UpdatesState()
            {
                var result = new RevalidationState
                {
                    DesiredPackageEventRate = 123
                };

                _state
                    .Setup(s => s.UpdateStateAsync(It.IsAny<Action<RevalidationState>>()))
                    .Callback((Action<RevalidationState> a) => a(result))
                    .Returns(Task.CompletedTask);

                await _target.ResetDesiredPackageEventRateAsync();

                _state.Verify(
                    s => s.UpdateStateAsync(It.IsAny<Action<RevalidationState>>()),
                    Times.Once);

                Assert.Equal(100, result.DesiredPackageEventRate);
            }
        }

        public class TheIncreaseDesiredPackageEventRateAsyncMethod : FactsBase
        {
            [Fact]
            public async Task IncreasesDesiredPackageEventRateValue()
            {
                // Arrange
                bool? result = null;
                var state = new RevalidationState
                {
                    DesiredPackageEventRate = 123
                };

                _state
                    .Setup(s => s.MaybeUpdateStateAsync(It.IsAny<Func<RevalidationState, bool>>()))
                    .Callback((Func<RevalidationState, bool> a) =>
                    {
                        result = a(state);
                    })
                    .ReturnsAsync(state);

                // Act
                await _target.IncreaseDesiredPackageEventRateAsync();

                // Assert
                Assert.Equal(143, state.DesiredPackageEventRate);
                Assert.True(result);

                _state.Verify(s => s.MaybeUpdateStateAsync(It.IsAny<Func<RevalidationState, bool>>()), Times.Once);
            }

            [Fact]
            public async Task DoesntIncreaseIfReachedConfiguredMaxRate()
            {
                // Arrange
                bool? result = null;
                var state = new RevalidationState
                {
                    DesiredPackageEventRate = 500
                };

                _state
                    .Setup(s => s.MaybeUpdateStateAsync(It.IsAny<Func<RevalidationState, bool>>()))
                    .Callback((Func<RevalidationState, bool> a) =>
                    {
                        result = a(state);
                    })
                    .ReturnsAsync(state);

                // Act
                await _target.IncreaseDesiredPackageEventRateAsync();

                // Assert
                Assert.Equal(500, state.DesiredPackageEventRate);
                Assert.False(result);

                _state.Verify(s => s.MaybeUpdateStateAsync(It.IsAny<Func<RevalidationState, bool>>()), Times.Once);
            }

            [Fact]
            public async Task ResetsToConfiguredMaxRateIfPastConfiguredMax()
            {
                // Arrange
                bool? result = null;
                var state = new RevalidationState
                {
                    DesiredPackageEventRate = 600
                };

                _state
                    .Setup(s => s.MaybeUpdateStateAsync(It.IsAny<Func<RevalidationState, bool>>()))
                    .Callback((Func<RevalidationState, bool> a) =>
                    {
                        result = a(state);
                    })
                    .ReturnsAsync(state);

                // Act
                await _target.IncreaseDesiredPackageEventRateAsync();

                // Assert
                Assert.Equal(500, state.DesiredPackageEventRate);
                Assert.True(result);

                _state.Verify(s => s.MaybeUpdateStateAsync(It.IsAny<Func<RevalidationState, bool>>()), Times.Once);
            }
        }

        public class FactsBase
        {
            protected readonly Mock<IRevalidationStateService> _state;
            protected readonly RevalidationConfiguration _config;

            protected readonly RevalidationJobStateService _target;

            public FactsBase()
            {
                _state = new Mock<IRevalidationStateService>();

                _config = new RevalidationConfiguration
                {
                    MinPackageEventRate = 100,
                    MaxPackageEventRate = 500,

                    Queue = new RevalidationQueueConfiguration
                    {
                        MaxBatchSize = 20
                    }
                };

                _target = new RevalidationJobStateService(
                    _state.Object,
                    _config,
                    Mock.Of<ILogger<RevalidationJobStateService>>());
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGet.Services.Revalidate.Tests.Services
{
    public class RevalidationThrottlerFacts
    {
        public class TheIsThrottledAsyncMethod
        {
            private readonly Mock<IRevalidationJobStateService> _settings;
            private readonly Mock<IPackageRevalidationStateService> _state;
            private readonly RevalidationConfiguration _config;

            private readonly IRevalidationThrottler _target;

            public TheIsThrottledAsyncMethod()
            {
                _settings = new Mock<IRevalidationJobStateService>();
                _state = new Mock<IPackageRevalidationStateService>();

                _config = new RevalidationConfiguration();

                _target = new RevalidationThrottler(
                    _settings.Object,
                    _state.Object,
                    _config,
                    Mock.Of<ILogger<RevalidationThrottler>>());
            }

            [Fact]
            public async Task ReturnsTrueIfRecentRevalidationsMoreThanDesiredRate()
            {
                // Arrange
                _settings.Setup(s => s.GetDesiredPackageEventRateAsync()).ReturnsAsync(50);
                _state.Setup(s => s.CountRevalidationsEnqueuedInPastHourAsync()).ReturnsAsync(100);

                // Act & Assert
                Assert.True(await _target.IsThrottledAsync());

                _settings.Verify(s => s.GetDesiredPackageEventRateAsync(), Times.Once);
                _state.Verify(s => s.CountRevalidationsEnqueuedInPastHourAsync(), Times.Once);
            }

            [Fact]
            public async Task ReturnsFalseIfRecentRevalidationsLessThanDesiredRate()
            {
                // Arrange
                _settings.Setup(s => s.GetDesiredPackageEventRateAsync()).ReturnsAsync(100);
                _state.Setup(s => s.CountRevalidationsEnqueuedInPastHourAsync()).ReturnsAsync(50);

                // Act & Assert
                Assert.False(await _target.IsThrottledAsync());

                _settings.Verify(s => s.GetDesiredPackageEventRateAsync(), Times.Once);
                _state.Verify(s => s.CountRevalidationsEnqueuedInPastHourAsync(), Times.Once);
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Validation;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class AlwaysSucceedingValidatorFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void ThrowsWhenDisabled()
            {
                Assert.Throws<InvalidOperationException>(() => CreateTarget(enabled: false));
            }

            [Fact]
            public void ThrowsWhenDelayIsNegative()
            {
                Assert.Throws<InvalidOperationException>(
                    () => CreateTarget(enabled: true, TimeSpan.FromSeconds(-1)));
            }
        }

        public class TheStartAsyncMethod
        {
            [Fact]
            public async Task ReturnsSucceededWhenEnabled()
            {
                var target = CreateTarget(enabled: true);

                var response = await target.StartAsync(Mock.Of<INuGetValidationRequest>());

                Assert.Equal(ValidationStatus.Succeeded, response.Status);
            }

            [Fact]
            public async Task WaitsForConfiguredDelay()
            {
                var delay = TimeSpan.FromMilliseconds(100);
                var target = CreateTarget(enabled: true, delay);
                var stopwatch = Stopwatch.StartNew();

                var response = await target.StartAsync(Mock.Of<INuGetValidationRequest>());

                Assert.Equal(ValidationStatus.Succeeded, response.Status);
                Assert.True(stopwatch.Elapsed >= delay);
            }

            [Fact]
            public async Task ThrowsForNullRequest()
            {
                var target = CreateTarget(enabled: true);

                await Assert.ThrowsAsync<ArgumentNullException>(() => target.StartAsync(null));
            }
        }

        public class TheGetResponseAsyncMethod
        {
            [Fact]
            public async Task ReturnsNotStartedWhenEnabled()
            {
                var target = CreateTarget(enabled: true);

                var response = await target.GetResponseAsync(Mock.Of<INuGetValidationRequest>());

                Assert.Equal(ValidationStatus.NotStarted, response.Status);
            }

            [Fact]
            public void ThrowsForNullRequest()
            {
                var target = CreateTarget(enabled: true);

                Assert.Throws<ArgumentNullException>(() =>
                {
                    _ = target.GetResponseAsync(null);
                });
            }
        }

        private static AlwaysSucceedingValidator CreateTarget(bool enabled, TimeSpan? delay = null)
        {
            var configurationAccessor = new Mock<IOptionsSnapshot<AlwaysSucceedingValidatorConfiguration>>();
            configurationAccessor
                .SetupGet(x => x.Value)
                .Returns(new AlwaysSucceedingValidatorConfiguration
                {
                    Enabled = enabled,
                    Delay = delay ?? TimeSpan.Zero,
                });

            return new AlwaysSucceedingValidator(configurationAccessor.Object);
        }
    }
}

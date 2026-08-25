// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
            public async Task ReturnsIncomplete()
            {
                var target = CreateTarget(enabled: true);

                var response = await target.StartAsync(CreateRequest());

                Assert.Equal(ValidationStatus.Incomplete, response.Status);
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
            public async Task ReturnsNotStartedForUnknownValidation()
            {
                var target = CreateTarget(enabled: true);

                var response = await target.GetResponseAsync(CreateRequest());

                Assert.Equal(ValidationStatus.NotStarted, response.Status);
            }

            [Fact]
            public async Task ReturnsIncompleteBeforeDelayHasElapsed()
            {
                var request = CreateRequest();
                var target = CreateTarget(enabled: true, TimeSpan.FromMinutes(1));
                await target.StartAsync(request);

                var response = await target.GetResponseAsync(request);

                Assert.Equal(ValidationStatus.Incomplete, response.Status);
            }

            [Fact]
            public async Task ReturnsSucceededAfterDelayHasElapsed()
            {
                var request = CreateRequest();
                var target = CreateTarget(enabled: true);
                await target.StartAsync(request);

                var response = await target.GetResponseAsync(request);

                Assert.Equal(ValidationStatus.Succeeded, response.Status);
            }

            [Fact]
            public async Task TracksValidationsIndependently()
            {
                var startedRequest = CreateRequest();
                var unknownRequest = CreateRequest();
                var target = CreateTarget(enabled: true, TimeSpan.FromMinutes(1));
                await target.StartAsync(startedRequest);

                var startedResponse = await target.GetResponseAsync(startedRequest);
                var unknownResponse = await target.GetResponseAsync(unknownRequest);

                Assert.Equal(ValidationStatus.Incomplete, startedResponse.Status);
                Assert.Equal(ValidationStatus.NotStarted, unknownResponse.Status);
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

        private static INuGetValidationRequest CreateRequest()
        {
            var request = new Mock<INuGetValidationRequest>();
            request.SetupGet(x => x.ValidationId).Returns(Guid.NewGuid());
            return request.Object;
        }

        private static AlwaysSucceedingValidator CreateTarget(bool enabled, TimeSpan? delay = null)
        {
            var configurationAccessor = new Mock<IOptions<AlwaysSucceedingValidatorConfiguration>>();
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

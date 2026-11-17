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
    public class DevelopmentValidatorFacts
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
                    () => CreateTarget(enabled: true, delaySeconds: -1));
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
                var target = CreateTarget(enabled: true, delaySeconds: 60);
                await target.StartAsync(request);

                var response = await target.GetResponseAsync(request);

                Assert.Equal(ValidationStatus.Incomplete, response.Status);
            }

            [Fact]
            public async Task ReturnsSucceededAfterDelayHasElapsed()
            {
                var request = CreateRequest("ValidationPass.Example");
                var target = CreateTarget(enabled: true);
                await target.StartAsync(request);

                var response = await target.GetResponseAsync(request);

                Assert.Equal(ValidationStatus.Succeeded, response.Status);
            }

            [Fact]
            public async Task ReturnsFailedForConfiguredPackageIdPrefix()
            {
                var request = CreateRequest("validationfail.example");
                var target = CreateTarget(enabled: true, failurePackageIdPrefix: "ValidationFail.");
                await target.StartAsync(request);

                var response = await target.GetResponseAsync(request);

                Assert.Equal(ValidationStatus.Failed, response.Status);
                Assert.Equal(ValidationIssueCode.Unknown, Assert.Single(response.Issues).IssueCode);
            }

            [Fact]
            public async Task TracksValidationsIndependently()
            {
                var startedRequest = CreateRequest();
                var unknownRequest = CreateRequest();
                var target = CreateTarget(enabled: true, delaySeconds: 60);
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

        private static INuGetValidationRequest CreateRequest(string packageId = "Example.Package")
        {
            var request = new Mock<INuGetValidationRequest>();
            request.SetupGet(x => x.ValidationId).Returns(Guid.NewGuid());
            request.SetupGet(x => x.PackageId).Returns(packageId);
            return request.Object;
        }

        private static DevelopmentValidator CreateTarget(
            bool enabled,
            int delaySeconds = 0,
            string failurePackageIdPrefix = "ValidationFail.")
        {
            var configurationAccessor = new Mock<IOptions<DevelopmentValidatorConfiguration>>();
            configurationAccessor
                .SetupGet(x => x.Value)
                .Returns(new DevelopmentValidatorConfiguration
                {
                    Enabled = enabled,
                    DelaySeconds = delaySeconds,
                    FailurePackageIdPrefix = failurePackageIdPrefix,
                });

            return new DevelopmentValidator(configurationAccessor.Object);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Moq;
using Xunit;

namespace NuGet.Services.SearchService
{
    public class AzureWebAppTelemetryInitializerFacts
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("hello", "hello")]
        [InlineData("-staging-test", "-staging-test")]
        [InlineData("hello-staging", "hello")]
        [InlineData("hello-sTAGing", "hello")]
        public void UpdatesRoleName(string input, string expected)
        {
            var telemetry = new Mock<ITelemetry>();
            var context = new TelemetryContext();

            context.Cloud.RoleName = input;

            telemetry.Setup(t => t.Context).Returns(context);

            var target = new AzureWebAppTelemetryInitializer();
            target.Initialize(telemetry.Object);

            Assert.Equal(expected, context.Cloud.RoleName);
        }
    }
}

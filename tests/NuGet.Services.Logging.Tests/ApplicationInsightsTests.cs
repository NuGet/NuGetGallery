// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Services.Logging.Tests
{
    public class ApplicationInsightsTests
    {
        private const string InstrumentationKey = "abcdef12-3456-7890-abcd-ef123456789";

        [Fact]
        public void InitializeReturnsApplicationInsightsConfiguration()
        {
            var applicationInsightsConfiguration = ApplicationInsights.Initialize(InstrumentationKey);

            Assert.NotNull(applicationInsightsConfiguration);
            Assert.Equal(InstrumentationKey, applicationInsightsConfiguration.TelemetryConfiguration.InstrumentationKey);
        }

        [Fact]
        public void InitializeRegistersTelemetryContextInitializer()
        {
            var applicationInsightsConfiguration = ApplicationInsights.Initialize(InstrumentationKey);
            Assert.Contains(applicationInsightsConfiguration.TelemetryConfiguration.TelemetryInitializers, ti => ti is TelemetryContextInitializer);
        }

        [Fact]
        public void InitializeSetsHeartbeatIntervalAndDiagnosticsTelemetryModule()
        {
            var heartbeatInterval = TimeSpan.FromMinutes(1);

            var applicationInsightsConfiguration = ApplicationInsights.Initialize(InstrumentationKey, heartbeatInterval);

            Assert.NotNull(applicationInsightsConfiguration.DiagnosticsTelemetryModule);
            Assert.Equal(heartbeatInterval, applicationInsightsConfiguration.DiagnosticsTelemetryModule.HeartbeatInterval);
        }
    }
}

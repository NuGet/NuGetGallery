// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Moq;
using Ng;
using Xunit;

namespace NgTests
{
    public class JobNameTelemetryInitializerTests
    {
        [Fact]
        public void Constructor_WhenJobNameIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new JobNameTelemetryInitializer(jobName: null));

            Assert.Equal("jobName", exception.ParamName);
        }

        [Fact]
        public void Initialize_WhenTelemetryIsNotNull_SetsJobName()
        {
            var telemetryContext = new TelemetryContext();
            var telemetry = new Mock<ITelemetry>();

            telemetry.SetupGet(x => x.Context)
                .Returns(telemetryContext);

            var initializer = new JobNameTelemetryInitializer(jobName: "a");

            initializer.Initialize(telemetry.Object);

            Assert.Equal(1, telemetryContext.Properties.Count);
            Assert.Equal("a", telemetryContext.Properties["JobName"]);
        }
    }
}
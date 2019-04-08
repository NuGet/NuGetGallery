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
                () => new JobNameTelemetryInitializer(jobName: null, instanceName: "instanceName"));

            Assert.Equal("jobName", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenInstanceNameIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new JobNameTelemetryInitializer(jobName: "jobName", instanceName: null));

            Assert.Equal("instanceName", exception.ParamName);
        }

        [Fact]
        public void Initialize_WhenTelemetryIsNotNull_SetsJobNameAndInstanceName()
        {
            var telemetryContext = new TelemetryContext();
            var telemetry = new Mock<ITelemetry>();

            telemetry.SetupGet(x => x.Context)
                .Returns(telemetryContext);

            var initializer = new JobNameTelemetryInitializer(jobName: "a", instanceName: "b");

            initializer.Initialize(telemetry.Object);

            Assert.Equal(2, telemetryContext.Properties.Count);
            Assert.Equal("a", telemetryContext.Properties["JobName"]);
            Assert.Equal("b", telemetryContext.Properties["InstanceName"]);
        }
    }
}
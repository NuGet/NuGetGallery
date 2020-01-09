// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Moq;
using Xunit;

namespace NuGet.Services.Logging.Tests
{
    public class JobPropertiesTelemetryInitializerTests
    {
        private readonly TelemetryContext _telemetryContext = new TelemetryContext();
        private readonly Mock<ITelemetry> _telemetry = new Mock<ITelemetry>();

        public JobPropertiesTelemetryInitializerTests()
        {
            _telemetry.SetupGet(x => x.Context)
                .Returns(_telemetryContext);
        }

        [Fact]
        public void Constructor_WhenJobNameIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new JobPropertiesTelemetryInitializer(
                    jobName: null,
                    instanceName: "instanceName",
                    globalDimensions: new Dictionary<string, string>()));

            Assert.Equal("jobName", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenInstanceNameIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new JobPropertiesTelemetryInitializer(
                    jobName: "jobName",
                    instanceName: null,
                    globalDimensions: new Dictionary<string, string>()));

            Assert.Equal("instanceName", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenGlobalDimensionsIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new JobPropertiesTelemetryInitializer(
                    jobName: "jobName",
                    instanceName: "instanceName",
                    globalDimensions: null));

            Assert.Equal("globalDimensions", exception.ParamName);
        }

        [Fact]
        public void Initialize_WhenGlobalDimensionsIsEmpty_SetsJobNameAndInstanceName()
        {
            var telemetry = new TestableTelemetry();

            var initializer = new JobPropertiesTelemetryInitializer(
                jobName: "a",
                instanceName: "b",
                globalDimensions: new Dictionary<string, string>());

            initializer.Initialize(telemetry);

            Assert.Equal(2, telemetry.Properties.Count);
            Assert.Equal("a", telemetry.Properties["JobName"]);
            Assert.Equal("b", telemetry.Properties["InstanceName"]);
        }

        [Fact]
        public void Initialize_WhenGlobalDimensionsIsNotEmpty_SetsTelemetry()
        {
            var globalDimensions = new Dictionary<string, string>()
            {
                { "a", "b" },
                { "c", "d" }
            };
            var telemetry = new TestableTelemetry();

            var initializer = new JobPropertiesTelemetryInitializer(
                jobName: "jobName",
                instanceName: "instanceName",
                globalDimensions: globalDimensions);

            initializer.Initialize(telemetry);

            Assert.Equal(4, telemetry.Properties.Count);
            Assert.Equal("jobName", telemetry.Properties["JobName"]);
            Assert.Equal("instanceName", telemetry.Properties["InstanceName"]);
            Assert.Equal("b", telemetry.Properties["a"]);
            Assert.Equal("d", telemetry.Properties["c"]);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
            var telemetry = new TestableTelemetry();

            var initializer = new JobNameTelemetryInitializer(jobName: "a", instanceName: "b");

            initializer.Initialize(telemetry);

            Assert.Equal(2, telemetry.Properties.Count);
            Assert.Equal("a", telemetry.Properties["JobName"]);
            Assert.Equal("b", telemetry.Properties["InstanceName"]);
        }
    }
}
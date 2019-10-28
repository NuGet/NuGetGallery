// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Moq;
using Ng;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace NgTests
{
    public class JobPropertiesTelemetryInitializerTests
    {
        private readonly Dictionary<string, string> _globalDimensions;
        private readonly TelemetryContext _telemetryContext;
        private readonly Mock<ITelemetry> _telemetry;
        private readonly Mock<ITelemetryService> _telemetryService;

        public JobPropertiesTelemetryInitializerTests()
        {
            _globalDimensions = new Dictionary<string, string>();
            _telemetryContext = new TelemetryContext();
            _telemetry = new Mock<ITelemetry>();
            _telemetryService = new Mock<ITelemetryService>();

            _telemetry.SetupGet(x => x.Context)
                .Returns(_telemetryContext);
        }

        [Fact]
        public void Constructor_WhenTelemetryServiceIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new JobPropertiesTelemetryInitializer(telemetryService: null));

            Assert.Equal("telemetryService", exception.ParamName);
        }

        [Fact]
        public void Initialize_WhenGlobalDimensionsIsNull_DoesNothing()
        {
            _telemetryService.SetupGet(x => x.GlobalDimensions)
                .Returns((IDictionary<string, string>)null);

            var initializer = new JobPropertiesTelemetryInitializer(_telemetryService.Object);

            initializer.Initialize(_telemetry.Object);

            Assert.Empty(_telemetryContext.GlobalProperties);
        }

        [Fact]
        public void Initialize_WhenGlobalDimensionsIsEmpty_DoesNothing()
        {
            _telemetryService.SetupGet(x => x.GlobalDimensions)
                .Returns(_globalDimensions);

            var telemetry = new TestableTelemetry();

            var initializer = new JobPropertiesTelemetryInitializer(_telemetryService.Object);

            initializer.Initialize(telemetry);

            Assert.Empty(_telemetryContext.GlobalProperties);
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
            var telemetryService = new Mock<ITelemetryService>();

            telemetryService.SetupGet(x => x.GlobalDimensions)
                .Returns(globalDimensions);

            var initializer = new JobPropertiesTelemetryInitializer(telemetryService.Object);

            initializer.Initialize(telemetry);

            Assert.Equal(2, telemetry.Properties.Count);
            Assert.Equal("b", telemetry.Properties["a"]);
            Assert.Equal("d", telemetry.Properties["c"]);
        }
    }
}
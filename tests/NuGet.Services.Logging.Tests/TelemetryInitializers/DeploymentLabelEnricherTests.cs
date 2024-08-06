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
    public class DeploymentLabelEnricherTests
    {
        const string DeploymentLabel = "TestDeploymentLabel";
        const string DeploymentLabelPropertyName = "DeploymentLabel";

        private DeploymentLabelEnricher Target { get; }

        [Fact]
        public void ConstructorThrowsWhenDeploymentLabelNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new DeploymentLabelEnricher(deploymentLabel: null));
            Assert.Equal("propertyValue", ex.ParamName);
        }

        [Fact]
        public void WorksWithAnyITelemetry()
        {
            var telemetryMock = new Mock<ITelemetry>();
            var ex = Record.Exception(() => Target.Initialize(telemetryMock.Object));
            Assert.Null(ex);
        }

        [Fact]
        public void DoesNotOverwriteExistingLabel()
        {
            var telemetryMock = new Mock<ITelemetry>();
            var propertiesMock = new Mock<IDictionary<string, string>>();
            telemetryMock
                .As<ISupportProperties>()
                .SetupGet(sp => sp.Properties)
                .Returns(propertiesMock.Object);
            propertiesMock
                .Setup(p => p.ContainsKey(DeploymentLabelPropertyName))
                .Returns(true);
            Target.Initialize(telemetryMock.Object);

            propertiesMock
                .Verify(p => p.ContainsKey(DeploymentLabelPropertyName), Times.Once);
            propertiesMock
                .VerifyNoOtherCalls();
        }

        [Fact]
        public void StoresDeploymentLabel()
        {
            var telemetryMock = new Mock<ITelemetry>();
            var propertiesMock = new Mock<IDictionary<string, string>>();
            telemetryMock
                .As<ISupportProperties>()
                .SetupGet(sp => sp.Properties)
                .Returns(propertiesMock.Object);
            propertiesMock
                .Setup(p => p.ContainsKey(DeploymentLabelPropertyName))
                .Returns(false);
            Target.Initialize(telemetryMock.Object);

            propertiesMock
                .Verify(p => p.ContainsKey(DeploymentLabelPropertyName), Times.Once);
            propertiesMock
                .Verify(p => p.Add(DeploymentLabelPropertyName, DeploymentLabel), Times.Once);
        }

        public DeploymentLabelEnricherTests()
        {
            Target = new DeploymentLabelEnricher(DeploymentLabel);
        }
    }
}

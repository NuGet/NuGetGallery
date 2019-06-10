// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class DeploymentIdTelemetryEnricherFacts
    {
        private const string PropertyKey = "CloudDeploymentId";

        public DeploymentIdTelemetryEnricherFacts()
        {
            Telemetry = new Mock<ITelemetry>();
            Telemetry.Setup(x => x.Context).Returns(new TelemetryContext());
            Target = new Mock<TestableDeploymentIdTelemetryEnricher>();
            Target.Object.SetDeploymentId("TheDeploymentId");
        }

        public Mock<ITelemetry> Telemetry { get; }
        public Mock<TestableDeploymentIdTelemetryEnricher> Target { get; }

        [Fact]
        public void DoesNothingWithNullTelemetry()
        {
            Target.Object.Initialize(telemetry: null);

            Target.Verify(x => x.DeploymentId, Times.Never);
            Assert.DoesNotContain(PropertyKey, Telemetry.Object.Context.Properties.Keys);
        }

        [Fact]
        public void DoesNothingWhenDeploymentIdIsNull()
        {
            Target.Object.SetDeploymentId(null);

            Target.Object.Initialize(Telemetry.Object);

            Target.Verify(x => x.DeploymentId, Times.Never);
            Assert.DoesNotContain(PropertyKey, Telemetry.Object.Context.Properties.Keys);
        }

        [Fact]
        public void DoesNothingWhenDeploymentIdIsAlreadySet()
        {
            Telemetry.Object.Context.Properties[PropertyKey] = "something else";

            Target.Object.Initialize(Telemetry.Object);

            Target.Verify(x => x.DeploymentId, Times.Never);
            Assert.Equal(Telemetry.Object.Context.Properties[PropertyKey], "something else");
        }

        [Fact]
        public void SetsDeploymentId()
        {
            Target.Object.Initialize(Telemetry.Object);

            Assert.Equal(Telemetry.Object.Context.Properties[PropertyKey], "TheDeploymentId");
        }

        public class TestableDeploymentIdTelemetryEnricher : DeploymentIdTelemetryEnricher
        {
            private string _deploymentId;

            public void SetDeploymentId(string deploymentId)
            {
                _deploymentId = deploymentId;
            }

            internal override string DeploymentId => _deploymentId;
        }
    }
}

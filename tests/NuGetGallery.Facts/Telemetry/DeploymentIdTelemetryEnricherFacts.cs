// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Moq;
using Xunit;

namespace NuGetGallery.Telemetry
{
    public class DeploymentIdTelemetryEnricherFacts
    {
        private const string PropertyKey = "CloudDeploymentId";

        public DeploymentIdTelemetryEnricherFacts()
        {
            Telemetry = new TestableTelemetry();
            Target = new Mock<TestableDeploymentIdTelemetryEnricher>();
            Target.Object.SetDeploymentId("TheDeploymentId");
        }

        public TestableTelemetry Telemetry { get; }
        public Mock<TestableDeploymentIdTelemetryEnricher> Target { get; }

        [Fact]
        public void DoesNothingWithNullTelemetry()
        {
            Target.Object.Initialize(telemetry: null);

            Target.Verify(x => x.DeploymentId, Times.Never);
            Assert.DoesNotContain(PropertyKey, Telemetry.Properties.Keys);
        }

        [Fact]
        public void DoesNothingWhenDeploymentIdIsNull()
        {
            Target.Object.SetDeploymentId(null);

            Target.Object.Initialize(Telemetry);

            Target.Verify(x => x.DeploymentId, Times.Never);
            Assert.DoesNotContain(PropertyKey, Telemetry.Properties.Keys);
        }

        [Fact]
        public void DoesNothingWhenDeploymentIdIsAlreadySet()
        {
            Telemetry.Properties[PropertyKey] = "something else";

            Target.Object.Initialize(Telemetry);

            Target.Verify(x => x.DeploymentId, Times.Never);
            Assert.Equal(Telemetry.Properties[PropertyKey], "something else");
        }

        [Fact]
        public void SetsDeploymentId()
        {
            Target.Object.Initialize(Telemetry);

            Assert.Equal(Telemetry.Properties[PropertyKey], "TheDeploymentId");
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

        public class TestableTelemetry : ITelemetry, ISupportProperties
        {
            private readonly IDictionary<string, string>  _properties = new Dictionary<string, string>();

            public DateTimeOffset Timestamp { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public TelemetryContext Context => throw new NotImplementedException();

            public IExtension Extension { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public string Sequence { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public IDictionary<string, string> Properties => _properties;

            public ITelemetry DeepClone()
            {
                throw new NotImplementedException();
            }

            public void Sanitize()
            {
                throw new NotImplementedException();
            }

            public void SerializeData(ISerializationWriter serializationWriter)
            {
                throw new NotImplementedException();
            }
        }
    }
}

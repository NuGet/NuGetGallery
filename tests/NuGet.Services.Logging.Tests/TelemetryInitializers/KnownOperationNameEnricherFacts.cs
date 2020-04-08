// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Xunit;

namespace NuGet.Services.Logging.Tests
{
    public class KnownOperationNameEnricherFacts
    {
        public KnownOperationNameEnricherFacts()
        {
            KnownOperations = new List<string>();
            KnownOperations.Add("SomeOperation");
        }

        public List<string> KnownOperations { get; }
        public KnownOperationNameEnricher Target => new KnownOperationNameEnricher(KnownOperations);

        [Theory]
        [InlineData(typeof(DependencyTelemetry))]
        [InlineData(typeof(EventTelemetry))]
        [InlineData(typeof(ExceptionTelemetry))]
        [InlineData(typeof(MetricTelemetry))]
        [InlineData(typeof(TraceTelemetry))]
        public void IgnoresNonRequestTelemetry(Type type)
        {
            var telemetry = (ITelemetry)Activator.CreateInstance(type);
            telemetry.Context.Operation.Name = KnownOperations.First();

            Target.Initialize(telemetry);

            Assert.Empty(((ISupportProperties)telemetry).Properties);
        }

        [Fact]
        public void AddKnownOperationPropertyToRequestTelemetry()
        {
            var telemetry = new RequestTelemetry();
            telemetry.Context.Operation.Name = "SomeOperation";

            Target.Initialize(telemetry);

            var property = Assert.Single(telemetry.Properties);
            Assert.Equal("KnownOperation", property.Key);
            Assert.Equal("SomeOperation", property.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("IgnoreMe")]
        public void IgnoresTelemetryWithUnknownOperationName(string name)
        {
            var telemetry = new RequestTelemetry();
            telemetry.Context.Operation.Name = name;

            Target.Initialize(telemetry);

            Assert.Empty(telemetry.Properties);
        }
    }
}

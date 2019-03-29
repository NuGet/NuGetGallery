// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Services.Logging;
using Search.GenerateAuxiliaryData.Telemetry;
using Xunit;

namespace Tests.Search.GenerateAuxiliaryData.Telemetry
{
    public class TelemetryServiceFacts
    {
        public class TrackExporterDuration : BaseFacts
        {
            [Fact]
            public void EmitsExpectedMetric()
            {
                Target.TrackExporterDuration(
                    "TheExporter",
                    "TheReport",
                    TimeSpan.FromMilliseconds(1234),
                    success: true);

                TelemetryClient.Verify(
                    x => x.TrackMetric(
                        "Search.GenerateAuxiliaryData.ExporterDurationMs",
                        1234,
                        It.IsAny<IDictionary<string, string>>()),
                    Times.Once);

                var properties = Assert.Single(Properties);
                Assert.Equal(new[] { "Exporter", "Report", "Success" }, properties.Keys.OrderBy(x => x).ToArray());
                Assert.Equal("TheExporter", properties["Exporter"]);
                Assert.Equal("TheReport", properties["Report"]);
                Assert.Equal("True", properties["Success"]);
            }
        }

        public abstract class BaseFacts
        {
            public BaseFacts()
            {
                TelemetryClient = new Mock<ITelemetryClient>();
                Properties = new ConcurrentQueue<IDictionary<string, string>>();

                TelemetryClient
                    .Setup(x => x.TrackMetric(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<IDictionary<string, string>>()))
                    .Callback<string, double, IDictionary<string, string>>((_, __, p) => Properties.Enqueue(p));

                Target = new TelemetryService(TelemetryClient.Object);
            }

            public Mock<ITelemetryClient> TelemetryClient { get; }
            public ConcurrentQueue<IDictionary<string, string>> Properties { get; }
            public TelemetryService Target { get; }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Services.Logging;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class TelemetryServiceFacts
    {
        private readonly Mock<ITelemetryClient> _telemetryClient;
        private readonly TelemetryService _telemetryService;

        public TelemetryServiceFacts()
        {
            _telemetryClient = new Mock<ITelemetryClient>(MockBehavior.Strict);
            _telemetryService = new TelemetryService(_telemetryClient.Object);
        }

        [Fact]
        public void Constructor_WhenTelemetryClientIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TelemetryService(telemetryClient: null));

            Assert.Equal("telemetryClient", exception.ParamName);
        }

        [Fact]
        public void TrackDurationToHashPackage_TracksDuration()
        {
            const string PackageId = "a";
            const string NormalizedVersion = "b";
            const long PackageSize = 3;
            const string HashAlgorithm = "c";
            const string StreamType = "d";

            var expectedReturnValue = Mock.Of<IDisposable>();

            _telemetryClient.Setup(
                    x => x.TrackMetric(
                        It.IsNotNull<string>(),
                        It.IsAny<double>(),
                        It.IsNotNull<IDictionary<string, string>>()))
                .Callback((string metricName, double value, IDictionary<string, string> properties) =>
                {
                    Assert.Equal("Orchestrator.DurationToHashPackageSeconds", metricName);
                    Assert.True(value > 0);
                    Assert.NotEmpty(properties);
                    Assert.Equal(new Dictionary<string, string>()
                    {
                        { "PackageId", PackageId },
                        { "NormalizedVersion", NormalizedVersion },
                        { "PackageSize", PackageSize.ToString() },
                        { "HashAlgorithm", HashAlgorithm },
                        { "StreamType", StreamType }
                    }, properties);
                });

            using (_telemetryService.TrackDurationToHashPackage(
                PackageId,
                NormalizedVersion,
                PackageSize,
                HashAlgorithm,
                StreamType))
            {
            }

            _telemetryClient.VerifyAll();
        }
    }
}
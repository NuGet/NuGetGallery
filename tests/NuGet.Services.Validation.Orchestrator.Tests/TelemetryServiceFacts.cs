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
            Guid validationTrackingId = new Guid();
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
                        { "ValidationTrackingId", validationTrackingId.ToString() },
                        { "PackageSize", PackageSize.ToString() },
                        { "HashAlgorithm", HashAlgorithm },
                        { "StreamType", StreamType }
                    }, properties);
                });

            using (_telemetryService.TrackDurationToHashPackage(
                PackageId,
                NormalizedVersion,
                validationTrackingId,
                PackageSize,
                HashAlgorithm,
                StreamType))
            {
            }

            _telemetryClient.VerifyAll();
        }

        [Fact]
        public void TrackDurationToBackupPackage_TracksDuration()
        {
            // Arrange
            var validationTrackingId = Guid.NewGuid();
            var packageId = "a";
            var normalizedVersion = "b";
            var validationSet = new PackageValidationSet
            {
                ValidationTrackingId = validationTrackingId,
                PackageId = packageId,
                PackageNormalizedVersion = normalizedVersion
            };

            var expectedReturnValue = Mock.Of<IDisposable>();

            _telemetryClient.Setup(
                    x => x.TrackMetric(
                        It.IsNotNull<string>(),
                        It.IsAny<double>(),
                        It.IsNotNull<IDictionary<string, string>>()))
                .Callback((string metricName, double value, IDictionary<string, string> properties) =>
                {
                    Assert.Equal("Orchestrator.DurationToBackupPackageSeconds", metricName);
                    Assert.True(value > 0);
                    Assert.NotEmpty(properties);
                    Assert.Equal(new Dictionary<string, string>()
                    {
                        { "ValidationTrackingId", validationTrackingId.ToString() },
                        { "PackageId", packageId },
                        { "NormalizedVersion", normalizedVersion },
                    }, properties);
                });

            // Act
            using (_telemetryService.TrackDurationToBackupPackage(validationSet))
            {
            }

            // Assert
            _telemetryClient.VerifyAll();
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
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

        [Theory]
        [InlineData(ValidatingType.StagedPackage, false, true, "Orchestrator.StagedPackageValidation.TotalDurationSeconds")]
        [InlineData(ValidatingType.StagedSymbolPackage, false, false, "Orchestrator.StagedSymbolPackageValidation.TotalDurationSeconds")]
        [InlineData(ValidatingType.StagedSymbolPackage, true, true, "Orchestrator.StagedSymbolPromotion.TotalDurationSeconds")]
        public void StagingDurationsAreSeparatedByWorkflow(ValidatingType validatingType, bool isPromotion, bool isSuccess, string metricName)
        {
            var validationSet = CreateValidationSet(validatingType, isPromotion);
            var duration = TimeSpan.FromSeconds(12.5);
            ExpectMetric(metricName, duration.TotalSeconds, validationSet, isSuccess);

            _telemetryService.TrackStagingValidationDuration(validationSet, duration, isSuccess);

            VerifyMetric();
        }

        [Theory]
        [InlineData(ValidatingType.StagedPackage, false, "Orchestrator.StagedPackageValidation.TimedOut")]
        [InlineData(ValidatingType.StagedSymbolPackage, false, "Orchestrator.StagedSymbolPackageValidation.TimedOut")]
        [InlineData(ValidatingType.StagedSymbolPackage, true, "Orchestrator.StagedSymbolPromotion.TimedOut")]
        public void StagingTimeoutsAreSeparatedByWorkflow(ValidatingType validatingType, bool isPromotion, string metricName)
        {
            var validationSet = CreateValidationSet(validatingType, isPromotion);
            ExpectMetric(metricName, 1, validationSet);

            _telemetryService.TrackStagingValidationSetTimeout(validationSet);

            VerifyMetric();
        }

        [Theory]
        [InlineData(ValidatingType.StagedPackage, "Orchestrator.StagedPackageValidation.DurationToValidationSetCreationSeconds")]
        [InlineData(ValidatingType.StagedSymbolPackage, "Orchestrator.StagedSymbolPackageValidation.DurationToValidationSetCreationSeconds")]
        public void StagingCreationDelaysAreSeparatedByWorkflow(ValidatingType validatingType, string metricName)
        {
            var validationSet = CreateValidationSet(validatingType, isPromotion: false);
            var duration = TimeSpan.FromSeconds(12.5);
            ExpectMetric(metricName, duration.TotalSeconds, validationSet);

            _telemetryService.TrackStagingDurationToValidationSetCreation(validationSet, duration);

            VerifyMetric();
        }

        [Theory]
        [InlineData("creation", "Orchestrator.DurationToValidationSetCreationSeconds", 12.5)]
        [InlineData("duration", "Orchestrator.TotalValidationDurationSeconds", 12.5)]
        [InlineData("timeout", "Orchestrator.ValidationSetTimedOut", 1)]
        public void OrdinaryWorkflowMetricNamesAndPropertiesAreUnchanged(string operation, string metricName, double value)
        {
            var validationSet = CreateValidationSet(ValidatingType.Package, isPromotion: false);
            bool? isSuccess = null;
            if (operation == "duration")
            {
                isSuccess = true;
            }

            ExpectMetric(metricName, value, validationSet, isSuccess);

            switch (operation)
            {
                case "creation":
                    _telemetryService.TrackDurationToValidationSetCreation(
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        validationSet.ValidationTrackingId,
                        TimeSpan.FromSeconds(value));
                    break;
                case "duration":
                    _telemetryService.TrackTotalValidationDuration(
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        validationSet.ValidationTrackingId,
                        TimeSpan.FromSeconds(value),
                        true);
                    break;
                case "timeout":
                    _telemetryService.TrackValidationSetTimeout(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }

            VerifyMetric();
        }

        private static PackageValidationSet CreateValidationSet(ValidatingType validatingType, bool isPromotion)
        {
            var validator = ValidatorName.SymbolsValidator;
            if (isPromotion)
            {
                validator = ValidatorName.SymbolsIngester;
            }

            return new PackageValidationSet
            {
                PackageId = "PackageA",
                PackageNormalizedVersion = "1.0.0",
                ValidationTrackingId = Guid.NewGuid(),
                ValidatingType = validatingType,
                PackageValidations = new List<PackageValidation> { new PackageValidation { Type = validator } },
            };
        }

        private void ExpectMetric(string metricName, double value, PackageValidationSet validationSet, bool? isSuccess = null)
        {
            var expectedProperties = new Dictionary<string, string>
            {
                { "PackageId", validationSet.PackageId },
                { "NormalizedVersion", validationSet.PackageNormalizedVersion },
                { "ValidationTrackingId", validationSet.ValidationTrackingId.ToString() },
            };
            if (isSuccess.HasValue)
            {
                expectedProperties.Add("IsSuccess", isSuccess.Value.ToString());
            }

            _telemetryClient.Setup(client => client.TrackMetric(metricName, value, It.IsAny<IDictionary<string, string>>()))
                .Callback((string name, double metricValue, IDictionary<string, string> properties) => Assert.Equal(expectedProperties, properties));
        }

        private void VerifyMetric()
        {
            _telemetryClient.VerifyAll();
            _telemetryClient.Verify(client => client.TrackMetric(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
            _telemetryClient.VerifyNoOtherCalls();
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
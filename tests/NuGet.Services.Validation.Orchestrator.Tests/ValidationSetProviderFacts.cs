// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationSetProviderFacts
    {
        public Mock<IValidationStorageService> ValidationStorageMock { get; }
        public Mock<IOptionsSnapshot<ValidationConfiguration>> ConfigurationAccessorMock { get; }
        public Mock<ITelemetryService> TelemetryServiceMock { get; }
        public Mock<ILogger<ValidationSetProvider>> LoggerMock { get; }
        public ValidationConfiguration Configuration { get; }
        public Package Package { get; }
        public PackageValidationSet ValidationSet { get; }

        [Fact]
        public async Task TriesToGetValidationSetFirst()
        {
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(ValidationSet.ValidationTrackingId))
                .ReturnsAsync(ValidationSet)
                .Verifiable();

            var provider = CreateProvider();

            var set = await provider.TryGetOrCreateValidationSetAsync(ValidationSet.ValidationTrackingId, Package);

            ValidationStorageMock
                .Verify(vs => vs.GetValidationSetAsync(ValidationSet.ValidationTrackingId), Times.Once());

            Assert.Same(ValidationSet, set);
            
            TelemetryServiceMock.Verify(
                x => x.TrackDurationToValidationSetCreation(It.IsAny<TimeSpan>()),
                Times.Never);
        }

        [Fact]
        public async Task ThrowsIfPackageIdDoesNotMatchValidationSet()
        {
            ValidationSet.PackageId = string.Join("", ValidationSet.PackageId.Reverse());
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(ValidationSet.ValidationTrackingId))
                .ReturnsAsync(ValidationSet)
                .Verifiable();

            var provider = CreateProvider();

            var ex = await Assert.ThrowsAsync<Exception>(() => provider.TryGetOrCreateValidationSetAsync(ValidationSet.ValidationTrackingId, Package));
            Assert.Contains(ValidationSet.PackageId, ex.Message);
            Assert.Contains(Package.PackageRegistration.Id, ex.Message);
        }

        [Fact]
        public async Task ThrowsIfPackageVersionDoesNotMatchValidationSet()
        {
            ValidationSet.PackageNormalizedVersion = ValidationSet.PackageNormalizedVersion + ".42";
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(ValidationSet.ValidationTrackingId))
                .ReturnsAsync(ValidationSet)
                .Verifiable();

            var provider = CreateProvider();

            var ex = await Assert.ThrowsAsync<Exception>(() => provider.TryGetOrCreateValidationSetAsync(ValidationSet.ValidationTrackingId, Package));
            Assert.Contains(ValidationSet.PackageNormalizedVersion, ex.Message);
            Assert.Contains(Package.NormalizedVersion, ex.Message);
        }

        [Fact]
        public async Task ProperlyConstructsValidationSet()
        {
            const string validation1 = "validation1";
            const string validation2 = "validation2";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem(){ Name = validation1, FailAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ validation2 } },
                new ValidationConfigurationItem(){ Name = validation2, FailAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ } }
            };

            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<int>(), It.IsAny<TimeSpan>(), validationTrackingId))
                .ReturnsAsync(false);

            PackageValidationSet createdSet = null;
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<int>()))
                .ReturnsAsync(1);

            var provider = new ValidationSetProvider(
                ValidationStorageMock.Object,
                ConfigurationAccessorMock.Object,
                TelemetryServiceMock.Object,
                LoggerMock.Object);

            var returnedSet = await provider.TryGetOrCreateValidationSetAsync(validationTrackingId, Package);
            var endOfCallTimestamp = DateTime.UtcNow;

            ValidationStorageMock
                .Verify(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Once);

            Assert.NotNull(returnedSet);
            Assert.NotNull(createdSet);
            Assert.Same(createdSet, returnedSet);
            Assert.Equal(Package.PackageRegistration.Id, createdSet.PackageId);
            Assert.Equal(Package.NormalizedVersion, createdSet.PackageNormalizedVersion);
            Assert.Equal(Package.Key, createdSet.PackageKey);
            Assert.Equal(validationTrackingId, createdSet.ValidationTrackingId);
            Assert.True(createdSet.Created.Kind == DateTimeKind.Utc);
            Assert.True(createdSet.Updated.Kind == DateTimeKind.Utc);

            var allowedTimeDifference = TimeSpan.FromSeconds(5);
            Assert.True(endOfCallTimestamp - createdSet.Created < allowedTimeDifference);
            Assert.True(endOfCallTimestamp - createdSet.Updated < allowedTimeDifference);
            Assert.All(createdSet.PackageValidations, v => Assert.Same(createdSet, v.PackageValidationSet));
            Assert.All(createdSet.PackageValidations, v => Assert.Equal(ValidationStatus.NotStarted, v.ValidationStatus));
            Assert.All(createdSet.PackageValidations, v => Assert.True(endOfCallTimestamp - v.ValidationStatusTimestamp < allowedTimeDifference));
            Assert.Contains(createdSet.PackageValidations, v => v.Type == validation1);
            Assert.Contains(createdSet.PackageValidations, v => v.Type == validation2);

            TelemetryServiceMock.Verify(
                x => x.TrackDurationToValidationSetCreation(createdSet.Created - Package.Created),
                Times.Once);
        }

        [Fact]
        public async Task DoesNotEmitTelemetryIfMultipleValidationSetsExist()
        {
            const string validation1 = "validation1";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem(){ Name = validation1, FailAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ } }
            };

            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            PackageValidationSet createdSet = null;
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<int>()))
                .ReturnsAsync(2);

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<Guid>()))
                .ReturnsAsync(false);

            var provider = new ValidationSetProvider(
                ValidationStorageMock.Object,
                ConfigurationAccessorMock.Object,
                TelemetryServiceMock.Object,
                LoggerMock.Object);

            var returnedSet = await provider.TryGetOrCreateValidationSetAsync(validationTrackingId, Package);

            ValidationStorageMock
                .Verify(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Once);

            TelemetryServiceMock.Verify(
                x => x.TrackDurationToValidationSetCreation(It.IsAny<TimeSpan>()),
                Times.Never);
        }

        [Fact]
        public async Task GetOrCreateValidationSetAsyncDoesNotCreateDuplicateValidationSet()
        {
            Guid validationTrackingId = Guid.NewGuid();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null);

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(
                    Package.Key,
                    It.IsAny<TimeSpan>(),
                    validationTrackingId))
                .ReturnsAsync(true);

            var provider = CreateProvider();
            var result = await provider.TryGetOrCreateValidationSetAsync(validationTrackingId, Package);

            Assert.Null(result);
            ValidationStorageMock
                .Verify(
                    vs => vs.OtherRecentValidationSetForPackageExists(
                        Package.Key,
                        It.IsAny<TimeSpan>(),
                        validationTrackingId),
                    Times.Once);
            ValidationStorageMock
                .Verify(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
        }

        public ValidationSetProviderFacts()
        {
            ValidationStorageMock = new Mock<IValidationStorageService>(MockBehavior.Strict);
            ConfigurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            TelemetryServiceMock = new Mock<ITelemetryService>();
            LoggerMock = new Mock<ILogger<ValidationSetProvider>>();

            Configuration = new ValidationConfiguration();
            ConfigurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(() => Configuration);

            Package = new Package
            {
                PackageRegistration = new PackageRegistration { Id = "package1" },
                Version = "1.2.3.456",
                NormalizedVersion = "1.2.3",
                Key = 42,
                Created = new DateTime(2010, 1, 2, 8, 30, 0, DateTimeKind.Utc),
            };
            Package.PackageRegistration.Packages = new List<Package> { Package };

            ValidationSet = new PackageValidationSet
            {
                PackageId = Package.PackageRegistration.Id,
                PackageNormalizedVersion = Package.NormalizedVersion,
                ValidationTrackingId = Guid.NewGuid()
            };
        }

        private ValidationSetProvider CreateProvider()
        {
            return new ValidationSetProvider(
                ValidationStorageMock.Object,
                ConfigurationAccessorMock.Object,
                TelemetryServiceMock.Object,
                LoggerMock.Object);
        }
    }
}

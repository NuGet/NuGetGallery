// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationSetProviderFacts
    {
        public Mock<IValidationStorageService> ValidationStorageMock { get; }
        public Mock<IValidationFileService> PackageFileServiceMock { get; }
        public Mock<IValidatorProvider> ValidatorProvider { get; }
        public Mock<IOptionsSnapshot<ValidationConfiguration>> ConfigurationAccessorMock { get; }
        public Mock<ITelemetryService> TelemetryServiceMock { get; }
        public Mock<ILogger<ValidationSetProvider<Package>>> LoggerMock { get; }
        public ValidationConfiguration Configuration { get; }
        public string ETag { get; }
        public Package Package { get; }
        public PackageValidationSet ValidationSet { get; }
        public PackageValidationMessageData PackageValidationMessageData { get; }
        public PackageValidatingEntity PackageValidatingEntity { get; }

        [Fact]
        public async Task TriesToGetValidationSetFirst()
        {
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(ValidationSet.ValidationTrackingId))
                .ReturnsAsync(ValidationSet)
                .Verifiable();

            var provider = CreateProvider();

            var set = await provider.TryGetOrCreateValidationSetAsync(PackageValidationMessageData, PackageValidatingEntity);

            ValidationStorageMock
                .Verify(vs => vs.GetValidationSetAsync(ValidationSet.ValidationTrackingId), Times.Once());

            Assert.Same(ValidationSet, set);

            PackageFileServiceMock.Verify(
                x => x.CopyPackageFileForValidationSetAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
            PackageFileServiceMock.Verify(
                x => x.CopyValidationPackageForValidationSetAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
            PackageFileServiceMock.Verify(
                x => x.BackupPackageFileFromValidationSetPackageAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
            TelemetryServiceMock.Verify(
                x => x.TrackDurationToValidationSetCreation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<TimeSpan>()),
                Times.Never);
        }

        [Fact]
        public async Task CopiesToValidationSetContainerBeforeAddingDbRecord()
        {
            const string validation1 = "validation1";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem
                {
                    Name = validation1,
                    TrackAfter = TimeSpan.FromDays(1),
                    RequiredValidations = new List<string>(),
                    ShouldStart = true,
                }
            };

            Package.PackageStatusKey = PackageStatus.Available;

            var operations = new List<string>();

            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<IValidatingEntity<Package>>(), It.IsAny<TimeSpan>(), validationTrackingId))
                .ReturnsAsync(false);

            PackageFileServiceMock
                .Setup(x => x.CopyPackageFileForValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .ReturnsAsync(ETag)
                .Callback<PackageValidationSet>(_ => operations.Add(nameof(IValidationFileService.CopyPackageFileForValidationSetAsync)));
            PackageFileServiceMock
                .Setup(x => x.BackupPackageFileFromValidationSetPackageAsync(It.IsAny<PackageValidationSet>()))
                .Returns(Task.CompletedTask)
                .Callback(() => operations.Add(nameof(IValidationFileService.BackupPackageFileFromValidationSetPackageAsync)));
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(_ => operations.Add(nameof(IValidationStorageService.CreateValidationSetAsync)));

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<IValidatingEntity<Package>>()))
                .ReturnsAsync(1);

            var provider = CreateProvider();
            var packageValidationMessageData = new PackageValidationMessageData(
              Package.PackageRegistration.Id,
              Package.NormalizedVersion,
              validationTrackingId);
            await provider.TryGetOrCreateValidationSetAsync(packageValidationMessageData, new PackageValidatingEntity(Package));

            Assert.Equal(new[]
            {
                nameof(IValidationFileService.CopyPackageFileForValidationSetAsync),
                nameof(IValidationFileService.BackupPackageFileFromValidationSetPackageAsync),
                nameof(IValidationStorageService.CreateValidationSetAsync),
            }, operations);
        }

        [Fact]
        public async Task DoesNotBackUpThePackageWhenThereAreNoValidators()
        {
            const string validation1 = "validation1";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem(){ Name = validation1, TrackAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ } }
            };

            ValidatorProvider
                .Setup(x => x.IsProcessor(validation1))
                .Returns(false);

            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<IValidatingEntity<Package>>(), It.IsAny<TimeSpan>(), validationTrackingId))
                .ReturnsAsync(false);

            PackageValidationSet createdSet = null;
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<IValidatingEntity<Package>>()))
                .ReturnsAsync(1);

            var provider = CreateProvider();

            var packageValidationMessageData = new PackageValidationMessageData(
              Package.PackageRegistration.Id,
              Package.NormalizedVersion,
              validationTrackingId);
            var actual = await provider.TryGetOrCreateValidationSetAsync(packageValidationMessageData, PackageValidatingEntity);

            PackageFileServiceMock.Verify(
                x => x.BackupPackageFileFromValidationSetPackageAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
        }

        [Fact]
        public async Task CopiesPackageFromPackagesContainerWhenAvailable()
        {
            const string validation1 = "validation1";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem
                {
                    Name = validation1,
                    TrackAfter = TimeSpan.FromDays(1),
                    RequiredValidations = new List<string>(),
                    ShouldStart = true,
                }
            };

            Package.PackageStatusKey = PackageStatus.Available;
            
            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<IValidatingEntity<Package>>(), It.IsAny<TimeSpan>(), validationTrackingId))
                .ReturnsAsync(false);

            PackageValidationSet createdSet = null;
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<IValidatingEntity<Package>>()))
                .ReturnsAsync(1);

            var provider = CreateProvider();

            var packageValidationMessageData = new PackageValidationMessageData(
               Package.PackageRegistration.Id,
               Package.NormalizedVersion,
               validationTrackingId);
            var actual = await provider.TryGetOrCreateValidationSetAsync(packageValidationMessageData, PackageValidatingEntity);

            PackageFileServiceMock.Verify(x => x.CopyPackageFileForValidationSetAsync(createdSet), Times.Once);
            PackageFileServiceMock.Verify(x => x.CopyPackageFileForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Once);
            PackageFileServiceMock.Verify(x => x.CopyValidationPackageForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
            PackageFileServiceMock.Verify(x => x.BackupPackageFileFromValidationSetPackageAsync(createdSet), Times.Once);
            Assert.Equal(ETag, actual.PackageETag);
        }

        [Theory]
        [InlineData(PackageStatus.Validating)]
        [InlineData(PackageStatus.FailedValidation)]
        public async Task CopiesPackageFromValidationContainerWhenNotAvailable(PackageStatus packageStatus)
        {
            const string validation1 = "validation1";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem()
                {
                    Name = validation1,
                    TrackAfter = TimeSpan.FromDays(1),
                    RequiredValidations = new List<string>(),
                    ShouldStart = true,
                }
            };

            Package.PackageStatusKey = packageStatus;

            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<IValidatingEntity<Package>>(), It.IsAny<TimeSpan>(), validationTrackingId))
                .ReturnsAsync(false);

            PackageValidationSet createdSet = null;
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<IValidatingEntity<Package>>()))
                .ReturnsAsync(1);

            var provider = CreateProvider();

            var packageValidationMessageData = new PackageValidationMessageData(
                Package.PackageRegistration.Id,
                Package.NormalizedVersion,
                validationTrackingId);
            var actual = await provider.TryGetOrCreateValidationSetAsync(packageValidationMessageData, PackageValidatingEntity);

            PackageFileServiceMock.Verify(x => x.CopyPackageFileForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
            PackageFileServiceMock.Verify(x => x.CopyValidationPackageForValidationSetAsync(createdSet), Times.Once);
            PackageFileServiceMock.Verify(x => x.CopyValidationPackageForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Once);
            PackageFileServiceMock.Verify(x => x.BackupPackageFileFromValidationSetPackageAsync(createdSet), Times.Once);
            Assert.Null(actual.PackageETag);
        }

        [Fact]
        public async Task ThrowsIfPackageKeyDoesNotMatchValidationSet()
        {
            ValidationSet.PackageKey += 1111;
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(ValidationSet.ValidationTrackingId))
                .ReturnsAsync(ValidationSet)
                .Verifiable();

            var provider = CreateProvider();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.TryGetOrCreateValidationSetAsync(PackageValidationMessageData, PackageValidatingEntity));
            Assert.Contains(ValidationSet.PackageKey.ToString(), ex.Message);
            Assert.Contains(Package.Key.ToString(), ex.Message);
        }

        [Fact]
        public async Task ProperlyConstructsValidationSet()
        {
            const string validation1 = "validation1";
            const string validation2 = "validation2";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem()
                {
                    Name = validation1,
                    TrackAfter = TimeSpan.FromDays(1),
                    RequiredValidations = new List<string>{ validation2 },
                    ShouldStart = true,
                },
                new ValidationConfigurationItem()
                {
                    Name = validation2,
                    TrackAfter = TimeSpan.FromDays(1),
                    RequiredValidations = new List<string>(),
                    ShouldStart = true,
                }
            };

            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<IValidatingEntity<Package>>(), It.IsAny<TimeSpan>(), validationTrackingId))
                .ReturnsAsync(false);

            PackageValidationSet createdSet = null;
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<IValidatingEntity<Package>>()))
                .ReturnsAsync(1);

            var provider = new ValidationSetProvider<Package>(
                ValidationStorageMock.Object,
                PackageFileServiceMock.Object,
                ValidatorProvider.Object,
                ConfigurationAccessorMock.Object,
                TelemetryServiceMock.Object,
                LoggerMock.Object);

            var packageValidationMessageData = new PackageValidationMessageData(
                Package.PackageRegistration.Id,
                Package.NormalizedVersion,
                validationTrackingId);
            var returnedSet = await provider.TryGetOrCreateValidationSetAsync(packageValidationMessageData, PackageValidatingEntity);
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

            PackageFileServiceMock.Verify(
                x => x.CopyValidationPackageForValidationSetAsync(returnedSet),
                Times.Once);
            TelemetryServiceMock.Verify(
                x => x.TrackDurationToValidationSetCreation(createdSet.PackageId, createdSet.PackageNormalizedVersion, createdSet.ValidationTrackingId, createdSet.Created - Package.Created),
                Times.Once);
        }

        [Fact]
        public async Task DoesNotCreateValidationsWhenShouldStartFalse()
        {
            const string validation1 = "validation1";
            const string validation2 = "validation2";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem()
                {
                    Name = validation1,
                    TrackAfter = TimeSpan.FromDays(1),
                    RequiredValidations = new List<string>(),
                    ShouldStart = true,
                },
                new ValidationConfigurationItem()
                {
                    Name = validation2,
                    TrackAfter = TimeSpan.FromDays(1),
                    RequiredValidations = new List<string>(),
                    ShouldStart = false,
                }
            };

            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<IValidatingEntity<Package>>(), It.IsAny<TimeSpan>(), validationTrackingId))
                .ReturnsAsync(false);

            PackageValidationSet createdSet = null;
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<IValidatingEntity<Package>>()))
                .ReturnsAsync(1);

            var provider = new ValidationSetProvider<Package>(
                ValidationStorageMock.Object,
                PackageFileServiceMock.Object,
                ValidatorProvider.Object,
                ConfigurationAccessorMock.Object,
                TelemetryServiceMock.Object,
                LoggerMock.Object);

            var packageValidationMessageData = new PackageValidationMessageData(
                Package.PackageRegistration.Id,
                Package.NormalizedVersion,
                validationTrackingId);
            var returnedSet = await provider.TryGetOrCreateValidationSetAsync(packageValidationMessageData, PackageValidatingEntity);
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
            Assert.DoesNotContain(createdSet.PackageValidations, v => v.Type == validation2);

            PackageFileServiceMock.Verify(
                x => x.CopyValidationPackageForValidationSetAsync(returnedSet),
                Times.Once);
            TelemetryServiceMock.Verify(
                x => x.TrackDurationToValidationSetCreation(createdSet.PackageId, createdSet.PackageNormalizedVersion, createdSet.ValidationTrackingId, createdSet.Created - Package.Created),
                Times.Once);
        }

        [Fact]
        public async Task DoesNotEmitTelemetryIfMultipleValidationSetsExist()
        {
            const string validation1 = "validation1";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem(){ Name = validation1, TrackAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ } }
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
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<IValidatingEntity<Package>>()))
                .ReturnsAsync(2);

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<IValidatingEntity<Package>>(), It.IsAny<TimeSpan>(), It.IsAny<Guid>()))
                .ReturnsAsync(false);

            var provider = new ValidationSetProvider<Package>(
                ValidationStorageMock.Object,
                PackageFileServiceMock.Object,
                ValidatorProvider.Object,
                ConfigurationAccessorMock.Object,
                TelemetryServiceMock.Object,
                LoggerMock.Object);

            var packageValidationMessageData = new PackageValidationMessageData(
                Package.PackageRegistration.Id,
                Package.NormalizedVersion,
                validationTrackingId);

            var returnedSet = await provider.TryGetOrCreateValidationSetAsync(packageValidationMessageData, PackageValidatingEntity);

            ValidationStorageMock
                .Verify(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Once);

            TelemetryServiceMock.Verify(
                x => x.TrackDurationToValidationSetCreation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<TimeSpan>()),
                Times.Never);
        }

        [Fact]
        public async Task GetOrCreateValidationSetAsyncDoesNotCreateDuplicateValidationSet()
        {
            Guid validationTrackingId = Guid.NewGuid();
            var message = new PackageValidationMessageData(PackageValidationMessageData.PackageId, PackageValidationMessageData.PackageVersion, validationTrackingId);

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null);

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(
                    PackageValidatingEntity,
                    It.IsAny<TimeSpan>(),
                    validationTrackingId))
                .ReturnsAsync(true);

            var provider = CreateProvider();
            var result = await provider.TryGetOrCreateValidationSetAsync(message, PackageValidatingEntity);

            Assert.Null(result);
            ValidationStorageMock
                .Verify(
                    vs => vs.OtherRecentValidationSetForPackageExists(
                        PackageValidatingEntity,
                        It.IsAny<TimeSpan>(),
                        validationTrackingId),
                    Times.Once);
            ValidationStorageMock
                .Verify(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
            PackageFileServiceMock.Verify(
                x => x.CopyPackageFileForValidationSetAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
            PackageFileServiceMock.Verify(
                x => x.CopyValidationPackageForValidationSetAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
        }

        public ValidationSetProviderFacts()
        {
            ValidationStorageMock = new Mock<IValidationStorageService>(MockBehavior.Strict);
            PackageFileServiceMock = new Mock<IValidationFileService>(MockBehavior.Strict);
            ValidatorProvider = new Mock<IValidatorProvider>(MockBehavior.Strict);
            ConfigurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            TelemetryServiceMock = new Mock<ITelemetryService>();
            LoggerMock = new Mock<ILogger<ValidationSetProvider<Package>>>();

            PackageFileServiceMock
                .Setup(x => x.CopyPackageFileForValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .ReturnsAsync(() => ETag);

            PackageFileServiceMock
                .Setup(x => x.CopyValidationPackageForValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns(Task.CompletedTask);

            PackageFileServiceMock
                .Setup(x => x.BackupPackageFileFromValidationSetPackageAsync(It.IsAny<PackageValidationSet>()))
                .Returns(Task.CompletedTask);

            ValidatorProvider
                .Setup(x => x.IsProcessor(It.IsAny<string>()))
                .Returns(true);

            Configuration = new ValidationConfiguration();
            ConfigurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(() => Configuration);

            ETag = "\"some-etag\"";
            Package = new Package
            {
                PackageRegistration = new PackageRegistration { Id = "package1" },
                Version = "1.2.3.456",
                NormalizedVersion = "1.2.3",
                Key = 42,
                Created = new DateTime(2010, 1, 2, 8, 30, 0, DateTimeKind.Utc),
                PackageStatusKey = PackageStatus.Validating,
            };
            Package.PackageRegistration.Packages = new List<Package> { Package };

            ValidationSet = new PackageValidationSet
            {
                PackageId = Package.PackageRegistration.Id,
                PackageNormalizedVersion = Package.NormalizedVersion,
                PackageKey = Package.Key,
                ValidationTrackingId = Guid.NewGuid(),
            };

            PackageValidationMessageData = new PackageValidationMessageData(
                Package.PackageRegistration.Id,
                Package.NormalizedVersion,
                ValidationSet.ValidationTrackingId);

            PackageValidatingEntity = new PackageValidatingEntity(Package);
        }

        private ValidationSetProvider<Package> CreateProvider()
        {
            return new ValidationSetProvider<Package>(
                ValidationStorageMock.Object,
                PackageFileServiceMock.Object,
                ValidatorProvider.Object,
                ConfigurationAccessorMock.Object,
                TelemetryServiceMock.Object,
                LoggerMock.Object);
        }
    }
}

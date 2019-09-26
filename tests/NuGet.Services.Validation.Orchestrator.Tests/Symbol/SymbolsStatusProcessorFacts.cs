// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Extensions.Logging;

using Moq;
using NuGet.Services.Validation.Orchestrator.Telemetry;

using Xunit;


using NuGetGallery;
using NuGetGallery.Packaging;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator.Tests.Symbol
{
    public class SymbolsStatusProcessorFacts
    {
        public class TheProceedToMakePackageAvailable : BaseFacts
        {
            [Fact]
            public void ItShouldNotProceedWhenFromAvailableState()
            {
                // Arrange
                IValidatingEntity<SymbolPackage> validatingSymbolPackage = null;
                SymbolsPackageServiceMock.Setup(sp => sp.FindPackageByIdAndVersionStrict(PackageId, PackageVersion))
                .Returns(validatingSymbolPackage);

                var validationSet = new PackageValidationSet
                {
                    PackageId = AvailableSymbolPackage.Id,
                    PackageNormalizedVersion = AvailableSymbolPackage.Version,
                    PackageKey = AvailableSymbolPackage.Key,
                    PackageValidations = new List<PackageValidation>
                    {
                        new PackageValidation { Type = "SomeValidator" },
                    }
                };

                // Act
                bool result = Target.CanProceedToMakePackageAvailable(AvailableSymbolPackageValidatingEntity, validationSet);
                Target.SetStatusAsync(AvailableSymbolPackageValidatingEntity, validationSet, PackageStatus.Available);

                // Assert
                Assert.False(result);
                SymbolPackageFileServiceMock.Verify(spfs => spfs.DoesValidationSetPackageExistAsync(It.IsAny<PackageValidationSet>()), Times.Never);
                SymbolPackageFileServiceMock.Verify(spfs => spfs.CopyValidationSetPackageToPackageFileAsync(It.IsAny<PackageValidationSet>(), It.IsAny<IAccessCondition>()), Times.Never);
                SymbolPackageFileServiceMock.Verify(spfs => spfs.CopyValidationPackageToPackageFileAsync(It.IsAny<PackageValidationSet>()), Times.Never);
                SymbolPackageFileServiceMock.Verify(spfs => spfs.UpdatePackageBlobMetadataInValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
                SymbolPackageFileServiceMock.Verify(spfs => spfs.UpdatePackageBlobMetadataInValidationAsync(It.IsAny<PackageValidationSet>()), Times.Never);
                SymbolsPackageServiceMock.Verify(sps => sps.UpdateStatusAsync(It.IsAny < SymbolPackage>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()), Times.Never);
            }

            [Fact]
            public void ItShouldNotProceedWhenFromFailedStateWithValidationInProgress()
            {
                // Arrange
                SymbolsPackageServiceMock.Setup(sp => sp.FindPackageByIdAndVersionStrict(PackageId, PackageVersion))
                .Returns(ValidatingSymbolPackageValidatingEntity);

                var validationSet = new PackageValidationSet
                {
                    PackageId = FailedSymbolPackage.Id,
                    PackageNormalizedVersion = FailedSymbolPackage.Version,
                    PackageKey = FailedSymbolPackageValidatingEntity.Key,
                    PackageValidations = new List<PackageValidation>
                    {
                        new PackageValidation { Type = "SomeValidator" },
                    }
                };

                // Act
                bool result = Target.CanProceedToMakePackageAvailable(FailedSymbolPackageValidatingEntity, validationSet);
                Target.SetStatusAsync(FailedSymbolPackageValidatingEntity, validationSet, PackageStatus.Available);

                // Assert
                Assert.False(result);
                SymbolPackageFileServiceMock.Verify(spfs => spfs.DoesValidationSetPackageExistAsync(It.IsAny<PackageValidationSet>()), Times.Never);
                SymbolPackageFileServiceMock.Verify(spfs => spfs.CopyValidationSetPackageToPackageFileAsync(It.IsAny<PackageValidationSet>(), It.IsAny<IAccessCondition>()), Times.Never);
                SymbolPackageFileServiceMock.Verify(spfs => spfs.CopyValidationPackageToPackageFileAsync(It.IsAny<PackageValidationSet>()), Times.Never);
                SymbolPackageFileServiceMock.Verify(spfs => spfs.UpdatePackageBlobMetadataInValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
                SymbolPackageFileServiceMock.Verify(spfs => spfs.UpdatePackageBlobMetadataInValidationAsync(It.IsAny<PackageValidationSet>()), Times.Never);
                SymbolsPackageServiceMock.Verify(sps => sps.UpdateStatusAsync(It.IsAny<SymbolPackage>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()), Times.Never);
            }

            [Fact]
            public void ItShouldProceedWhenFromFailedStateWithNoValidationInProgress()
            {
                // Arrange
                IValidatingEntity<SymbolPackage> validatingSymbolPackage = null;
                SymbolsPackageServiceMock.Setup(sp => sp.FindPackageByIdAndVersionStrict(PackageId, PackageVersion))
                .Returns(validatingSymbolPackage);

                var validationSet = new PackageValidationSet
                {
                    PackageId = FailedSymbolPackage.Id,
                    PackageNormalizedVersion = FailedSymbolPackage.Version,
                    PackageKey = FailedSymbolPackageValidatingEntity.Key,
                    PackageValidations = new List<PackageValidation>
                    {
                        new PackageValidation { Type = "SomeValidator" },
                    }
                };

                // Act
                bool result = Target.CanProceedToMakePackageAvailable(FailedSymbolPackageValidatingEntity, validationSet);
                Target.SetStatusAsync(FailedSymbolPackageValidatingEntity, validationSet, PackageStatus.Available);

                // Assert
                Assert.True(result);
                SymbolPackageFileServiceMock.Verify(spfs => spfs.DoesValidationSetPackageExistAsync(It.IsAny<PackageValidationSet>()), Times.Once);
            }

            [Fact]
            public void ItShouldProceedWhenFromValidatingState()
            {
                // Arrange
                SymbolsPackageServiceMock.Setup(sp => sp.FindPackageByIdAndVersionStrict(PackageId, PackageVersion))
                .Returns(ValidatingSymbolPackageValidatingEntity);

                var validationSet = new PackageValidationSet
                {
                    PackageId = ValidatingSymbolPackage.Id,
                    PackageNormalizedVersion = ValidatingSymbolPackage.Version,
                    PackageKey = ValidatingSymbolPackage.Key,
                    PackageValidations = new List<PackageValidation>
                    {
                        new PackageValidation { Type = "SomeValidator" },
                    }
                };

                // Act
                bool result = Target.CanProceedToMakePackageAvailable(ValidatingSymbolPackageValidatingEntity, validationSet);
                Target.SetStatusAsync(ValidatingSymbolPackageValidatingEntity, validationSet, PackageStatus.Available);

                // Assert
                Assert.True(result);
                SymbolPackageFileServiceMock.Verify(spfs => spfs.DoesValidationSetPackageExistAsync(It.IsAny<PackageValidationSet>()), Times.Once);
            }
        }

        public class BaseFacts
        {
            public const string PackageId = "SomeId";
            public const string PackageVersion = "1.1.1";
            public const int PackageKey = 100;

            public BaseFacts()
            {

                Package = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = PackageId
                    },
                    PackageStatusKey = PackageStatus.Available,
                    Version = PackageVersion,
                    NormalizedVersion = PackageVersion,
                    Key = PackageKey
                };

                AvailableSymbolPackage = new SymbolPackage
                {
                    Key = 1,
                    Package = Package,
                    PackageKey = PackageKey,
                    StatusKey = PackageStatus.Available
                };

                FailedSymbolPackage = new SymbolPackage
                {
                    Key = 2,
                    Package = Package,
                    PackageKey = PackageKey,
                    StatusKey = PackageStatus.FailedValidation
                };

                ValidatingSymbolPackage = new SymbolPackage
                {
                    Key = 3,
                    Package = Package,
                    PackageKey = 100,
                    StatusKey = PackageStatus.Validating
                };

                SymbolsPackageServiceMock = new Mock<IEntityService<SymbolPackage>>();
                SymbolPackageFileServiceMock = new Mock<IValidationFileService>();
                ValidatorProviderMock = new Mock<IValidatorProvider>();
                TelemetryServiceMock = new Mock<ITelemetryService>();
                LoggerMock = new Mock<ILogger<EntityStatusProcessor<SymbolPackage>>>();

                var streamMetadata = new PackageStreamMetadata()
                {
                    Size = 1,
                    Hash = "hash",
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId
                };

                Target = new SymbolsStatusProcessor(
                    SymbolsPackageServiceMock.Object,
                    SymbolPackageFileServiceMock.Object,
                    ValidatorProviderMock.Object,
                    TelemetryServiceMock.Object,
                    LoggerMock.Object);

                AvailableSymbolPackageValidatingEntity = new SymbolPackageValidatingEntity(AvailableSymbolPackage);
                FailedSymbolPackageValidatingEntity = new SymbolPackageValidatingEntity(FailedSymbolPackage);
                ValidatingSymbolPackageValidatingEntity = new SymbolPackageValidatingEntity(ValidatingSymbolPackage);
            }

            public Package Package { get; }
            public SymbolPackage AvailableSymbolPackage { get; }
            public SymbolPackage FailedSymbolPackage { get; }
            public SymbolPackage ValidatingSymbolPackage { get; }
            public Mock<IEntityService<SymbolPackage>> SymbolsPackageServiceMock { get; }
            public Mock<IValidationFileService> SymbolPackageFileServiceMock { get; }
            public Mock<IValidatorProvider> ValidatorProviderMock { get; }
            public Mock<ITelemetryService> TelemetryServiceMock { get; }
            public Mock<ILogger<EntityStatusProcessor<SymbolPackage>>> LoggerMock { get; }
            public SymbolsStatusProcessor Target { get; }
            public SymbolPackageValidatingEntity AvailableSymbolPackageValidatingEntity { get; }
            public SymbolPackageValidatingEntity FailedSymbolPackageValidatingEntity { get; }
            public SymbolPackageValidatingEntity ValidatingSymbolPackageValidatingEntity { get; }

        }

    }
}

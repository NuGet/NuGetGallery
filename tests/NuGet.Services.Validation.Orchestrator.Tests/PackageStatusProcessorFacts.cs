// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class PackageStatusProcessorFacts
    {
        public class Constructor : BaseFacts
        {
            [Fact]
            public void NullCoreLicenseFileServiceThrowsArgumentNullException()
            {
                var ex = Assert.Throws<ArgumentNullException>(
                    () => new PackageStatusProcessor(
                        PackageServiceMock.Object,
                        PackageFileServiceMock.Object,
                        ValidatorProviderMock.Object,
                        TelemetryServiceMock.Object,
                        SasDefinitionConfigurationMock.Object,
                        LoggerMock.Object,
                        null,
                        CoreReadmeFileServiceMock.Object));

                Assert.Equal("coreLicenseFileService", ex.ParamName);
            }

            [Fact]
            public void DoesNotThrowWhenSasDefinitionConfigurationAccesssorIsNull()
            {
                var processor = new PackageStatusProcessor(
                    PackageServiceMock.Object,
                    PackageFileServiceMock.Object,
                    ValidatorProviderMock.Object,
                    TelemetryServiceMock.Object,
                    null,
                    LoggerMock.Object,
                    CoreLicenseFileServiceMock.Object,
                    CoreReadmeFileServiceMock.Object);
            }

            [Fact]
            public void DoesNotThrowWhenSasDefinitionConfigurationAccesssorValueIsNull()
            {
                var sasDefinitionConfigurationMock = new Mock<IOptionsSnapshot<SasDefinitionConfiguration>>();
                sasDefinitionConfigurationMock.Setup(x => x.Value).Returns(() => (SasDefinitionConfiguration)null);

                var processor = new PackageStatusProcessor(
                    PackageServiceMock.Object,
                    PackageFileServiceMock.Object,
                    ValidatorProviderMock.Object,
                    TelemetryServiceMock.Object,
                    SasDefinitionConfigurationMock.Object,
                    LoggerMock.Object,
                    CoreLicenseFileServiceMock.Object,
                    CoreReadmeFileServiceMock.Object);
            }
        }

        public class SetPackageStatusAsync : BaseFacts
        {
            [Theory]
            [InlineData(PackageStatus.Deleted)]
            [InlineData(PackageStatus.Validating)]
            public async Task RejectsUnsupportedPackageStatus(PackageStatus packageStatus)
            {
                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, packageStatus));

                Assert.Equal("status", ex.ParamName);
                Assert.Contains("A package can only transition to the Available or FailedValidation states.", ex.Message);
            }

            [Theory]
            [InlineData(PackageStatus.Available)]
            [InlineData(PackageStatus.FailedValidation)]
            public async Task RejectsDeletedPackage(PackageStatus packageStatus)
            {
                Package.PackageStatusKey = PackageStatus.Deleted;

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, packageStatus));

                Assert.Equal("validatingEntity", ex.ParamName);
                Assert.Contains("A package in the Deleted state cannot be processed.", ex.Message);
            }

            [Fact]
            public async Task RejectsAvailableToFailedValidation()
            {
                Package.PackageStatusKey = PackageStatus.Available;

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.FailedValidation));

                Assert.Equal("status", ex.ParamName);
                Assert.Contains("A package cannot transition from Available to FailedValidation.", ex.Message);
            }

            [Theory]
            [InlineData(PackageStatus.Available, PackageStatus.Available)]
            [InlineData(PackageStatus.Validating, PackageStatus.Available)]
            [InlineData(PackageStatus.Validating, PackageStatus.FailedValidation)]
            [InlineData(PackageStatus.FailedValidation, PackageStatus.Available)]
            [InlineData(PackageStatus.FailedValidation, PackageStatus.FailedValidation)]
            public async Task EmitsTelemetryOnStatusChange(PackageStatus fromStatus, PackageStatus toStatus)
            {
                Package.PackageStatusKey = fromStatus;

                await Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, toStatus);

                if (fromStatus != toStatus)
                {
                    TelemetryServiceMock.Verify(
                        x => x.TrackPackageStatusChange(ValidationSet.PackageId, ValidationSet.PackageNormalizedVersion, ValidationSet.ValidationTrackingId, fromStatus, toStatus),
                        Times.Once);
                    TelemetryServiceMock.Verify(
                        x => x.TrackPackageStatusChange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<PackageStatus>(), It.IsAny<PackageStatus>()),
                        Times.Once);
                }
                else
                {
                    TelemetryServiceMock.Verify(
                        x => x.TrackPackageStatusChange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<PackageStatus>(), It.IsAny<PackageStatus>()),
                        Times.Never);
                }
            }
        }

        public class MakePackageAvailableAsync : BaseFacts
        {
            [Fact]
            public async Task DoesNotSetPackageStreamMetadataIfNotChanged()
            {
                var content = "Hello, world.";
                Package.PackageFileSize = content.Length;
                Package.HashAlgorithm = "SHA512";
                Package.Hash = "rQw3wx1psxXzqB8TyM3nAQlK2RcluhsNwxmcqXE2YbgoDW735o8TPmIR4uWpoxUERddvFwjgRSGw7gNPCwuvJg==";
                var stream = new MemoryStream(Encoding.ASCII.GetBytes(content));

                var corePackageService = new Mock<ICorePackageService>();
                var mockPackageEntityRepository = new Mock<IEntityRepository<Package>>();
                var entityPackageService = new PackageEntityService(corePackageService.Object, mockPackageEntityRepository.Object);

                PackageFileServiceMock
                    .Setup(x => x.DownloadPackageFileToDiskAsync(ValidationSet, SasDefinitionConfiguration.PackageStatusProcessorSasDefinition))
                    .ReturnsAsync(stream);

                var streamMetadata = new PackageStreamMetadata()
                {
                    Size = Package.PackageFileSize,
                    Hash = Package.Hash,
                    HashAlgorithm = Package.HashAlgorithm
                };

                PackageFileServiceMock
                    .Setup(x => x.UpdatePackageBlobMetadataInValidationSetAsync(It.IsAny<PackageValidationSet>()))
                    .ReturnsAsync(streamMetadata);

                await Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available);

                corePackageService.Verify(
                    x => x.UpdatePackageStreamMetadataAsync(It.IsAny<Package>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<bool>()),
                    Times.Never);
            }

            [Fact]
            public async Task SetsPackageStreamMetadataIfChanged()
            {
                var content = "Hello, world.";
                var expectedHash = "rQw3wx1psxXzqB8TyM3nAQlK2RcluhsNwxmcqXE2YbgoDW735o8TPmIR4uWpoxUERddvFwjgRSGw7gNPCwuvJg==";
                var stream = new MemoryStream(Encoding.ASCII.GetBytes(content));
                PackageStreamMetadata actual = null;
                PackageFileServiceMock
                    .Setup(x => x.DownloadPackageFileToDiskAsync(ValidationSet, SasDefinitionConfiguration.PackageStatusProcessorSasDefinition))
                    .ReturnsAsync(stream);
                var streamMetadata = new PackageStreamMetadata()
                {
                    Size = stream.Length,
                    Hash = expectedHash,
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId
                };
                PackageFileServiceMock
                    .Setup(x => x.UpdatePackageBlobMetadataInValidationAsync(It.IsAny<PackageValidationSet>()))
                    .ReturnsAsync(streamMetadata);
                PackageServiceMock
                    .Setup(x => x.UpdateMetadataAsync(Package, It.IsAny<PackageStreamMetadata>(), false))
                    .Returns(Task.CompletedTask)
                    .Callback<Package, object, bool>((_, m, __) => actual = m as PackageStreamMetadata);

                await Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available);

                Assert.NotNull(actual);
                Assert.Equal(content.Length, actual.Size);
                Assert.Equal(expectedHash, actual.Hash);
                Assert.Equal("SHA512", actual.HashAlgorithm);
                PackageServiceMock.Verify(
                    x => x.UpdateMetadataAsync(Package, actual, false),
                    Times.Once);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.Absent, false)]
            [InlineData(EmbeddedLicenseFileType.PlainText, true)]
            [InlineData(EmbeddedLicenseFileType.Markdown, true)]
            public async Task SavesPackageLicenseFileWhenPresent(EmbeddedLicenseFileType licenseFileType, bool expectedSave)
            {
                var content = "Hello, world.";
                var stream = new MemoryStream(Encoding.ASCII.GetBytes(content));
                Package.EmbeddedLicenseType = licenseFileType;
                PackageFileServiceMock
                    .Setup(x => x.DownloadPackageFileToDiskAsync(ValidationSet, SasDefinitionConfiguration.PackageStatusProcessorSasDefinition))
                    .ReturnsAsync(stream);

                await Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available);

                if (expectedSave)
                {
                    CoreLicenseFileServiceMock
                        .Verify(clfs => clfs.ExtractAndSaveLicenseFileAsync(PackageValidatingEntity.EntityRecord, stream), Times.Once);
                    CoreLicenseFileServiceMock
                        .Verify(clfs => clfs.ExtractAndSaveLicenseFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()), Times.Once);
                }
                else
                {
                    CoreLicenseFileServiceMock
                        .Verify(clfs => clfs.ExtractAndSaveLicenseFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()), Times.Never);
                }
            }

            [Theory]
            [InlineData(EmbeddedReadmeFileType.Absent, false)]
            [InlineData(EmbeddedReadmeFileType.Markdown, true)]
            public async Task SavesPackageReadmeFileWhenPresent(EmbeddedReadmeFileType readmeFileType, bool expectedSave)
            {
                var content = "Hello, world.";
                var stream = new MemoryStream(Encoding.ASCII.GetBytes(content));
                Package.EmbeddedReadmeType = readmeFileType;
                Package.HasReadMe = true;
                PackageFileServiceMock
                    .Setup(x => x.DownloadPackageFileToDiskAsync(ValidationSet, SasDefinitionConfiguration.PackageStatusProcessorSasDefinition))
                    .ReturnsAsync(stream);

                await Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available);

                if (expectedSave)
                {
                    CoreReadmeFileServiceMock
                        .Verify(clfs => clfs.ExtractAndSaveReadmeFileAsync(PackageValidatingEntity.EntityRecord, stream), Times.Once);
                    CoreReadmeFileServiceMock
                        .Verify(clfs => clfs.ExtractAndSaveReadmeFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()), Times.Once);
                }
                else
                {
                    CoreReadmeFileServiceMock
                        .Verify(clfs => clfs.ExtractAndSaveReadmeFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()), Times.Never);
                }
            }

            [Fact]
            public async Task AllowsPackageAlreadyInPublicContainerWhenValidationSetPackageDoesNotExist()
            {
                PackageFileServiceMock
                    .Setup(x => x.DoesValidationSetPackageExistAsync(It.IsAny<PackageValidationSet>()))
                    .ReturnsAsync(false);
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationPackageToPackageFileAsync(It.IsAny<PackageValidationSet>()))
                    .Throws(new InvalidOperationException("Duplicate!"));

                await Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available);

                PackageFileServiceMock.Verify(
                    x => x.UpdatePackageBlobMetadataInValidationAsync(It.IsAny<PackageValidationSet>()),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.CopyValidationPackageToPackageFileAsync(ValidationSet),
                    Times.Once);
                PackageServiceMock.Verify(
                    x => x.UpdateStatusAsync(Package, PackageStatus.Available, true),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.DeleteValidationPackageFileAsync(ValidationSet),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.DeletePackageForValidationSetAsync(ValidationSet),
                    Times.Never);
            }

            [Theory]
            [InlineData(PackageStatus.Validating)]
            [InlineData(PackageStatus.FailedValidation)]
            public async Task ThrowsExceptionWhenValidationSetPackageAndDestinationPackageBothExist(PackageStatus packageStatus)
            {
                ValidationSet.PackageETag = null;
                Package.PackageStatusKey = packageStatus;
                var expected = new InvalidOperationException("Duplicate!");

                PackageFileServiceMock
                    .Setup(x => x.DoesValidationSetPackageExistAsync(It.IsAny<PackageValidationSet>()))
                    .ReturnsAsync(true);
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationSetPackageToPackageFileAsync(It.IsAny<PackageValidationSet>(), It.IsAny<IAccessCondition>()))
                    .Throws(expected);

                var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available));

                Assert.Same(expected, actual);

                PackageFileServiceMock.Verify(
                    x => x.UpdatePackageBlobMetadataInValidationSetAsync(It.IsAny<PackageValidationSet>()),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.CopyValidationSetPackageToPackageFileAsync(ValidationSet, It.Is<IAccessCondition>(y => y.IfNoneMatchETag == "*")),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.UpdatePackageBlobMetadataInValidationAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
                PackageServiceMock.Verify(
                    x => x.UpdateStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()),
                    Times.Never);
                PackageFileServiceMock.Verify(
                    x => x.DeleteValidationPackageFileAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
                PackageFileServiceMock.Verify(
                    x => x.DeletePackageForValidationSetAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
            }

            [Fact]
            public async Task ThrowsExceptionWhenValidationSetPackageAndDestinationPackageDoesNotMatchETag()
            {
                ValidationSet.PackageETag = "\"some-etag\"";
                Package.PackageStatusKey = PackageStatus.Available;
                var expected = new CloudBlobPreconditionFailedException(null);

                PackageFileServiceMock
                    .Setup(x => x.DoesValidationSetPackageExistAsync(It.IsAny<PackageValidationSet>()))
                    .ReturnsAsync(true);
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationSetPackageToPackageFileAsync(It.IsAny<PackageValidationSet>(), It.IsAny<IAccessCondition>()))
                    .Throws(expected);

                var actual = await Assert.ThrowsAsync<CloudBlobPreconditionFailedException>(
                    () => Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available));

                Assert.Same(expected, actual);

                PackageFileServiceMock.Verify(
                    x => x.UpdatePackageBlobMetadataInValidationSetAsync(It.IsAny<PackageValidationSet>()),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.CopyValidationSetPackageToPackageFileAsync(ValidationSet, It.Is<IAccessCondition>(y => y.IfMatchETag == "\"some-etag\"")),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.UpdatePackageBlobMetadataInValidationAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
                PackageServiceMock.Verify(
                    x => x.UpdateStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()),
                    Times.Never);
                PackageFileServiceMock.Verify(
                    x => x.DeleteValidationPackageFileAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
                PackageFileServiceMock.Verify(
                    x => x.DeletePackageForValidationSetAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
            }

            [Theory]
            [InlineData(PackageStatus.Available, false)]
            [InlineData(PackageStatus.Validating, true)]
            [InlineData(PackageStatus.FailedValidation, true)]
            public async Task DeletesValidationPackageOnSuccess(PackageStatus fromStatus, bool delete)
            {
                Package.PackageStatusKey = fromStatus;

                await Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available);

                PackageFileServiceMock.Verify(
                    x => x.DeleteValidationPackageFileAsync(ValidationSet),
                    delete ? Times.Once() : Times.Never());
                PackageFileServiceMock.Verify(
                    x => x.DeleteValidationPackageFileAsync(It.IsAny<PackageValidationSet>()),
                    delete ? Times.Once() : Times.Never());
                PackageFileServiceMock.Verify(
                    x => x.DeletePackageForValidationSetAsync(ValidationSet),
                    Times.Never);
            }

            [Fact]
            public async Task DeletesPackageFromPublicStorageOnDbUpdateFailureIfCopiedAndOriginallyValidating()
            {
                Package.PackageStatusKey = PackageStatus.Validating;

                var expected = new Exception("Everything failed");
                PackageServiceMock
                    .Setup(ps => ps.UpdateStatusAsync(Package, PackageStatus.Available, true))
                    .Throws(expected);
                
                var actual = await Assert.ThrowsAsync<Exception>(
                    () => Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available));

                Assert.Same(expected, actual);
                
                PackageFileServiceMock.Verify(
                    x => x.DeletePackageFileAsync(ValidationSet),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.DeletePackageForValidationSetAsync(ValidationSet),
                    Times.Never);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.Absent, PackageStatus.Validating, false)]
            [InlineData(EmbeddedLicenseFileType.Absent, PackageStatus.Available, false)]
            [InlineData(EmbeddedLicenseFileType.PlainText, PackageStatus.Validating, true)]
            [InlineData(EmbeddedLicenseFileType.PlainText, PackageStatus.Available, false)]
            [InlineData(EmbeddedLicenseFileType.Markdown, PackageStatus.Validating, true)]
            [InlineData(EmbeddedLicenseFileType.Markdown, PackageStatus.Available, false)]
            public async Task DeletesLicenseFromPublicStorageOnDbUpdateFailure(EmbeddedLicenseFileType licenseFileType, PackageStatus originalStatus, bool expectedDelete)
            {
                Package.PackageStatusKey = originalStatus;
                Package.EmbeddedLicenseType = licenseFileType;

                var expected = new Exception("Everything failed");
                PackageServiceMock
                    .Setup(ps => ps.UpdateStatusAsync(Package, PackageStatus.Available, true))
                    .Throws(expected);

                var actual = await Assert.ThrowsAsync<Exception>(
                    () => Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available));

                Assert.Same(expected, actual);

                if (expectedDelete)
                {
                    CoreLicenseFileServiceMock
                        .Verify(clfs => clfs.DeleteLicenseFileAsync(Package.Id, Package.NormalizedVersion), Times.Once);
                    CoreLicenseFileServiceMock
                        .Verify(clfs => clfs.DeleteLicenseFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
                }
                else
                {
                    CoreLicenseFileServiceMock
                        .Verify(clfs => clfs.DeleteLicenseFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                }
            }

            [Theory]
            [InlineData(EmbeddedReadmeFileType.Absent, PackageStatus.Validating, false)]
            [InlineData(EmbeddedReadmeFileType.Absent, PackageStatus.Available, false)]
            [InlineData(EmbeddedReadmeFileType.Markdown, PackageStatus.Validating, true)]
            [InlineData(EmbeddedReadmeFileType.Markdown, PackageStatus.Available, false)]
            public async Task DeletesReadmeFromPublicStorageOnDbUpdateFailure(EmbeddedReadmeFileType readmeFileType, PackageStatus originalStatus, bool expectedDelete)
            {
                Package.PackageStatusKey = originalStatus;
                Package.EmbeddedReadmeType = readmeFileType;
                Package.HasReadMe = true;

                var expected = new Exception("Everything failed");
                PackageServiceMock
                    .Setup(ps => ps.UpdateStatusAsync(Package, PackageStatus.Available, true))
                    .Throws(expected);

                var actual = await Assert.ThrowsAsync<Exception>(
                    () => Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available));

                Assert.Same(expected, actual);

                if (expectedDelete)
                {
                    CoreReadmeFileServiceMock
                        .Verify(clfs => clfs.DeleteReadmeFileAsync(Package.Id, Package.NormalizedVersion), Times.Once);
                    CoreReadmeFileServiceMock
                        .Verify(clfs => clfs.DeleteReadmeFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
                }
                else
                {
                    CoreReadmeFileServiceMock
                        .Verify(clfs => clfs.DeleteReadmeFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                }
            }

            [Fact]
            public async Task DoesNotDeletePackageFromPublicStorageOnDbUpdateFailureIfCopiedAndOriginallyAvailable()
            {
                Package.PackageStatusKey = PackageStatus.Available;

                var expected = new Exception("Everything failed");
                PackageServiceMock
                    .Setup(ps => ps.UpdateStatusAsync(Package, PackageStatus.Available, true))
                    .Throws(expected);

                var actual = await Assert.ThrowsAsync<Exception>(
                    () => Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available));

                Assert.Same(expected, actual);

                PackageFileServiceMock.Verify(
                    x => x.DeletePackageFileAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
                PackageFileServiceMock.Verify(
                    x => x.DeletePackageForValidationSetAsync(ValidationSet),
                    Times.Never);
            }

            [Fact]
            public async Task DoesNotDeletePackageFromPublicStorageOnDbUpdateFailureIfNotCopied()
            {
                var expected = new Exception("Everything failed");
                PackageServiceMock
                    .Setup(ps => ps.UpdateStatusAsync(Package, PackageStatus.Available, true))
                    .Throws(expected);
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationPackageToPackageFileAsync(It.IsAny<PackageValidationSet>()))
                    .Throws(new InvalidOperationException("Duplicate!"));

                var actual = await Assert.ThrowsAsync<Exception>(
                    () => Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available));

                Assert.Same(expected, actual);

                PackageFileServiceMock.Verify(
                    x => x.DeletePackageFileAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
                PackageFileServiceMock.Verify(
                    x => x.DeletePackageForValidationSetAsync(ValidationSet),
                    Times.Never);
            }

            [Fact]
            public async Task CopyDbUpdateDeleteInCorrectOrderWhenValidationSetPackageExists()
            {
                var operations = new List<string>();

                PackageFileServiceMock
                    .Setup(x => x.DoesValidationSetPackageExistAsync(ValidationSet))
                    .ReturnsAsync(true)
                    .Callback(() => operations.Add(nameof(IValidationFileService.DoesValidationSetPackageExistAsync)));
                PackageFileServiceMock
                    .Setup(x => x.UpdatePackageBlobMetadataInValidationSetAsync(It.IsAny<PackageValidationSet>()))
                    .ReturnsAsync(new PackageStreamMetadata())
                    .Callback(() => operations.Add(nameof(IValidationFileService.UpdatePackageBlobMetadataInValidationSetAsync)));
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationSetPackageToPackageFileAsync(ValidationSet, It.IsAny<IAccessCondition>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => operations.Add(nameof(IValidationFileService.CopyValidationSetPackageToPackageFileAsync)));
                PackageServiceMock
                    .Setup(x => x.UpdateStatusAsync(Package, PackageStatus.Available, true))
                    .Returns(Task.CompletedTask)
                    .Callback(() => operations.Add(nameof(IEntityService<Package>.UpdateStatusAsync)));
                PackageFileServiceMock
                    .Setup(x => x.DeleteValidationPackageFileAsync(ValidationSet))
                    .Returns(Task.CompletedTask)
                    .Callback(() => operations.Add(nameof(IValidationFileService.DeleteValidationPackageFileAsync)));
                
                await Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available);

                var expectedOrder = new[]
                {
                    nameof(IValidationFileService.DoesValidationSetPackageExistAsync),
                    nameof(IValidationFileService.UpdatePackageBlobMetadataInValidationSetAsync),
                    nameof(IValidationFileService.CopyValidationSetPackageToPackageFileAsync),
                    nameof(IEntityService<Package>.UpdateStatusAsync),
                    nameof(IValidationFileService.DeleteValidationPackageFileAsync),
                };

                Assert.Equal(expectedOrder, operations);
            }

            [Fact]
            public async Task AlwaysUsesValidationSetPackageWhenHasAnyProcessor()
            {
                ValidationSet.PackageValidations = new List<PackageValidation>
                {
                    new PackageValidation { Type = "SomeValidatorA" },
                    new PackageValidation { Type = "SomeValidatorB" },
                    new PackageValidation { Type = "SomeProcessorA" },
                    new PackageValidation { Type = "SomeProcessorB" },
                };
                var expected = new CloudBlobStorageException("Validation set package not found!");
                ValidatorProviderMock
                    .Setup(x => x.IsNuGetProcessor(It.Is<string>(n => n.Contains("Processor"))))
                    .Returns(true);
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationSetPackageToPackageFileAsync(ValidationSet, It.IsAny<IAccessCondition>()))
                    .Throws(expected);

                var actual = await Assert.ThrowsAsync<CloudBlobStorageException>(
                    () => Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available));
                Assert.Same(expected, actual);

                PackageFileServiceMock.Verify(
                    x => x.UpdatePackageBlobMetadataInValidationSetAsync(It.IsAny<PackageValidationSet>()),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.UpdatePackageBlobMetadataInValidationAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
                PackageFileServiceMock.Verify(
                    x => x.CopyValidationPackageToPackageFileAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
                PackageFileServiceMock.Verify(
                    x => x.DoesValidationSetPackageExistAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
                ValidatorProviderMock.Verify(
                    x => x.IsNuGetProcessor("SomeValidatorA"),
                    Times.Once);
                ValidatorProviderMock.Verify(
                    x => x.IsNuGetProcessor("SomeValidatorB"),
                    Times.Once);
                ValidatorProviderMock.Verify(
                    x => x.IsNuGetProcessor("SomeProcessorA"),
                    Times.Once);
                ValidatorProviderMock.Verify(
                    x => x.IsNuGetProcessor("SomeProcessorB"),
                    Times.Never); // Never checked, since SomeProcessorA was found.
            }

            [Fact]
            public async Task CopyDbUpdateDeleteInCorrectOrderWhenValidationSetPackageDoesNotExist()
            {
                var operations = new List<string>();

                PackageFileServiceMock
                    .Setup(x => x.DoesValidationSetPackageExistAsync(ValidationSet))
                    .ReturnsAsync(false)
                    .Callback(() => operations.Add(nameof(IValidationFileService.DoesValidationSetPackageExistAsync)));
                PackageFileServiceMock
                    .Setup(x => x.UpdatePackageBlobMetadataInValidationAsync(It.IsAny<PackageValidationSet>()))
                    .ReturnsAsync(new PackageStreamMetadata())
                    .Callback(() => operations.Add(nameof(IValidationFileService.UpdatePackageBlobMetadataInValidationAsync)));
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationPackageToPackageFileAsync(ValidationSet))
                    .Returns(Task.CompletedTask)
                    .Callback(() => operations.Add(nameof(IValidationFileService.CopyValidationPackageToPackageFileAsync)));
                PackageServiceMock
                    .Setup(x => x.UpdateStatusAsync(Package, PackageStatus.Available, true))
                    .Returns(Task.CompletedTask)
                    .Callback(() => operations.Add(nameof(IEntityService<Package>.UpdateStatusAsync)));
                PackageFileServiceMock
                    .Setup(x => x.DeleteValidationPackageFileAsync(ValidationSet))
                    .Returns(Task.CompletedTask)
                    .Callback(() => operations.Add(nameof(IValidationFileService.DeleteValidationPackageFileAsync)));
                
                await Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available);

                var expectedOrder = new[]
                {
                    nameof(IValidationFileService.DoesValidationSetPackageExistAsync),
                    nameof(IValidationFileService.UpdatePackageBlobMetadataInValidationAsync),
                    nameof(IValidationFileService.CopyValidationPackageToPackageFileAsync),
                    nameof(IEntityService<Package>.UpdateStatusAsync),
                    nameof(IValidationFileService.DeleteValidationPackageFileAsync),
                };

                Assert.Equal(expectedOrder, operations);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task TracksMissingNupkgForAvailablePackage(bool validationFileExists)
            {
                Package.PackageStatusKey = PackageStatus.Available;
                PackageFileServiceMock
                    .Setup(pfs => pfs.DoesPackageFileExistAsync(ValidationSet))
                    .ReturnsAsync(false);
                PackageFileServiceMock
                    .Setup(pfs => pfs.DoesValidationPackageFileExistAsync(ValidationSet))
                    .ReturnsAsync(validationFileExists);

                await Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available);

                TelemetryServiceMock.Verify(
                    x => x.TrackMissingNupkgForAvailablePackage(
                        ValidationSet.PackageId,
                        ValidationSet.PackageNormalizedVersion,
                        ValidationSet.ValidationTrackingId.ToString()),
                    Times.Once);
            }
        }

        public class MakePackageFailedValidationAsync : BaseFacts
        {
            [Fact]
            public async Task SetsPackageStatusToFailedValidation()
            {
                await Target.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.FailedValidation);

                PackageServiceMock.Verify(
                    x => x.UpdateStatusAsync(Package, PackageStatus.FailedValidation, true),
                    Times.Once);
                PackageServiceMock.Verify(
                    x => x.UpdateStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()),
                    Times.Once);
                TelemetryServiceMock.Verify(
                    x => x.TrackPackageStatusChange(ValidationSet.PackageId, ValidationSet.PackageNormalizedVersion, ValidationSet.ValidationTrackingId, PackageStatus.Validating, PackageStatus.FailedValidation),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.DeletePackageForValidationSetAsync(ValidationSet),
                    Times.Never);
            }
        }

        public class BaseFacts
        {
            public BaseFacts()
            {
                Package = new Package
                {
                    PackageRegistration = new PackageRegistration(),
                    PackageStatusKey = PackageStatus.Validating,
                };
                ValidationSet = new PackageValidationSet
                {
                    PackageValidations = new List<PackageValidation>
                    {
                        new PackageValidation { Type = "SomeValidator" },
                    }
                };

                PackageServiceMock = new Mock<IEntityService<Package>>();
                PackageFileServiceMock = new Mock<IValidationFileService>();
                ValidatorProviderMock = new Mock<IValidatorProvider>();
                TelemetryServiceMock = new Mock<ITelemetryService>();
                LoggerMock = new Mock<ILogger<EntityStatusProcessor<Package>>>();
                CoreLicenseFileServiceMock = new Mock<ICoreLicenseFileService>();
                CoreReadmeFileServiceMock = new Mock<ICoreReadmeFileService>();

                var streamMetadata = new PackageStreamMetadata()
                {
                    Size = 1,
                    Hash = "hash",
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId
                };

                SasDefinitionConfiguration = new SasDefinitionConfiguration()
                {
                    PackageStatusProcessorSasDefinition = "PackageStatusProcessorSasDefinition"
                };
                SasDefinitionConfigurationMock = new Mock<IOptionsSnapshot<SasDefinitionConfiguration>>();
                SasDefinitionConfigurationMock.Setup(x => x.Value).Returns(() => SasDefinitionConfiguration);

                Target = new PackageStatusProcessor(
                    PackageServiceMock.Object,
                    PackageFileServiceMock.Object,
                    ValidatorProviderMock.Object,
                    TelemetryServiceMock.Object,
                    SasDefinitionConfigurationMock.Object,
                    LoggerMock.Object,
                    CoreLicenseFileServiceMock.Object,
                    CoreReadmeFileServiceMock.Object);

                PackageValidatingEntity = new PackageValidatingEntity(Package);
            }

            public Package Package { get; }
            public PackageValidationSet ValidationSet { get; }
            public Mock<IEntityService<Package>> PackageServiceMock { get; }
            public Mock<IValidationFileService> PackageFileServiceMock { get; }
            public Mock<IValidatorProvider> ValidatorProviderMock { get; }
            public Mock<ITelemetryService> TelemetryServiceMock { get; }
            public Mock<ILogger<EntityStatusProcessor<Package>>> LoggerMock { get; }
            public Mock<ICoreLicenseFileService> CoreLicenseFileServiceMock { get; }
            public Mock<IOptionsSnapshot<SasDefinitionConfiguration>> SasDefinitionConfigurationMock;

            public Mock<ICoreReadmeFileService> CoreReadmeFileServiceMock { get; }
            public EntityStatusProcessor<Package> Target { get; }
            public PackageValidatingEntity PackageValidatingEntity { get; }
            public SasDefinitionConfiguration SasDefinitionConfiguration { get; }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Moq;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class PackageStatusProcessorFacts
    {
        public class SetPackageStatusAsync : BaseFacts
        {
            [Theory]
            [InlineData(PackageStatus.Deleted)]
            [InlineData(PackageStatus.Validating)]
            public async Task RejectsUnsupportedPackageStatus(PackageStatus packageStatus)
            {
                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => Target.SetPackageStatusAsync(Package, ValidationSet, packageStatus));

                Assert.Equal("packageStatus", ex.ParamName);
                Assert.Contains("A package can only transition to the Available or FailedValidation states.", ex.Message);
            }

            [Theory]
            [InlineData(PackageStatus.Available)]
            [InlineData(PackageStatus.FailedValidation)]
            public async Task RejectsDeletedPackage(PackageStatus packageStatus)
            {
                Package.PackageStatusKey = PackageStatus.Deleted;

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => Target.SetPackageStatusAsync(Package, ValidationSet, packageStatus));

                Assert.Equal("package", ex.ParamName);
                Assert.Contains("A package in the Deleted state cannot be processed.", ex.Message);
            }

            [Fact]
            public async Task RejectsAvailableToFailedValidation()
            {
                Package.PackageStatusKey = PackageStatus.Available;

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.FailedValidation));

                Assert.Equal("packageStatus", ex.ParamName);
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

                await Target.SetPackageStatusAsync(Package, ValidationSet, toStatus);

                if (fromStatus != toStatus)
                {
                    TelemetryServiceMock.Verify(
                        x => x.TrackPackageStatusChange(fromStatus, toStatus),
                        Times.Once);
                    TelemetryServiceMock.Verify(
                        x => x.TrackPackageStatusChange(It.IsAny<PackageStatus>(), It.IsAny<PackageStatus>()),
                        Times.Once);
                }
                else
                {
                    TelemetryServiceMock.Verify(
                        x => x.TrackPackageStatusChange(It.IsAny<PackageStatus>(), It.IsAny<PackageStatus>()),
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
                PackageFileServiceMock
                    .Setup(x => x.DownloadPackageFileToDiskAsync(Package))
                    .ReturnsAsync(stream);

                var streamMetadata = new PackageStreamMetadata()
                {
                    Size = Package.PackageFileSize,
                    Hash = Package.Hash,
                    HashAlgorithm = Package.HashAlgorithm
                };

                PackageFileServiceMock
                    .Setup(x => x.UpdatePackageBlobMetadataAsync(It.IsAny<Package>()))
                    .ReturnsAsync(streamMetadata);

                await Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available);

                PackageServiceMock.Verify(
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
                    .Setup(x => x.DownloadPackageFileToDiskAsync(Package))
                    .ReturnsAsync(stream);
                var streamMetadata = new PackageStreamMetadata()
                {
                    Size = stream.Length,
                    Hash = expectedHash,
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId
                };
                PackageFileServiceMock
                    .Setup(x => x.UpdatePackageBlobMetadataAsync(It.IsAny<Package>()))
                    .ReturnsAsync(streamMetadata);
                PackageServiceMock
                    .Setup(x => x.UpdatePackageStreamMetadataAsync(Package, It.IsAny<PackageStreamMetadata>(), false))
                    .Returns(Task.CompletedTask)
                    .Callback<Package, PackageStreamMetadata, bool>((_, m, __) => actual = m);

                await Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available);

                Assert.NotNull(actual);
                Assert.Equal(content.Length, actual.Size);
                Assert.Equal(expectedHash, actual.Hash);
                Assert.Equal("SHA512", actual.HashAlgorithm);
                PackageServiceMock.Verify(
                    x => x.UpdatePackageStreamMetadataAsync(Package, actual, false),
                    Times.Once);
            }

            [Fact]
            public async Task AllowsPackageAlreadyInPublicContainerWhenValidationSetPackageDoesNotExist()
            {
                PackageFileServiceMock
                    .Setup(x => x.DoesValidationSetPackageExistAsync(It.IsAny<PackageValidationSet>()))
                    .ReturnsAsync(false);
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationPackageToPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Throws(new InvalidOperationException("Duplicate!"));
                
                await Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available);

                PackageFileServiceMock.Verify(
                    x => x.CopyValidationPackageToPackageFileAsync(Package.PackageRegistration.Id, Package.NormalizedVersion),
                    Times.Once);
                PackageServiceMock.Verify(
                    x => x.UpdatePackageStatusAsync(Package, PackageStatus.Available, true),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.DeleteValidationPackageFileAsync(Package.PackageRegistration.Id, Package.Version),
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
                    () => Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available));

                Assert.Same(expected, actual);

                PackageFileServiceMock.Verify(
                    x => x.CopyValidationSetPackageToPackageFileAsync(ValidationSet, It.Is<IAccessCondition>(y => y.IfNoneMatchETag == "*")),
                    Times.Once);
                PackageServiceMock.Verify(
                    x => x.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()),
                    Times.Never);
                PackageFileServiceMock.Verify(
                    x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
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
                var expected = new StorageException(new RequestResult { HttpStatusCode = (int)HttpStatusCode.PreconditionFailed }, "Changed!", null);

                PackageFileServiceMock
                    .Setup(x => x.DoesValidationSetPackageExistAsync(It.IsAny<PackageValidationSet>()))
                    .ReturnsAsync(true);
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationSetPackageToPackageFileAsync(It.IsAny<PackageValidationSet>(), It.IsAny<IAccessCondition>()))
                    .Throws(expected);

                var actual = await Assert.ThrowsAsync<StorageException>(
                    () => Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available));

                Assert.Same(expected, actual);

                PackageFileServiceMock.Verify(
                    x => x.CopyValidationSetPackageToPackageFileAsync(ValidationSet, It.Is<IAccessCondition>(y => y.IfMatchETag == "\"some-etag\"")),
                    Times.Once);
                PackageServiceMock.Verify(
                    x => x.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()),
                    Times.Never);
                PackageFileServiceMock.Verify(
                    x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
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

                await Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available);

                PackageFileServiceMock.Verify(
                    x => x.DeleteValidationPackageFileAsync(Package.PackageRegistration.Id, Package.Version),
                    delete ? Times.Once() : Times.Never());
                PackageFileServiceMock.Verify(
                    x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
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
                    .Setup(ps => ps.UpdatePackageStatusAsync(Package, PackageStatus.Available, true))
                    .Throws(expected);
                
                var actual = await Assert.ThrowsAsync<Exception>(
                    () => Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available));

                Assert.Same(expected, actual);
                
                PackageFileServiceMock.Verify(
                    x => x.DeletePackageFileAsync(Package.PackageRegistration.Id, Package.Version),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.DeletePackageForValidationSetAsync(ValidationSet),
                    Times.Never);
            }

            [Fact]
            public async Task DoesNotDeletePackageFromPublicStorageOnDbUpdateFailureIfCopiedAndOriginallyAvailable()
            {
                Package.PackageStatusKey = PackageStatus.Available;

                var expected = new Exception("Everything failed");
                PackageServiceMock
                    .Setup(ps => ps.UpdatePackageStatusAsync(Package, PackageStatus.Available, true))
                    .Throws(expected);

                var actual = await Assert.ThrowsAsync<Exception>(
                    () => Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available));

                Assert.Same(expected, actual);

                PackageFileServiceMock.Verify(
                    x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
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
                    .Setup(ps => ps.UpdatePackageStatusAsync(Package, PackageStatus.Available, true))
                    .Throws(expected);
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationPackageToPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Throws(new InvalidOperationException("Duplicate!"));

                var actual = await Assert.ThrowsAsync<Exception>(
                    () => Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available));

                Assert.Same(expected, actual);

                PackageFileServiceMock.Verify(
                    x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
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
                    .Callback(() => operations.Add(nameof(IValidationPackageFileService.DoesValidationSetPackageExistAsync)));
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationSetPackageToPackageFileAsync(ValidationSet, It.IsAny<IAccessCondition>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => operations.Add(nameof(IValidationPackageFileService.CopyValidationSetPackageToPackageFileAsync)));
                PackageServiceMock
                    .Setup(x => x.UpdatePackageStatusAsync(Package, PackageStatus.Available, true))
                    .Returns(Task.CompletedTask)
                    .Callback(() => operations.Add(nameof(ICorePackageService.UpdatePackageStatusAsync)));
                PackageFileServiceMock
                    .Setup(x => x.DeleteValidationPackageFileAsync(Package.PackageRegistration.Id, Package.Version))
                    .Returns(Task.CompletedTask)
                    .Callback(() => operations.Add(nameof(IValidationPackageFileService.DeleteValidationPackageFileAsync)));
                
                await Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available);

                var expectedOrder = new[]
                {
                    nameof(IValidationPackageFileService.DoesValidationSetPackageExistAsync),
                    nameof(IValidationPackageFileService.CopyValidationSetPackageToPackageFileAsync),
                    nameof(ICorePackageService.UpdatePackageStatusAsync),
                    nameof(IValidationPackageFileService.DeleteValidationPackageFileAsync),
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
                var expected = new StorageException("Validation set package not found!");
                ValidatorProviderMock
                    .Setup(x => x.IsProcessor(It.Is<string>(n => n.Contains("Processor"))))
                    .Returns(true);
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationSetPackageToPackageFileAsync(ValidationSet, It.IsAny<IAccessCondition>()))
                    .Throws(expected);

                var actual = await Assert.ThrowsAsync<StorageException>(
                    () => Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available));
                Assert.Same(expected, actual);
                PackageFileServiceMock.Verify(
                    x => x.CopyValidationPackageToPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Never);
                PackageFileServiceMock.Verify(
                    x => x.DoesValidationSetPackageExistAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
                ValidatorProviderMock.Verify(
                    x => x.IsProcessor("SomeValidatorA"),
                    Times.Once);
                ValidatorProviderMock.Verify(
                    x => x.IsProcessor("SomeValidatorB"),
                    Times.Once);
                ValidatorProviderMock.Verify(
                    x => x.IsProcessor("SomeProcessorA"),
                    Times.Once);
                ValidatorProviderMock.Verify(
                    x => x.IsProcessor("SomeProcessorB"),
                    Times.Never); // Never checked, since SomeProcessorA was found.
            }

            [Fact]
            public async Task CopyDbUpdateDeleteInCorrectOrderWhenValidationSetPackageDoesNotExist()
            {
                var operations = new List<string>();

                PackageFileServiceMock
                    .Setup(x => x.DoesValidationSetPackageExistAsync(ValidationSet))
                    .ReturnsAsync(false)
                    .Callback(() => operations.Add(nameof(IValidationPackageFileService.DoesValidationSetPackageExistAsync)));
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationPackageToPackageFileAsync(Package.PackageRegistration.Id, Package.NormalizedVersion))
                    .Returns(Task.CompletedTask)
                    .Callback(() => operations.Add(nameof(IValidationPackageFileService.CopyValidationPackageToPackageFileAsync)));
                PackageServiceMock
                    .Setup(x => x.UpdatePackageStatusAsync(Package, PackageStatus.Available, true))
                    .Returns(Task.CompletedTask)
                    .Callback(() => operations.Add(nameof(ICorePackageService.UpdatePackageStatusAsync)));
                PackageFileServiceMock
                    .Setup(x => x.DeleteValidationPackageFileAsync(Package.PackageRegistration.Id, Package.Version))
                    .Returns(Task.CompletedTask)
                    .Callback(() => operations.Add(nameof(IValidationPackageFileService.DeleteValidationPackageFileAsync)));
                
                await Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available);

                var expectedOrder = new[]
                {
                    nameof(IValidationPackageFileService.DoesValidationSetPackageExistAsync),
                    nameof(IValidationPackageFileService.CopyValidationPackageToPackageFileAsync),
                    nameof(ICorePackageService.UpdatePackageStatusAsync),
                    nameof(IValidationPackageFileService.DeleteValidationPackageFileAsync),
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
                    .Setup(pfs => pfs.DoesPackageFileExistAsync(Package))
                    .ReturnsAsync(false);
                PackageFileServiceMock
                    .Setup(pfs => pfs.DoesValidationPackageFileExistAsync(Package))
                    .ReturnsAsync(validationFileExists);

                await Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available);

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
                await Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.FailedValidation);

                PackageServiceMock.Verify(
                    x => x.UpdatePackageStatusAsync(Package, PackageStatus.FailedValidation, true),
                    Times.Once);
                PackageServiceMock.Verify(
                    x => x.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()),
                    Times.Once);
                TelemetryServiceMock.Verify(
                    x => x.TrackPackageStatusChange(PackageStatus.Validating, PackageStatus.FailedValidation),
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

                PackageServiceMock = new Mock<ICorePackageService>();
                PackageFileServiceMock = new Mock<IValidationPackageFileService>();
                ValidatorProviderMock = new Mock<IValidatorProvider>();
                TelemetryServiceMock = new Mock<ITelemetryService>();
                LoggerMock = new Mock<ILogger<PackageStatusProcessor>>();

                var streamMetadata = new PackageStreamMetadata()
                {
                    Size = 1,
                    Hash = "hash",
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId
                };

                PackageFileServiceMock
                    .Setup(x => x.UpdatePackageBlobMetadataAsync(It.IsAny<Package>()))
                    .ReturnsAsync(streamMetadata);

                Target = new PackageStatusProcessor(
                    PackageServiceMock.Object,
                    PackageFileServiceMock.Object,
                    ValidatorProviderMock.Object,
                    TelemetryServiceMock.Object,
                    LoggerMock.Object);
            }

            public Package Package { get; }
            public PackageValidationSet ValidationSet { get; }
            public Mock<ICorePackageService> PackageServiceMock { get; }
            public Mock<IValidationPackageFileService> PackageFileServiceMock { get; }
            public Mock<IValidatorProvider> ValidatorProviderMock { get; }
            public Mock<ITelemetryService> TelemetryServiceMock { get; }
            public Mock<ILogger<PackageStatusProcessor>> LoggerMock { get; }
            public PackageStatusProcessor Target { get; }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
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

            [Fact]
            public async Task AllowsPackageAlreadyInPublicContainerWhenValidationSetPackageExists()
            {
                PackageFileServiceMock
                    .Setup(x => x.DoesValidationSetPackageExistAsync(It.IsAny<PackageValidationSet>()))
                    .ReturnsAsync(true);
                PackageFileServiceMock
                    .Setup(x => x.CopyValidationSetPackageToPackageFileAsync(It.IsAny<PackageValidationSet>()))
                    .Throws(new InvalidOperationException("Duplicate!"));

                await Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available);

                PackageFileServiceMock.Verify(
                    x => x.CopyValidationSetPackageToPackageFileAsync(ValidationSet),
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

            [Fact]
            public async Task DoesNotCopyPackageIfItsAvailable()
            {
                Package.PackageStatusKey = PackageStatus.Available;
                
                await Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available);

                PackageFileServiceMock.Verify(
                    x => x.CopyValidationSetPackageToPackageFileAsync(It.IsAny<PackageValidationSet>()),
                    Times.Never);
            }

            [Fact]
            public async Task DeletesValidationPackageOnSuccess()
            {
                await Target.SetPackageStatusAsync(Package, ValidationSet, PackageStatus.Available);

                PackageFileServiceMock.Verify(
                    x => x.DeleteValidationPackageFileAsync(Package.PackageRegistration.Id, Package.Version),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
                PackageFileServiceMock.Verify(
                    x => x.DeletePackageForValidationSetAsync(ValidationSet),
                    Times.Never);
            }

            [Fact]
            public async Task DeletesPackageFromPublicStorageOnDbUpdateFailureIfCopied()
            {
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
                    .Setup(x => x.CopyValidationSetPackageToPackageFileAsync(ValidationSet))
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
                ValidationSet = new PackageValidationSet();

                PackageServiceMock = new Mock<ICorePackageService>();
                PackageFileServiceMock = new Mock<IValidationPackageFileService>();
                TelemetryServiceMock = new Mock<ITelemetryService>();
                LoggerMock = new Mock<ILogger<PackageStatusProcessor>>();

                Target = new PackageStatusProcessor(
                    PackageServiceMock.Object,
                    PackageFileServiceMock.Object,
                    TelemetryServiceMock.Object,
                    LoggerMock.Object);
            }

            public Package Package { get; }
            public PackageValidationSet ValidationSet { get; }
            public Mock<ICorePackageService> PackageServiceMock { get; }
            public Mock<IValidationPackageFileService> PackageFileServiceMock { get; }
            public Mock<ITelemetryService> TelemetryServiceMock { get; }
            public Mock<ILogger<PackageStatusProcessor>> LoggerMock { get; }
            public PackageStatusProcessor Target { get; }
        }
    }
}

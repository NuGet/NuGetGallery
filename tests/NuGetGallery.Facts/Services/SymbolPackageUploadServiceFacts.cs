// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGetGallery
{
    public class SymbolPackageUploadServiceFacts
    {
        private static SymbolPackageUploadService CreateService(
            Mock<ISymbolPackageService> symbolPackageService = null,
            Mock<ISymbolPackageFileService> symbolPackageFileService = null,
            Mock<IEntitiesContext> entitiesContext = null,
            Mock<IValidationService> validationService = null,
            Mock<IPackageService> packageService = null,
            Mock<ITelemetryService> telemetryService = null,
            Mock<IContentObjectService> contentObjectService = null)
        {
            if (symbolPackageService == null)
            {
                symbolPackageService = new Mock<ISymbolPackageService>();
                symbolPackageService
                    .Setup(x => x.CreateSymbolPackage(It.IsAny<Package>(), It.IsAny<PackageStreamMetadata>()))
                    .Returns((Package package, PackageStreamMetadata streamMetadata) =>
                    {
                        var symbolPackage = new SymbolPackage()
                        {
                            Package = package,
                            PackageKey = package.Key,
                            Created = DateTime.UtcNow
                        };
                        package.SymbolPackages.Add(symbolPackage);

                        return symbolPackage;
                    })
                    .Verifiable();
            }

            if (symbolPackageFileService == null)
            {
                symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService
                    .Setup(x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                    .Completes();
                symbolPackageFileService
                    .Setup(x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Completes();
                symbolPackageFileService
                    .Setup(x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Completes();
            }

            if (entitiesContext == null)
            {
                entitiesContext = new Mock<IEntitiesContext>();
                entitiesContext
                    .Setup(x => x.SaveChangesAsync())
                    .CompletesWith(0);
            }

            validationService = validationService ?? new Mock<IValidationService>();
            packageService = packageService ?? new Mock<IPackageService>();
            telemetryService = telemetryService ?? new Mock<ITelemetryService>();
            if (contentObjectService == null)
            {
                contentObjectService = new Mock<IContentObjectService>();
                contentObjectService
                    .Setup(x => x.SymbolsConfiguration.IsSymbolsUploadEnabledForUser(It.IsAny<User>()))
                    .Returns(true);
            }

            var symbolPackageUploadService = new Mock<SymbolPackageUploadService>(
                symbolPackageService.Object,
                symbolPackageFileService.Object,
                entitiesContext.Object,
                validationService.Object,
                packageService.Object,
                telemetryService.Object,
                contentObjectService.Object);

            return symbolPackageUploadService.Object;
        }

        public class TheValidateUploadedSymbolsPackage
        {
            [Fact]
            public async Task WillReturnNotAllowedForAnyUser()
            {
                // Arrange
                var contentObjectService = new Mock<IContentObjectService>();
                contentObjectService
                    .Setup(x => x.SymbolsConfiguration.IsSymbolsUploadEnabledForUser(It.IsAny<User>()))
                    .Returns(false);

                // Act
                var service = CreateService(contentObjectService: contentObjectService);
                var result = await service.ValidateUploadedSymbolsPackage(new MemoryStream(), new User());

                // Assert
                Assert.NotNull(result);
                Assert.Equal(SymbolPackageValidationResultType.UserNotAllowedToUpload, result.Type);
            }

            [Fact]
            public async Task WillReturnInvalidResultForInvalidPackage()
            {
                // Arrange and act
                var service = CreateService();
                byte[] data = new byte[100];
                var result = await service.ValidateUploadedSymbolsPackage(new MemoryStream(data), new User());

                // Assert
                Assert.NotNull(result);
                Assert.Equal(SymbolPackageValidationResultType.Invalid, result.Type);
            }

            [Fact]
            public async Task WillReturnMissingPackageForDeletedPackage()
            {
                // Arrange 
                var packageService = new Mock<IPackageService>();
                Package package = new Package() { PackageStatusKey = PackageStatus.Deleted };
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(package);
                var service = CreateService(packageService: packageService);
                var symbolPackage = TestPackage.CreateTestSymbolPackageStream("theId", "1.0.42");

                // Act
                var result = await service.ValidateUploadedSymbolsPackage(symbolPackage, new User());

                // Assert
                Assert.NotNull(result);
                Assert.Equal(SymbolPackageValidationResultType.MissingPackage, result.Type);
            }

            [Fact]
            public async Task WillReturnSymbolPackageExistsForPendingValidation()
            {
                // Arrange 
                var packageService = new Mock<IPackageService>();
                Package package = new Package() { PackageStatusKey = PackageStatus.Available };
                var symbolPackage = new SymbolPackage() { Package = package, Key = 10, StatusKey = PackageStatus.Validating };
                package.SymbolPackages.Add(symbolPackage);
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(package);
                var service = CreateService(packageService: packageService);
                var symbolPackageStream = TestPackage.CreateTestSymbolPackageStream("theId", "1.0.42");

                // Act
                var result = await service.ValidateUploadedSymbolsPackage(symbolPackageStream, new User());

                // Assert
                Assert.NotNull(result);
                Assert.Equal(SymbolPackageValidationResultType.SymbolsPackagePendingValidation, result.Type);
            }

            [Fact]
            public async Task WillReturnInvalidWhenSynchronousSymbolValidationsFailAndTrackTelemetry()
            {
                // Arrange 
                Package package = new Package() { PackageStatusKey = PackageStatus.Available };
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(package);
                var symbolPackageService = new Mock<ISymbolPackageService>();
                symbolPackageService
                    .Setup(x => x.EnsureValidAsync(It.IsAny<PackageArchiveReader>()))
                    .ThrowsAsync(new InvalidPackageException("invalid package"));
                var telemetryService = new Mock<ITelemetryService>();

                var service = CreateService(packageService: packageService, symbolPackageService: symbolPackageService, telemetryService: telemetryService);
                var symbolPackageStream = TestPackage.CreateTestSymbolPackageStream("theId", "1.0.42");

                // Act
                var result = await service.ValidateUploadedSymbolsPackage(symbolPackageStream, new User());

                // Assert
                Assert.NotNull(result);
                Assert.Equal(SymbolPackageValidationResultType.Invalid, result.Type);
                telemetryService
                    .Verify(x => x.TrackSymbolPackageFailedGalleryValidationEvent(It.IsAny<string>(), It.IsAny<string>()),
                        times: Times.Once);
            }

            [Fact]
            public async Task WillReturnAcceptedForValidPackage()
            {
                // Arrange 
                Package package = new Package() { PackageStatusKey = PackageStatus.Available };
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(package);
                var symbolPackageService = new Mock<ISymbolPackageService>();
                symbolPackageService
                    .Setup(x => x.EnsureValidAsync(It.IsAny<PackageArchiveReader>()))
                    .Completes();

                var service = CreateService(packageService: packageService, symbolPackageService: symbolPackageService);
                var symbolPackageStream = TestPackage.CreateTestSymbolPackageStream("theId", "1.0.42");

                // Act
                var result = await service.ValidateUploadedSymbolsPackage(symbolPackageStream, new User());

                // Assert
                Assert.NotNull(result);
                Assert.NotNull(result.Package);
                Assert.Equal(SymbolPackageValidationResultType.Accepted, result.Type);
            }
        }

        public class TheCreateAndUploadSymbolsPackageMethod
        {
            [Theory]
            [InlineData(PackageStatus.Deleted)]
            [InlineData(PackageStatus.FailedValidation)]
            public async Task WillThrowExceptionIfValidationDoesNotSetValidStatus(PackageStatus invalidStatus)
            {
                var validationService = new Mock<IValidationService>();
                validationService
                    .Setup(x => x.UpdatePackageAsync(It.IsAny<SymbolPackage>()))
                    .Returns((SymbolPackage sp) =>
                    {
                        sp.StatusKey = invalidStatus;
                        return Task.CompletedTask;
                    })
                    .Verifiable();

                var service = CreateService(validationService: validationService);

                var package = new Package();
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.CreateAndUploadSymbolsPackage(package, new MemoryStream()));
            }

            [Fact]
            public async Task WillReturnConflictIfFileExistsInContainer()
            {
                // Arrange
                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService
                    .Setup(x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                    .Throws(new FileAlreadyExistsException())
                    .Verifiable();

                var service = CreateService(symbolPackageFileService: symbolPackageFileService);
                var package = new Package();

                // Act
                var result = await service.CreateAndUploadSymbolsPackage(package, new MemoryStream());

                // Assert
                Assert.NotNull(result);
                Assert.Equal(PackageCommitResult.Conflict, result);
                symbolPackageFileService.Verify(x => x.SavePackageFileAsync(package, It.IsAny<Stream>(), It.IsAny<bool>()), Times.Once);
            }

            [Fact]
            public async Task WillDeleteFailedValidationSnupkg()
            {
                // Arrange
                var symbolPackageService = new Mock<ISymbolPackageService>();
                symbolPackageService
                    .Setup(x => x.CreateSymbolPackage(It.IsAny<Package>(), It.IsAny<PackageStreamMetadata>()))
                    .Returns((Package package1, PackageStreamMetadata streamMetadata) =>
                    {
                        var symbolPackage = new SymbolPackage()
                        {
                            Package = package1,
                            PackageKey = package1.Key,
                            Created = DateTime.UtcNow,
                            StatusKey = PackageStatus.Validating
                        };

                        return symbolPackage;
                    })
                    .Verifiable();

                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService
                    .Setup(x => x.DoesValidationPackageFileExistAsync(It.IsAny<Package>()))
                    .ReturnsAsync(true)
                    .Verifiable();
                symbolPackageFileService
                    .Setup(x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Completes()
                    .Verifiable();
                symbolPackageFileService
                    .Setup(x => x.SaveValidationPackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Completes()
                    .Verifiable();

                var service = CreateService(symbolPackageService: symbolPackageService, symbolPackageFileService: symbolPackageFileService);
                var package = new Package() { PackageRegistration = new PackageRegistration() { Id = "theId" }, Version = "1.0.23" };
                package.SymbolPackages.Add(new SymbolPackage()
                {
                    StatusKey = PackageStatus.FailedValidation,
                    Key = 1232,
                    Package = package
                });

                // Act
                var result = await service.CreateAndUploadSymbolsPackage(package, new MemoryStream());

                // Assert
                Assert.NotNull(result);
                Assert.Equal(PackageCommitResult.Success, result);
                symbolPackageFileService.VerifyAll();
            }

            [Fact]
            public async Task WillDeleteSavedFileAndThrowWhenDBWriteFails()
            {
                // Arrange
                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService
                    .Setup(x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                    .Completes()
                    .Verifiable();
                symbolPackageFileService
                    .Setup(x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Completes()
                    .Verifiable();

                var entitiesContext = new Mock<IEntitiesContext>();
                entitiesContext
                    .Setup(x => x.SaveChangesAsync())
                    .ThrowsAsync(new EntityException("Something happened"))
                    .Verifiable();

                var service = CreateService(symbolPackageFileService: symbolPackageFileService, entitiesContext: entitiesContext);
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration() { Id = "TheId" },
                    Version = "1.0.0"
                };

                // Act and Assert
                await Assert.ThrowsAsync<EntityException>(async () => await service.CreateAndUploadSymbolsPackage(package, new MemoryStream()));

                symbolPackageFileService.Verify(x => x.SavePackageFileAsync(package, It.IsAny<Stream>(), It.IsAny<bool>()), Times.Once);
                symbolPackageFileService.Verify(x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version), Times.Once);
                entitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task WillMarkExistingSymbolPackagesForDeletionAndOverwriteFiles()
            {
                // Arrange
                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                var telemetryService = new Mock<ITelemetryService>();
                symbolPackageFileService
                    .Setup(x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                    .Completes()
                    .Verifiable();

                var service = CreateService(symbolPackageFileService: symbolPackageFileService, telemetryService: telemetryService);
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration() { Id = "TheId" },
                    Version = "1.0.0"
                };
                var existingAvailableSymbolPackage = new SymbolPackage()
                {
                    Package = package,
                    Key = 1,
                    StatusKey = PackageStatus.Available
                };
                var existingDeletedSymbolPackage = new SymbolPackage()
                {
                    Package = package,
                    Key = 2,
                    StatusKey = PackageStatus.Deleted
                };
                package.SymbolPackages.Add(existingAvailableSymbolPackage);
                package.SymbolPackages.Add(existingDeletedSymbolPackage);

                // Act
                var result = await service.CreateAndUploadSymbolsPackage(package, new MemoryStream());

                // Assert
                Assert.Equal(PackageStatus.Deleted, existingAvailableSymbolPackage.StatusKey);
                Assert.Equal(PackageStatus.Deleted, existingDeletedSymbolPackage.StatusKey);
                symbolPackageFileService.Verify(x => x.SavePackageFileAsync(package, It.IsAny<Stream>(), true), Times.Once);
                telemetryService.Verify(x => x.TrackSymbolPackagePushEvent(package.PackageRegistration.Id, package.NormalizedVersion), Times.Once);
                Assert.NotNull(result);
                Assert.Equal(PackageCommitResult.Success, result);
            }
        }

        public class TheDeleteSymbolsPackageAsyncMethod
        {
            [Fact]
            public async Task ThrowsExceptionForNullPackage()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.DeleteSymbolsPackageAsync(null));
            }

            [Fact]
            public async Task WillNotDeleteSymbolsPackageIfItDoesNotExist()
            {
                // Arrange
                var symbolPackage = new SymbolPackage()
                {
                    Package = new Package()
                    {
                        PackageRegistration = new PackageRegistration() { Id = "foo" },
                        Version = "1.0.0",
                        NormalizedVersion = "1.0.0"
                    },
                    StatusKey = PackageStatus.Available
                };
                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService
                    .Setup(x => x.DoesPackageFileExistAsync(It.IsAny<Package>()))
                    .ReturnsAsync(false)
                    .Verifiable();
                var symbolPackageService = new Mock<ISymbolPackageService>();
                symbolPackageService
                    .Setup(x => x.UpdateStatusAsync(It.IsAny<SymbolPackage>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()))
                    .Completes()
                    .Verifiable();

                var service = CreateService(symbolPackageFileService: symbolPackageFileService, symbolPackageService: symbolPackageService);

                // Act
                await service.DeleteSymbolsPackageAsync(symbolPackage);

                // Assert
                symbolPackageService
                    .Verify(x => x.UpdateStatusAsync(symbolPackage, PackageStatus.Deleted, true), Times.Once);
                symbolPackageFileService
                    .Verify(x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                symbolPackageFileService
                    .Verify(x => x.DoesPackageFileExistAsync(It.IsAny<Package>()), Times.Once);
            }

            [Fact]
            public async Task WillDeleteSymbolsPackageIfItDoesExist()
            {
                // Arrange
                var symbolPackage = new SymbolPackage()
                {
                    Package = new Package()
                    {
                        PackageRegistration = new PackageRegistration() { Id = "foo" },
                        Version = "1.0.0",
                        NormalizedVersion = "1.0.0"
                    },
                    StatusKey = PackageStatus.Available
                };
                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService
                    .Setup(x => x.DoesPackageFileExistAsync(It.IsAny<Package>()))
                    .ReturnsAsync(true)
                    .Verifiable();
                symbolPackageFileService
                    .Setup(x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Completes()
                    .Verifiable();
                var symbolPackageService = new Mock<ISymbolPackageService>();
                symbolPackageService
                    .Setup(x => x.UpdateStatusAsync(symbolPackage, PackageStatus.Deleted, true))
                    .Completes()
                    .Verifiable();

                var service = CreateService(symbolPackageFileService: symbolPackageFileService, symbolPackageService: symbolPackageService);

                // Act
                await service.DeleteSymbolsPackageAsync(symbolPackage);

                // Assert
                symbolPackageService.Verify();
                symbolPackageFileService.Verify();
            }
        }
    }
}
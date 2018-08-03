// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
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
            Mock<IValidationService> validationService = null)
        {
            if (symbolPackageService == null)
            {
                symbolPackageService = new Mock<ISymbolPackageService>();
                symbolPackageService
                    .Setup(x => x.CreateSymbolPackage(It.IsAny<Package>(), It.IsAny<PackageStreamMetadata>()))
                    .Returns((Package package, PackageStreamMetadata streamMetadata) => {
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

            var symbolPackageUploadService = new Mock<SymbolPackageUploadService>(
                symbolPackageService.Object,
                symbolPackageFileService.Object,
                entitiesContext.Object,
                validationService.Object);

            return symbolPackageUploadService.Object;
        }

        public class TheCreateAndUploadSymbolsPackageMethod
        {
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
                var result = await service.CreateAndUploadSymbolsPackage(package, new PackageStreamMetadata(), new MemoryStream());

                // Assert
                Assert.NotNull(result);
                Assert.Equal(PackageCommitResult.Conflict, result);
                symbolPackageFileService.Verify(x => x.SavePackageFileAsync(package, It.IsAny<Stream>(), It.IsAny<bool>()), Times.Once);
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
                await Assert.ThrowsAsync<EntityException>(async () => await service.CreateAndUploadSymbolsPackage(package, new PackageStreamMetadata(), new MemoryStream()));

                symbolPackageFileService.Verify(x => x.SavePackageFileAsync(package, It.IsAny<Stream>(), It.IsAny<bool>()), Times.Once);
                symbolPackageFileService.Verify(x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version), Times.Once);
                entitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task WillMarkExistingSymbolPackagesForDeletionAndOverwriteFiles()
            {
                // Arrange
                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService
                    .Setup(x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                    .Completes()
                    .Verifiable();

                var service = CreateService(symbolPackageFileService: symbolPackageFileService);
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
                var result = await service.CreateAndUploadSymbolsPackage(package, new PackageStreamMetadata(), new MemoryStream());

                // Assert
                Assert.Equal(PackageStatus.Deleted, existingAvailableSymbolPackage.StatusKey);
                Assert.Equal(PackageStatus.Deleted, existingDeletedSymbolPackage.StatusKey);
                symbolPackageFileService.Verify(x => x.SavePackageFileAsync(package, It.IsAny<Stream>(), true), Times.Once);
                Assert.NotNull(result);
                Assert.Equal(PackageCommitResult.Success, result);
            }
        }
    }
}
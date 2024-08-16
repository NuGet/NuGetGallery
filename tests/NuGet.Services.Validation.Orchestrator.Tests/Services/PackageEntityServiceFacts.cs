// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using NuGetGallery;
using NuGetGallery.Packaging;
using NuGet.Services.Validation.Orchestrator;
using Xunit;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation
{
    public class PackageEntityServiceFacts
    {
        private const int PackageKey = 1001;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageNormalizedVersion = "1.0.0";
        private static DateTime PackageCreated = new DateTime(2018, 4, 4, 4, 4, 4);

        public class TheFindPackageByIdAndVersionStrictMethod
        {
            [Fact]
            public void ReturnsNullIfPackageDoesNotExist()
            {
                // Arrange
                var mockCorePackageService = new Mock<ICorePackageService>();
                var mockIPackageEntityRepository = new Mock<IEntityRepository<Package>>();
                mockCorePackageService.Setup(c => c.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns((Package)null);
                var service = new PackageEntityService(mockCorePackageService.Object, mockIPackageEntityRepository.Object);

                // Act & Assert
                var result = service.FindPackageByIdAndVersionStrict("Id", "version");

                Assert.Null(result);
            }

            [Fact]
            public void ReturnsThePackageEntityAsIValidatingEntity()
            {
                // Arrange
                var package = CreatePackage();

                var mockCorePackageService = new Mock<ICorePackageService>();
                var mockIPackageEntityRepository = new Mock<IEntityRepository<Package>>();
                mockCorePackageService.Setup(c => c.FindPackageByIdAndVersionStrict(PackageId, PackageNormalizedVersion)).Returns(package);
                var service = new PackageEntityService(mockCorePackageService.Object, mockIPackageEntityRepository.Object);

                // Act & Assert
                var result = service.FindPackageByIdAndVersionStrict(PackageId, PackageNormalizedVersion);

                Assert.Equal(PackageKey, result.EntityRecord.Key);
                Assert.Equal(PackageKey, result.Key);
                Assert.Equal(PackageId, result.EntityRecord.PackageRegistration.Id);
                Assert.Equal(PackageNormalizedVersion, result.EntityRecord.NormalizedVersion);
                Assert.Equal(PackageCreated, result.Created);
            }
        }

        public class TheUpdatedStatusAsyncMethod
        {
            [Fact]
            public void InvokeTheICorePackageServices()
            {
                // Arrange
                var package = CreatePackage();
                var mockCorePackageService = new Mock<ICorePackageService>();
                var mockIPackageEntityRepository = new Mock<IEntityRepository<Package>>();
                mockCorePackageService.Setup(c => c.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), true)).Returns(Task.CompletedTask);
                var service = new PackageEntityService(mockCorePackageService.Object, mockIPackageEntityRepository.Object);

                // Act & Assert
                var result = service.UpdateStatusAsync(package, PackageStatus.Available, true);

                mockCorePackageService
                .Verify(s => s.UpdatePackageStatusAsync(package, PackageStatus.Available, true), Times.Once);
            }
        }

        public class TheUpdateMetadataAsyncMethod
        {
            [Fact]
            public void NullMetadataIsNoop()
            {
                // Arrange
                var package = CreatePackage();
                var mockCorePackageService = new Mock<ICorePackageService>();
                var mockIPackageEntityRepository = new Mock<IEntityRepository<Package>>();
                var service = new PackageEntityService(mockCorePackageService.Object, mockIPackageEntityRepository.Object);

                // Act & Assert
                var result = service.UpdateMetadataAsync(package, null, true);

                mockCorePackageService
                .Verify(s => s.UpdatePackageStreamMetadataAsync(package, It.IsAny<PackageStreamMetadata>(), true), Times.Never);
            }

            [Fact]
            public void IncorrectMetadataTypeIsNoop()
            {
                // Arrange
                var package = CreatePackage();
                var mockCorePackageService = new Mock<ICorePackageService>();
                var mockIPackageEntityRepository = new Mock<IEntityRepository<Package>>();
                var service = new PackageEntityService(mockCorePackageService.Object, mockIPackageEntityRepository.Object);

                // Act & Assert
                var result = service.UpdateMetadataAsync(package, "NotMetadata", true);

                mockCorePackageService
                .Verify(s => s.UpdatePackageStreamMetadataAsync(package, It.IsAny<PackageStreamMetadata>(), true), Times.Never);
            }

            [Fact]
            public void IdenticalMetadataChangesIsNoop()
            {
                // Arrange
                var package = CreatePackage();
                package.PackageFileSize = 100;
                package.HashAlgorithm = "SHA512";
                package.Hash = "rQw3wx1psxXzqB8TyM3nAQlK2RcluhsNwxmcqXE2YbgoDW735o8TPmIR4uWpoxUERddvFwjgRSGw7gNPCwuvJg==";
                var metadata = new PackageStreamMetadata()
                {
                    Hash = package.Hash,
                    HashAlgorithm = package.HashAlgorithm,
                    Size = package.PackageFileSize
                };

                var mockCorePackageService = new Mock<ICorePackageService>();
                var mockIPackageEntityRepository = new Mock<IEntityRepository<Package>>();
                var service = new PackageEntityService(mockCorePackageService.Object, mockIPackageEntityRepository.Object);

                // Act & Assert
                var result = service.UpdateMetadataAsync(package, metadata, true);

                mockCorePackageService
                .Verify(s => s.UpdatePackageStreamMetadataAsync(package, metadata, true), Times.Never);
            }

            [Fact]
            public void MetadataChangeInvokeICoreService()
            {
                // Arrange
                var package = CreatePackage();
                package.PackageFileSize = 100;
                package.HashAlgorithm = "SHA512";
                package.Hash = "rQw3wx1psxXzqB8TyM3nAQlK2RcluhsNwxmcqXE2YbgoDW735o8TPmIR4uWpoxUERddvFwjgRSGw7gNPCwuvJg==";
                var metadata = new PackageStreamMetadata()
                {
                    Hash = package.Hash,
                    HashAlgorithm = package.HashAlgorithm,
                    Size = package.PackageFileSize*2
                };

                var mockCorePackageService = new Mock<ICorePackageService>();
                var mockIPackageEntityRepository = new Mock<IEntityRepository<Package>>();
                mockCorePackageService.Setup(c => c.UpdatePackageStreamMetadataAsync(package, metadata, true)).Returns(Task.CompletedTask);
                var service = new PackageEntityService(mockCorePackageService.Object, mockIPackageEntityRepository.Object);

                // Act & Assert
                var result = service.UpdateMetadataAsync(package, metadata, true);

                mockCorePackageService
                .Verify(s => s.UpdatePackageStreamMetadataAsync(package, metadata, true), Times.Once);
            }
        }

        private static Package CreatePackage()
        {
            return new Package()
            {
                NormalizedVersion = PackageNormalizedVersion,
                PackageRegistration = new PackageRegistration()
                {
                    Id = PackageId
                },
                Key = PackageKey,
                Created = PackageCreated
            };
        }
    }
}

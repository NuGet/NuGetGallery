// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Services.Entities;
using System;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery
{
    public class CoreSymbolPackageServiceFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void Constructor_WhenSymbolPackageRepositoryIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new CoreSymbolPackageService(symbolPackageRepository: null, corePackageService: Mock.Of<ICorePackageService>()));

                Assert.Equal("symbolPackageRepository", exception.ParamName);
            }

            [Fact]
            public void Constructor_CorePackageServiceIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new CoreSymbolPackageService(Mock.Of<IEntityRepository<SymbolPackage>>(), corePackageService: null));

                Assert.Equal("corePackageService", exception.ParamName);
            }
        }

        public class TheUpdatePackageStatusMethod
        {
            [Fact]
            public async Task RejectsNullPackage()
            {
                // Arrange
                SymbolPackage symbolPackage = null;
                var service = CreateService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.UpdateStatusAsync(symbolPackage, PackageStatus.Deleted, commitChanges: true));
            }

            [Fact]
            public async Task DoesNotCommitWhenNoChangeOfStatus()
            {
                // Arrange
                var symbolPackageRepository = new Mock<IEntityRepository<SymbolPackage>>();
                var symbolPackage = new SymbolPackage
                {
                    Package = new Package
                    {
                        PackageRegistration = new PackageRegistration(),
                    },
                    StatusKey = PackageStatus.Available
                };

                var service = CreateService(symbolPackageRepository: symbolPackageRepository);

                // Act
                await service.UpdateStatusAsync(symbolPackage, PackageStatus.Available, commitChanges: true);

                // Assert
                symbolPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Theory]
            [InlineData(false, 0)]
            [InlineData(true, 1)]
            public async Task CommitsTheCorrectNumberOfTimes(bool commitChanges, int commitCount)
            {
                // Arrange
                var symbolPackageRepository = new Mock<IEntityRepository<SymbolPackage>>();
                var symbolPackage = new SymbolPackage
                {
                    Package = new Package
                    {
                        PackageRegistration = new PackageRegistration(),
                    },
                    StatusKey = PackageStatus.Validating
                };

                var service = CreateService(symbolPackageRepository: symbolPackageRepository);

                // Act
                await service.UpdateStatusAsync(symbolPackage, PackageStatus.Available, commitChanges);

                // Assert
                symbolPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Exactly(commitCount));
            }
        }

        private static ICoreSymbolPackageService CreateService(
            Mock<IEntityRepository<SymbolPackage>> symbolPackageRepository = null,
            Mock<ICorePackageService> corePackageService = null)
        {
            return CreateMockService(symbolPackageRepository, corePackageService).Object;
        }

        private static Mock<CoreSymbolPackageService> CreateMockService(
            Mock<IEntityRepository<SymbolPackage>> symbolPackageRepository = null,
            Mock<ICorePackageService> corePackageService = null)
        {
            symbolPackageRepository = symbolPackageRepository ?? new Mock<IEntityRepository<SymbolPackage>>();
            corePackageService = corePackageService ?? new Mock<ICorePackageService>();

            var packageService = new Mock<CoreSymbolPackageService>(
                symbolPackageRepository.Object,
                corePackageService.Object);

            packageService.CallBase = true;

            return packageService;
        }
    }
}
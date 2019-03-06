// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;
using NuGetGallery.TestUtils;
using Xunit;
using ClientPackageType = NuGet.Packaging.Core.PackageType;

namespace NuGetGallery
{
    public class SymbolPackageServiceFacts
    {
        private static ISymbolPackageService CreateService(
            Mock<IEntityRepository<SymbolPackage>> symbolPackageRepository = null,
            Mock<IPackageService> packageService = null,
            Action<Mock<SymbolPackageService>> setup = null)
        {
            symbolPackageRepository = symbolPackageRepository ?? new Mock<IEntityRepository<SymbolPackage>>();
            packageService = packageService ?? new Mock<IPackageService>();

            var symbolPackageService = new Mock<SymbolPackageService>(
                symbolPackageRepository.Object,
                packageService.Object);

            symbolPackageService.CallBase = true;

            if (setup != null)
            {
                setup(symbolPackageService);
            }

            return symbolPackageService.Object;
        }

        private static List<ClientPackageType> CreateSymbolPackageTypesObject()
        {
            return new List<ClientPackageType>()
                {
                    new ClientPackageType("SymbolsPackage", ClientPackageType.EmptyVersion)
                };
        }

        private static Action<ZipArchive> CreatePopulatePackageAction(string extension)
        {
            return archive =>
            {
                var entryList = new List<ZipArchiveEntry>() {
                            archive.CreateEntry("file1" + extension)
                        };

                foreach (var entry in entryList)
                {
                    using (var stream = entry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write("Fake file.");
                    }
                }
            };
        }

        public class TheEnsureValidAsyncMethod
        {
            [Fact]
            public async Task WillThrowForNullPackageArchiveReader()
            {
                var service = CreateService();

                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.EnsureValidAsync(null));
            }

            [Fact]
            public async Task WillThrowForMissingSymbolsPackageType()
            {
                var service = CreateService();
                var invalidSymbolPackageStream = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                var packageArchiveReader = PackageServiceUtility.CreateArchiveReader(invalidSymbolPackageStream);

                await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.EnsureValidAsync(packageArchiveReader));
            }

            [Fact]
            public async Task WillThrowForIncorrectSymbolsPackageTypeVersion()
            {
                var service = CreateService();
                var packageTypes = new List<ClientPackageType>()
                {
                    new ClientPackageType("SymbolsPackage", new Version("1.1"))
                };
                var invalidSymbolPackageStream = TestPackage.CreateTestPackageStream("theId", "1.0.42", packageTypes: packageTypes);
                var packageArchiveReader = PackageServiceUtility.CreateArchiveReader(invalidSymbolPackageStream);

                await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.EnsureValidAsync(packageArchiveReader));
            }

            [Fact]
            public async Task WillThrowForMultiplePackageTypesInNuspec()
            {
                var service = CreateService();
                var packageTypes = CreateSymbolPackageTypesObject();
                packageTypes.Add(new ClientPackageType("RandomPackageType", ClientPackageType.EmptyVersion));

                var invalidSymbolPackageStream = TestPackage.CreateTestPackageStream("theId", "1.0.42", packageTypes: packageTypes);
                var packageArchiveReader = PackageServiceUtility.CreateArchiveReader(invalidSymbolPackageStream);

                await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.EnsureValidAsync(packageArchiveReader));
            }

            [Fact]
            public async Task WillThrowForAuthorsMetadataInNuspec()
            {
                var service = CreateService();
                var packageTypes = CreateSymbolPackageTypesObject();

                var invalidSymbolPackageStream = TestPackage.CreateTestPackageStream("theId", "1.0.42", authors: "Random authors", packageTypes: packageTypes);
                var packageArchiveReader = PackageServiceUtility.CreateArchiveReader(invalidSymbolPackageStream);

                await Assert.ThrowsAsync<InvalidDataException>(async () => await service.EnsureValidAsync(packageArchiveReader));
            }

            [Fact]
            public async Task WillThrowForOwnersMetadataInNuspec()
            {
                var service = CreateService();
                var packageTypes = CreateSymbolPackageTypesObject();

                var invalidSymbolPackageStream = TestPackage.CreateTestPackageStream("theId", "1.0.42", owners: "Random owners", packageTypes: packageTypes);
                var packageArchiveReader = PackageServiceUtility.CreateArchiveReader(invalidSymbolPackageStream);

                await Assert.ThrowsAsync<InvalidDataException>(async () => await service.EnsureValidAsync(packageArchiveReader));
            }

            [Theory]
            [InlineData(".dll")]
            [InlineData(".exe")]
            [InlineData(".jpg")]
            [InlineData(".mp4")]
            public async Task WillThrowForInvalidFilesInSnupkg(string extension)
            {
                var service = CreateService();
                var action = CreatePopulatePackageAction(extension);

                var invalidSymbolPackageStream = TestPackage.CreateTestSymbolPackageStream("theId", "1.0.42", populatePackage: action);
                var packageArchiveReader = PackageServiceUtility.CreateArchiveReader(invalidSymbolPackageStream);

                await Assert.ThrowsAsync<InvalidDataException>(async () => await service.EnsureValidAsync(packageArchiveReader));
            }

            [Theory]
            [InlineData(".pdb")]
            [InlineData(".xml")]
            [InlineData(".psmdcp")]
            [InlineData(".rels")]
            [InlineData(".p7s")]
            public async Task WillNotThrowForValidSnupkgFile(string extension)
            {
                var service = CreateService();
                var action = CreatePopulatePackageAction(extension);

                var validSymbolPackageStream = TestPackage.CreateTestSymbolPackageStream("theId", "1.0.42", populatePackage: action);
                var packageArchiveReader = PackageServiceUtility.CreateArchiveReader(validSymbolPackageStream);

                await service.EnsureValidAsync(packageArchiveReader);
            }

            [Fact]
            public async Task WillThrowForSnupkgFileWithoutSymbols()
            {
                var service = CreateService();
                var action = CreatePopulatePackageAction(".p7s");

                var validSymbolPackageStream = TestPackage.CreateTestSymbolPackageStream("theId", "1.0.42", populatePackage: action);
                var packageArchiveReader = PackageServiceUtility.CreateArchiveReader(validSymbolPackageStream);

                await Assert.ThrowsAsync<InvalidDataException>(async () => await service.EnsureValidAsync(packageArchiveReader));
            }
        }

        public class TheCreateSymbolPackageMethod
        {
            [Fact]
            public void WillThrowForNullPackage()
            {
                var service = CreateService();

                Assert.Throws<ArgumentNullException>(() => service.CreateSymbolPackage(nugetPackage: null, symbolPackageStreamMetadata: new PackageStreamMetadata()));
            }

            [Fact]
            public void WillThrowForNullPackageStreamMetadata()
            {
                var service = CreateService();

                Assert.Throws<ArgumentNullException>(() => service.CreateSymbolPackage(nugetPackage: new Package(), symbolPackageStreamMetadata: null));
            }

            [Fact]
            public void WillThrowInvalidPackageExceptionForDbFail()
            {
                // Arrange
                var symbolPackageStreamMetadata = new PackageStreamMetadata()
                {
                    Size = 12312,
                    Hash = "01asdf2130",
                    HashAlgorithm = "VerySecureAlogrithm"
                };

                var mockSymbolPackageRepository = new Mock<IEntityRepository<SymbolPackage>>();
                mockSymbolPackageRepository
                    .Setup(x => x.InsertOnCommit(It.IsAny<SymbolPackage>()))
                    .Throws(new EntityException("MyException"))
                    .Verifiable();

                var service = CreateService(mockSymbolPackageRepository);

                // Act and Assert
                Assert.Throws<InvalidPackageException>(() => service.CreateSymbolPackage(new Package(), symbolPackageStreamMetadata));
                mockSymbolPackageRepository.Verify();
            }

            [Fact]
            public void WillCreateAndReturnSymbolPackageButNotCommit()
            {
                // Arrange
                var symbolPackageStreamMetadata = new PackageStreamMetadata()
                {
                    Size = 12312,
                    Hash = "01asdf2130",
                    HashAlgorithm = "VerySecureAlogrithm"
                };
                var package = new Package();
                var mockSymbolPackageRepository = new Mock<IEntityRepository<SymbolPackage>>();
                mockSymbolPackageRepository
                    .Setup(x => x.InsertOnCommit(It.IsAny<SymbolPackage>()));

                var service = CreateService(mockSymbolPackageRepository);

                // Act
                var symbolPackage = service.CreateSymbolPackage(package, symbolPackageStreamMetadata);

                // Assert
                mockSymbolPackageRepository.Verify(x => x.InsertOnCommit(It.IsAny<SymbolPackage>()), Times.Once);
                mockSymbolPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
                Assert.NotNull(symbolPackage);
                Assert.Equal(symbolPackageStreamMetadata.Hash, symbolPackage.Hash);
                Assert.Equal(symbolPackageStreamMetadata.HashAlgorithm, symbolPackage.HashAlgorithm);
                Assert.Equal(symbolPackageStreamMetadata.Size, symbolPackage.FileSize);
                Assert.Equal(package, symbolPackage.Package);
            }
        }
    }
}
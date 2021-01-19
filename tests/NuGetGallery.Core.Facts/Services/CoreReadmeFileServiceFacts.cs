// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery.Services
{
    public class CoreReadmeFileServiceFacts
    {
        public class ExtractAndSaveReadmeFileAsyncMethod
        {
            [Fact]
            public async Task ThrowsWhenPackageIsNull()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.ExtractAndSaveReadmeFileAsync(
                    package: null,
                    packageStream: Mock.Of<Stream>()));

                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task ThrowsWhenPackageStreamIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.ExtractAndSaveReadmeFileAsync(
                    package: Mock.Of<Package>(),
                    packageStream: null));

                Assert.Equal("packageStream", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData(" ")]
            [InlineData(null)]
            public async Task ThrowsWhenNoReadmeFileSpecified(string readmeFileName)
            {
                var service = CreateService();
                var packageStream = GeneratePackageWithReadmeFile(readmeFileName);
                var package = PackageServiceUtility.CreateTestPackage();
                package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown; // tested method should ignore the package settings and check .nuspec

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExtractAndSaveReadmeFileAsync(package, packageStream));

                Assert.Contains("No readme file", ex.Message);
            }

            [Fact]
            public async Task ThrowsOnMissingReadmeFile()
            {
                var service = CreateService();
                const string readmeFileName = "readme.md";
                var packageStream = GeneratePackageWithReadmeFile(readmeFileName, false);
                var pacakge = PackageServiceUtility.CreateTestPackage();

                var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => service.ExtractAndSaveReadmeFileAsync(pacakge, packageStream));
                Assert.Contains(readmeFileName, ex.Message);
            }

            [Fact]
            public async Task WhenEmbeddedReadmeTypeIsAbsent_ThrowsArgumentException()
            {
                var service = CreateService();
                var package = CreatePackage();
                package.EmbeddedReadmeType = EmbeddedReadmeFileType.Absent;
                var packageStream = GeneratePackageWithReadmeFile("readme.md");

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.ExtractAndSaveReadmeFileAsync(package, packageStream));
                Assert.Equal("package", ex.ParamName);
                Assert.Contains("embedded readme", ex.Message);
            }

            [Theory]
            [InlineData("readme.md")]
            [InlineData("foo\\readme.md")]
            [InlineData("foo/readme.md")]
            public async Task SavesReadmeFile(String readmeFileName)
            {
                var fileServiceMock = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileServiceMock);
                var packageStream = GeneratePackageWithReadmeFile(readmeFileName);
                var package = PackageServiceUtility.CreateTestPackage();
                package.HasReadMe = true;
                package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown;
                var savedReadmeBytes = new byte[ReadmeFileContents.Length];
                var expectedFileName = BuildReadmeFileName(package.Id, package.Version);

                fileServiceMock.Setup(x => x.SaveFileAsync(
                    CoreConstants.Folders.PackagesContentFolderName,
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    true))
                    .Completes()
                    .Callback<string, string, Stream, bool>((_, __, s, ___) => s.Read(savedReadmeBytes, 0, savedReadmeBytes.Length))
                    .Verifiable();

                // Act.
                await service.ExtractAndSaveReadmeFileAsync(package, packageStream);

                // Assert.
                fileServiceMock.VerifyAll();
                Assert.Equal(ReadmeFileContents, savedReadmeBytes);
            }
        }

        public class TheDownloadReadmeFileAsyncMethod
        {
            [Fact]
            public async Task WillThrowIfPackageIsNull()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.DownloadReadmeFileAsync(null));

                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(EmbeddedReadmeFileType.Markdown)]
            public async Task WillThrowIfPackageIsMissingPackageRegistration(EmbeddedReadmeFileType readmeFileType)
            {
                var service = CreateService();
                var package = new Package { PackageRegistration = null, EmbeddedReadmeType = readmeFileType };

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.DownloadReadmeFileAsync(package));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(EmbeddedReadmeFileType.Markdown)]
            public async Task WillThrowIfPackageIsMissingPackageRegistrationId(EmbeddedReadmeFileType readmeFileType)
            {
                var service = CreateService();
                var packageRegistration = new PackageRegistration { Id = null };
                var package = new Package { PackageRegistration = packageRegistration, EmbeddedReadmeType = readmeFileType };

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.DownloadReadmeFileAsync(package));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(EmbeddedReadmeFileType.Markdown)]
            public async Task WillThrowIfPackageIsMissingNormalizedVersionAndVersion(EmbeddedReadmeFileType readmeFileType)
            {
                var service = CreateService();
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistration, NormalizedVersion = null, Version = null, EmbeddedReadmeType = readmeFileType };

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => service.DownloadReadmeFileAsync(package));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(EmbeddedReadmeFileType.Markdown)]
            public async Task WillUseNormalizedRegularVersionIfNormalizedVersionMissing(EmbeddedReadmeFileType readmeFileType)
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistration, NormalizedVersion = null, Version = "01.01.01", EmbeddedReadmeType = readmeFileType };

                await service.DownloadReadmeFileAsync(package);

                fileStorageSvc
                    .Verify(fss => fss.GetFileAsync(CoreConstants.Folders.PackagesContentFolderName, BuildReadmeFileName("theId", "1.1.1")),
                        Times.Once);
                fileStorageSvc
                    .Verify(fss => fss.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                        Times.Once);
            }

            [Fact]
            public async Task WhenExists_ReturnsMarkdownStream()
            {
                var expectedMd = "<p>Hello World!</p>";

                // Arrange.
                using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(expectedMd)))
                {
                    var fileStorageSvc = new Mock<ICoreFileStorageService>();
                    var service = CreateService(fileStorageService: fileStorageSvc);

                    var package = new Package()
                    {
                        PackageRegistration = new PackageRegistration()
                        {
                            Id = "Foo"
                        },
                        Version = "01.1.01"
                    };

                    fileStorageSvc.Setup(f => f.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                        .Returns(Task.FromResult(stream))
                        .Verifiable();

                    // Act.
                    var actualMd = await service.DownloadReadmeFileAsync(package);

                    // Assert.
                    Assert.Equal(expectedMd, actualMd);

                    fileStorageSvc.Verify(f => f.GetFileAsync(CoreConstants.Folders.PackagesContentFolderName, BuildReadmeFileName("Foo", "1.1.1")), Times.Once);
                }
            }

            [Fact]
            public async Task WhenDoesNotExist_ReturnsNull()
            {
                // Arrange
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);

                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo"
                    },
                    Version = "01.1.01"
                };

                fileStorageSvc.Setup(f => f.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.FromResult((Stream)null))
                    .Verifiable();

                // Act
                var result = await service.DownloadReadmeFileAsync(package);

                // Assert
                Assert.Null(result);

                fileStorageSvc.Verify(f => f.GetFileAsync(CoreConstants.Folders.PackagesContentFolderName, BuildReadmeFileName("Foo", "1.1.1")), Times.Once);
            }
        }

        public class TheDeleteReadmeFileAsyncMethod
        {
            [Fact]
            public async Task WillThrowIfIdIsNull()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.DeleteReadmeFileAsync(null, "1.2.3"));
                Assert.Equal("id", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfIdIsEmpty()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteReadmeFileAsync("", "1.2.3"));
                Assert.Equal("id", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfVersionIsNull()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.DeleteReadmeFileAsync("theId", null));
                Assert.Equal("version", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfVersionIsEmpty()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteReadmeFileAsync("theId", ""));
                Assert.Equal("version", ex.ParamName);
            }

            [Fact]
            public async Task WillNormalizeVersion()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);

                await service.DeleteReadmeFileAsync("theId", "01.02.03");

                fileStorageSvc
                    .Verify(fss => fss.DeleteFileAsync(CoreConstants.Folders.PackagesContentFolderName, BuildReadmeFileName("theId", "1.2.3")), Times.Once);
                fileStorageSvc
                    .Verify(fss => fss.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            }
        }

        static Package CreatePackage()
        {
            var packageRegistration = new PackageRegistration { Id = "theId", Packages = new HashSet<Package>() };
            var package = new Package { Version = "theVersion", NormalizedVersion = "theNormalizedVersion", PackageRegistration = packageRegistration };
            packageRegistration.Packages.Add(package);
            return package;
        }

        static CoreReadmeFileService CreateService(Mock<ICoreFileStorageService> fileStorageService = null)
        {
            fileStorageService = fileStorageService ?? new Mock<ICoreFileStorageService>();
            var contentFileMetadataService = new Mock<IContentFileMetadataService>();
            contentFileMetadataService
                .SetupGet(c => c.PackageContentFolderName)
                .Returns(CoreConstants.Folders.PackagesContentFolderName);

            contentFileMetadataService
                .SetupGet(c => c.PackageContentPathTemplate)
                .Returns(CoreConstants.PackageContentFileSavePathTemplate);

            return new CoreReadmeFileService(
                fileStorageService.Object, contentFileMetadataService.Object);
        }

        private static string BuildReadmeFileName(string id, string version)
        {
            return string.Format(CoreConstants.PackageContentFileSavePathTemplate + "/readme", id.ToLowerInvariant(), version.ToLowerInvariant());
        }

        private static byte[] ReadmeFileContents => Encoding.UTF8.GetBytes("Sample readme md file");

        private static MemoryStream GeneratePackageWithReadmeFile(string readmeFileName = null, bool saveReadmeFile = true)
        {
            return PackageServiceUtility.CreateNuGetPackageStream(
                readmeFilename: readmeFileName,
                readmeFileContents: readmeFileName != null && saveReadmeFile ? ReadmeFileContents : null);
        }
    }
}

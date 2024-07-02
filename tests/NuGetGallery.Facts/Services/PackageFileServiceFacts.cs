// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class PackageFileServiceFacts
    {
        public class TheDeletePackageFileMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfIdIsNullOrEmpty(string id)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.DeletePackageFileAsync(id, "theVersion").Wait());

                Assert.Equal("id", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfVersionIsNullOrEmpty(string version)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.DeletePackageFileAsync("theId", version).Wait());

                Assert.Equal("version", ex.ParamName);
            }

            [Fact]
            public async Task WillDeleteTheFileViaTheFileStorageServiceUsingThePackagesFolder()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);
                fileStorageSvc.Setup(x => x.DeleteFileAsync(CoreConstants.Folders.PackagesFolderName, It.IsAny<string>()))
                    .Completes()
                    .Verifiable();

                await service.DeletePackageFileAsync("theId", "theVersion");

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillDeleteTheFileViaTheFileStorageServiceUsingAFileNameWithIdAndVersion()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);
                fileStorageSvc.Setup(x => x.DeleteFileAsync(It.IsAny<string>(), BuildFileName("theId", "theVersion", CoreConstants.NuGetPackageFileExtension, CoreConstants.PackageFileSavePathTemplate)))
                    .Completes()
                    .Verifiable();

                await service.DeletePackageFileAsync("theId", "theVersion");

                fileStorageSvc.VerifyAll();
            }
        }

        public class TheCreateDownloadPackageActionResultMethod
        {
            [Fact]
            public void WillThrowIfPackageIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.CreateDownloadPackageActionResultAsync(new Uri("http://fake"), null).Wait());

                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingPackageRegistration()
            {
                var service = CreateService();
                var package = new Package { PackageRegistration = null };

                var ex = Assert.Throws<ArgumentException>(() => service.CreateDownloadPackageActionResultAsync(new Uri("http://fake"), package).Wait());

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingPackageRegistrationId()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = null };
                var package = new Package { PackageRegistration = packageRegistraion };

                var ex = Assert.Throws<ArgumentException>(() => service.CreateDownloadPackageActionResultAsync(new Uri("http://fake"), package).Wait());

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingVersion()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistraion, Version = null };

                var ex = Assert.Throws<ArgumentException>(() => service.CreateDownloadPackageActionResultAsync(new Uri("http://fake"), package).Wait());

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillGetAResultFromTheFileStorageServiceUsingThePackagesFolder()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);
                fileStorageSvc.Setup(x => x.CreateDownloadFileActionResultAsync(new Uri("http://fake"), CoreConstants.Folders.PackagesFolderName, It.IsAny<string>()))
                    .CompletesWithNull()
                    .Verifiable();

                await service.CreateDownloadPackageActionResultAsync(new Uri("http://fake"), CreatePackage());

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillGetAResultFromTheFileStorageServiceUsingAFileNameWithIdAndNormalizedVersion()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);
                fileStorageSvc.Setup(x => x.CreateDownloadFileActionResultAsync(new Uri("http://fake"), It.IsAny<string>(), BuildFileName("theId", "theNormalizedVersion", CoreConstants.NuGetPackageFileExtension, CoreConstants.PackageFileSavePathTemplate)))
                    .CompletesWithNull()
                    .Verifiable();

                await service.CreateDownloadPackageActionResultAsync(new Uri("http://fake"), CreatePackage());

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillUseNormalizedRegularVersionIfNormalizedVersionMissing()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);
                var packageRegistraion = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistraion, NormalizedVersion = null, Version = "01.01.01" };

                fileStorageSvc.Setup(x => x.CreateDownloadFileActionResultAsync(new Uri("http://fake"), It.IsAny<string>(), BuildFileName("theId", "1.1.1", CoreConstants.NuGetPackageFileExtension, CoreConstants.PackageFileSavePathTemplate)))
                    .CompletesWithNull()
                    .Verifiable();

                await service.CreateDownloadPackageActionResultAsync(new Uri("http://fake"), package);

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillReturnTheResultFromTheFileStorageService()
            {
                ActionResult fakeResult = new RedirectResult("http://aUrl");
                var fileStorageSvc = new Mock<IFileStorageService>();
                fileStorageSvc.Setup(x => x.CreateDownloadFileActionResultAsync(new Uri("http://fake"), It.IsAny<string>(), It.IsAny<string>()))
                    .CompletesWith(fakeResult);

                var service = CreateService(fileStorageSvc: fileStorageSvc);

                var result = await service.CreateDownloadPackageActionResultAsync(new Uri("http://fake"), CreatePackage()) as RedirectResult;

                Assert.Equal(fakeResult, result);
            }
        }

        public class TheDeleteReadMeMdFileAsync
        {
            [Fact]
            public async Task WhenPackageNull_ThrowsArgumentNullException()
            {
                var service = CreateService();

                await Assert.ThrowsAsync<ArgumentNullException>(() => service.DeleteReadMeMdFileAsync(null));
            }

            [Fact]
            public async Task WhenValid_DeletesFromStorage()
            {
                // Arrange.
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration() { Id = "Test" },
                    Version = "1.0.0",
                };

                var fileServiceMock = new Mock<IFileStorageService>();
                fileServiceMock.Setup(fs => fs.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var service = CreateService(fileServiceMock);

                // Act.
                await service.DeleteReadMeMdFileAsync(package);

                // Assert.
                fileServiceMock.Verify(fs => fs.DeleteFileAsync(CoreConstants.Folders.PackageReadMesFolderName, "active/test/1.0.0.md"), Times.Once);
            }
        }

        public class TheSaveReadMeMdFileAsyncMethod
        {
            [Fact]
            public async Task WhenPackageNull_ThrowsArgumentNullException()
            {
                var service = CreateService();

                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.SaveReadMeMdFileAsync(null, ""));
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            [InlineData("   ")]
            public async Task WhenReadMeMdMissing_ThrowsArgumentException(string markdown)
            {
                var service = CreateService();

                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.SaveReadMeMdFileAsync(new Package(), markdown));
            }

            [Fact]
            public async Task WhenValid_SavesReadMeFile()
            {
                // Arrange.
                var fileServiceMock = new Mock<IFileStorageService>();
                fileServiceMock.Setup(f => f.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();
                var service = CreateService(fileServiceMock);

                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration() { Id = "Foo" },
                    Version = "1.0.0",
                };

                // Act.
                await service.SaveReadMeMdFileAsync(package, "<p>Hello World!</p>");

                // Assert.
                fileServiceMock.Verify(f => f.SaveFileAsync(CoreConstants.Folders.PackageReadMesFolderName, "active/foo/1.0.0.md", It.IsAny<Stream>(), true),
                    Times.Once);
            }
        }

        public class TheDownloadReadMeMdFileAsyncMethod
        {
            [Fact]
            public async Task WhenPackageNull_ThrowsArgumentNull()
            {
                var service = CreateService();

                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.DownloadReadMeMdFileAsync(null));
            }

            [Fact]
            public async Task WhenExists_ReturnsMarkdownStream()
            {
                var expectedMd = "<p>Hello World!</p>";

                // Arrange.
                using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(expectedMd)))
                {
                    var fileServiceMock = new Mock<IFileStorageService>();
                    var service = CreateService(fileStorageSvc: fileServiceMock);

                    var package = new Package()
                    {
                        PackageRegistration = new PackageRegistration()
                        {
                            Id = "Foo"
                        },
                        Version = "01.1.01"
                    };
                    fileServiceMock.Setup(f => f.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                        .Returns(Task.FromResult(stream))
                        .Verifiable();

                    // Act.
                    var actualMd = await service.DownloadReadMeMdFileAsync(package);

                    // Assert.
                    Assert.Equal(expectedMd, actualMd);

                    fileServiceMock.Verify(f => f.GetFileAsync(CoreConstants.Folders.PackageReadMesFolderName, "active/foo/1.1.1.md"), Times.Once);
                }
            }

            [Fact]
            public async Task WhenDoesNotExist_ReturnsNull()
            {
                // Arrange
                var fileServiceMock = new Mock<IFileStorageService>();
                var service = CreateService(fileServiceMock);

                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo"
                    },
                    Version = "01.1.01"
                };
                fileServiceMock.Setup(f => f.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.FromResult((Stream)null))
                    .Verifiable();

                // Act
                var result = await service.DownloadReadMeMdFileAsync(package);

                // Assert
                Assert.Null(result);

                fileServiceMock.Verify(f => f.GetFileAsync(CoreConstants.Folders.PackageReadMesFolderName, "active/foo/1.1.1.md"), Times.Once);
            }
        }

        static string BuildFileName(
            string id,
            string version, string extension, string path)
        {
            return string.Format(
                path,
                id.ToLowerInvariant(),
                NuGetVersionFormatter.Normalize(version).ToLowerInvariant(), // No matter what ends up getting passed in, the version should be normalized
                extension);
        }

        static Package CreatePackage()
        {
            var packageRegistration = new PackageRegistration { Id = "theId", Packages = new HashSet<Package>() };
            var package = new Package { Version = "theVersion", NormalizedVersion = "theNormalizedVersion", PackageRegistration = packageRegistration };
            packageRegistration.Packages.Add(package);
            return package;
        }

        static PackageFileService CreateService(Mock<IFileStorageService> fileStorageSvc = null)
        {
            fileStorageSvc = fileStorageSvc ?? new Mock<IFileStorageService>();

            return new PackageFileService(
                fileStorageSvc.Object);
        }
    }
}

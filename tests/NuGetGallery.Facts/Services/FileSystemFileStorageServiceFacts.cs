// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery
{
    public class FileSystemFileStorgeServiceFacts
    {
        private static readonly Uri HttpRequestUrl = new Uri("http://nuget.org/something");

        private const string FakeConfiguredFileStorageDirectory = "theFileStorageDirectory";

        private static MemoryStream CreateFileStream()
        {
            return new MemoryStream(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }, 0, 8, true, true);
        }

        private static FileSystemFileStorageService CreateService(
            Mock<IAppConfiguration> configuration = null,
            Mock<IFileSystemService> fileSystemService = null)
        {
            if (configuration == null)
            {
                configuration = new Mock<IAppConfiguration>();
                configuration.Setup(x => x.FileStorageDirectory).Returns(FakeConfiguredFileStorageDirectory);
            }

            if (fileSystemService == null)
            {
                fileSystemService = new Mock<IFileSystemService>();
                fileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
            }

            return new FileSystemFileStorageService(
                configuration.Object,
                fileSystemService.Object);
        }

        public class TheCreateDownloadFileActionResultMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.CreateDownloadFileActionResultAsync(
                        HttpRequestUrl,
                        folderName,
                        "theFileName"));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.CreateDownloadFileActionResultAsync(
                        HttpRequestUrl,
                        Constants.PackagesFolderName,
                        fileName));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public async Task WillReturnAFilePathResultWithTheFilePath()
            {
                var service = CreateService();

                var result = await service.CreateDownloadFileActionResultAsync(HttpRequestUrl, Constants.PackagesFolderName, "theFileName") as FilePathResult;

                Assert.NotNull(result);
                Assert.Equal(
                    Path.Combine(FakeConfiguredFileStorageDirectory, Constants.PackagesFolderName, "theFileName"),
                    result.FileName);
            }

            [Fact]
            public async Task WillReturnAnHttpNotFoundResultWhenTheFileDoesNotExist()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);
                var service = CreateService(fileSystemService: fakeFileSystemService);

                var result = await service.CreateDownloadFileActionResultAsync(HttpRequestUrl, Constants.PackagesFolderName, "theFileName") as HttpNotFoundResult;

                Assert.NotNull(result);
            }

            [Fact]
            public async Task WillSetTheResultContentTypeForThePackagesFolder()
            {
                var service = CreateService();

                var result = await service.CreateDownloadFileActionResultAsync(HttpRequestUrl, Constants.PackagesFolderName, "theFileName") as FilePathResult;

                Assert.NotNull(result);
                Assert.Equal(Constants.PackageContentType, result.ContentType);
            }

            [Fact]
            public async Task WillSetTheResultDownloadFilePath()
            {
                var service = CreateService();

                var result = await service.CreateDownloadFileActionResultAsync(HttpRequestUrl, Constants.PackagesFolderName, "theFileName") as FilePathResult;

                Assert.NotNull(result);
                Assert.Equal(
                    "theFileName",
                    result.FileDownloadName);
            }
        }

        public class TheDeleteFileMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.DeleteFileAsync(
                        folderName,
                        "theFileName"));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.DeleteFileAsync(
                        Constants.PackagesFolderName,
                        fileName));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public void WillDeleteTheFileIfItExists()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
                var service = CreateService(fileSystemService: fakeFileSystemService);

                service.DeleteFileAsync(Constants.PackagesFolderName, "theFileName");

                fakeFileSystemService.Verify(
                    x => x.DeleteFile(
                        Path.Combine(FakeConfiguredFileStorageDirectory, Constants.PackagesFolderName, "theFileName")));
            }

            [Fact]
            public void WillNotDeleteTheFileIfItDoesNotExist()
            {
                var deleteWasInvoked = false;
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);
                fakeFileSystemService.Setup(x => x.DeleteFile(It.IsAny<string>())).Callback(() => deleteWasInvoked = true);
                var service = CreateService(fileSystemService: fakeFileSystemService);

                service.DeleteFileAsync(Constants.PackagesFolderName, "theFileName");

                Assert.False(deleteWasInvoked);
            }
        }

        public class TheGetFileMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.GetFileAsync(
                        folderName,
                        "theFileName"));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.GetFileAsync(
                        Constants.PackagesFolderName,
                        fileName));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public async Task WillCheckWhetherTheFileExists()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
                var service = CreateService(fileSystemService: fakeFileSystemService);
                var expectedPath = Path.Combine(
                    FakeConfiguredFileStorageDirectory,
                    "theFolderName",
                    "theFileName");

                await service.GetFileAsync("theFolderName", "theFileName");

                fakeFileSystemService.Verify(x => x.FileExists(expectedPath));
            }

            [Fact]
            public async Task WillReadTheRequestedFileWhenItExists()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
                var service = CreateService(fileSystemService: fakeFileSystemService);
                var expectedPath = Path.Combine(
                    FakeConfiguredFileStorageDirectory,
                    "theFolderName",
                    "theFileName");

                await service.GetFileAsync("theFolderName", "theFileName");

                fakeFileSystemService.Verify(x => x.OpenRead(expectedPath));
            }

            [Fact]
            public async Task WillReturnTheRequestFileStreamWhenItExists()
            {
                var expectedPath = Path.Combine(
                    FakeConfiguredFileStorageDirectory,
                    "theFolderName",
                    "theFileName");
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
                var fakeFileStream = new MemoryStream();
                fakeFileSystemService.Setup(x => x.OpenRead(expectedPath)).Returns(fakeFileStream);
                var service = CreateService(fileSystemService: fakeFileSystemService);

                var fileStream = await service.GetFileAsync("theFolderName", "theFileName");

                Assert.Same(fakeFileStream, fileStream);
            }

            [Fact]
            public async Task WillReturnNullWhenRequestedFileDoesNotExist()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);
                var service = CreateService(fileSystemService: fakeFileSystemService);

                var fileStream = await service.GetFileAsync("theFolderName", "theFileName");

                Assert.Null(fileStream);
            }
        }

        public class TheSaveFileMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveFileAsync(folderName, "theFileName", CreateFileStream()));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveFileAsync("theFolderName", fileName, CreateFileStream()));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfFileStreamIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveFileAsync("theFolderName", "theFileName", null));

                Assert.Equal("packageFile", ex.ParamName);
            }

            [Fact]
            public async Task WillCreateTheConfiguredFileStorageDirectoryIfItDoesNotExist()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);
                fakeFileSystemService.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(new MemoryStream(new byte[8]));
                var service = CreateService(fileSystemService: fakeFileSystemService);

                await service.SaveFileAsync("theFolderName", "theFileName", CreateFileStream());

                fakeFileSystemService.Verify(x => x.CreateDirectory(FakeConfiguredFileStorageDirectory));
            }

            [Fact]
            public async Task WillCreateTheFolderPathIfItDoesNotExist()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);
                fakeFileSystemService.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(new MemoryStream(new byte[8]));
                var service = CreateService(fileSystemService: fakeFileSystemService);

                await service.SaveFileAsync("theFolderName", "theFileName", CreateFileStream());

                fakeFileSystemService.Verify(x => x.CreateDirectory(Path.Combine(FakeConfiguredFileStorageDirectory, "theFolderName")));
            }

            [Fact]
            public async Task WillSaveThePackageFileToTheSpecifiedFolder()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
                fakeFileSystemService.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(new MemoryStream(new byte[8]));
                var service = CreateService(fileSystemService: fakeFileSystemService);

                await service.SaveFileAsync("theFolderName", "theFileName", CreateFileStream());

                fakeFileSystemService.Verify(
                    x =>
                    x.OpenWrite(
                        Path.Combine(
                            FakeConfiguredFileStorageDirectory,
                            "theFolderName",
                            "theFileName")));
            }

            [Fact]
            public async Task WillSaveThePackageFileBytes()
            {
                var fakePackageFile = CreateFileStream();
                var fakeFileStream = new MemoryStream(new byte[8], 0, 8, true, true);
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
                fakeFileSystemService.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(fakeFileStream);
                var service = CreateService(fileSystemService: fakeFileSystemService);

                await service.SaveFileAsync("theFolderName", "theFileName", CreateFileStream());

                for (var i = 0; i < fakePackageFile.Length; i++)
                {
                    Assert.Equal(fakePackageFile.GetBuffer()[i], fakeFileStream.GetBuffer()[i]);
                }
            }
        }
    }
}
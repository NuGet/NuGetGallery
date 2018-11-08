// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Configuration;
using NuGetGallery.Utilities;
using Xunit;

namespace NuGetGallery
{
    public class FileSystemFileStorageServiceFacts
    {
        private static readonly Uri HttpRequestUrl = new Uri("http://nuget.org/something");

        private const string FakeConfiguredFileStorageDirectory = "theFileStorageDirectory";

        private static MemoryStream CreateFileStream()
        {
            return new MemoryStream(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }, index: 0, count: 8, writable: true, publiclyVisible: true);
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
                        CoreConstants.Folders.PackagesFolderName,
                        fileName));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public async Task WillReturnAFilePathResultWithTheFilePath()
            {
                var service = CreateService();

                var result = await service.CreateDownloadFileActionResultAsync(HttpRequestUrl, CoreConstants.Folders.PackagesFolderName, "theFileName") as FilePathResult;

                Assert.NotNull(result);
                Assert.Equal(
                    Path.Combine(FakeConfiguredFileStorageDirectory, CoreConstants.Folders.PackagesFolderName, "theFileName"),
                    result.FileName);
            }

            [Fact]
            public async Task WillReturnAnHttpNotFoundResultWhenTheFileDoesNotExist()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);
                var service = CreateService(fileSystemService: fakeFileSystemService);

                var result = await service.CreateDownloadFileActionResultAsync(HttpRequestUrl, CoreConstants.Folders.PackagesFolderName, "theFileName") as HttpNotFoundResult;

                Assert.NotNull(result);
            }

            [Fact]
            public async Task WillSetTheResultContentTypeForThePackagesFolder()
            {
                var service = CreateService();

                var result = await service.CreateDownloadFileActionResultAsync(HttpRequestUrl, CoreConstants.Folders.PackagesFolderName, "theFileName") as FilePathResult;

                Assert.NotNull(result);
                Assert.Equal(CoreConstants.PackageContentType, result.ContentType);
            }

            [Fact]
            public async Task WillSetTheResultContentTypeForTheSymbolPackagesFolder()
            {
                var service = CreateService();

                var result = await service.CreateDownloadFileActionResultAsync(HttpRequestUrl, CoreConstants.Folders.SymbolPackagesFolderName, "theFileName") as FilePathResult;

                Assert.NotNull(result);
                Assert.Equal(CoreConstants.PackageContentType, result.ContentType);
            }

            [Fact]
            public async Task WillSetTheResultDownloadFilePath()
            {
                var service = CreateService();

                var result = await service.CreateDownloadFileActionResultAsync(HttpRequestUrl, CoreConstants.Folders.PackagesFolderName, "theFileName") as FilePathResult;

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
                        CoreConstants.Folders.PackagesFolderName,
                        fileName));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public void WillDeleteTheFileIfItExists()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
                var service = CreateService(fileSystemService: fakeFileSystemService);

                service.DeleteFileAsync(CoreConstants.Folders.PackagesFolderName, "theFileName");

                fakeFileSystemService.Verify(
                    x => x.DeleteFile(
                        Path.Combine(FakeConfiguredFileStorageDirectory, CoreConstants.Folders.PackagesFolderName, "theFileName")));
            }

            [Fact]
            public void WillNotDeleteTheFileIfItDoesNotExist()
            {
                var deleteWasInvoked = false;
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);
                fakeFileSystemService.Setup(x => x.DeleteFile(It.IsAny<string>())).Callback(() => deleteWasInvoked = true);
                var service = CreateService(fileSystemService: fakeFileSystemService);

                service.DeleteFileAsync(CoreConstants.Folders.PackagesFolderName, "theFileName");

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
                        CoreConstants.Folders.PackagesFolderName,
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
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeFileSystemService.Setup(x => x.OpenRead(expectedPath)).Returns(fakeFileStream);
                    var service = CreateService(fileSystemService: fakeFileSystemService);

                    var fileStream = await service.GetFileAsync("theFolderName", "theFileName");

                    Assert.Same(fakeFileStream, fileStream);
                }
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

        public class TheCopyFileAsyncMethod : IDisposable
        {
            private readonly TestDirectory _directory;
            private readonly string _srcFolderName;
            private readonly string _srcFileName;
            private readonly string _destFolderName;
            private readonly string _destFileName;
            private readonly Mock<IAppConfiguration> _appConfiguration;
            private readonly Mock<FileSystemService> _fileSystemService;
            private readonly FileSystemFileStorageService _target;

            public TheCopyFileAsyncMethod()
            {
                _directory = TestDirectory.Create();

                _srcFolderName = "validation";
                _srcFileName = "4b6f16cc-7acd-45eb-ac21-33f0d927ec14/nuget.versioning.4.5.0.nupkg";
                _destFolderName = "packages";
                _destFileName = "nuget.versioning.4.5.0.nupkg";

                _appConfiguration = new Mock<IAppConfiguration>();
                _appConfiguration
                    .Setup(x => x.FileStorageDirectory)
                    .Returns(() => _directory);

                _fileSystemService = new Mock<FileSystemService> { CallBase = true };

                _target = new FileSystemFileStorageService(
                    _appConfiguration.Object,
                    _fileSystemService.Object);
            }

            [Fact]
            public async Task CopiesFileWhenDestinationDoesNotExist()
            {
                // Arrange
                var content = "Hello, world!";
                await _target.SaveFileAsync(
                    _srcFolderName,
                    _srcFileName,
                    new MemoryStream(Encoding.ASCII.GetBytes(content)));

                // Act
                await _target.CopyFileAsync(
                    _srcFolderName,
                    _srcFileName,
                    _destFolderName,
                    _destFileName,
                    destAccessCondition: null);

                // Assert
                using (var destStream = await _target.GetFileAsync(_destFolderName, _destFileName))
                using (var reader = new StreamReader(destStream))
                {
                    Assert.Equal(content, reader.ReadToEnd());
                }
            }

            [Fact]
            public async Task ThrowsWhenDestinationIsSame()
            {
                // Arrange
                var content = "Hello, world!";
                await _target.SaveFileAsync(
                    _srcFolderName,
                    _srcFileName,
                    new MemoryStream(Encoding.ASCII.GetBytes(content)));
                await _target.SaveFileAsync(
                    _destFolderName,
                    _destFileName,
                    new MemoryStream(Encoding.ASCII.GetBytes(content)));

                await Assert.ThrowsAsync<FileAlreadyExistsException>(
                    () => _target.CopyFileAsync(
                        _srcFolderName,
                        _srcFileName,
                        _destFolderName,
                        _destFileName,
                        destAccessCondition: null));
            }

            [Fact]
            public async Task ThrowsWhenFileAlreadyExists()
            {
                // Arrange
                var content = "Hello, world!";
                await _target.SaveFileAsync(
                    _srcFolderName,
                    _srcFileName,
                    new MemoryStream(Encoding.ASCII.GetBytes(content)));
                await _target.SaveFileAsync(
                    _destFolderName,
                    _destFileName,
                    new MemoryStream(Encoding.ASCII.GetBytes("Something else.")));

                // Act & Assert
                await Assert.ThrowsAsync<FileAlreadyExistsException>(
                    () => _target.CopyFileAsync(
                        _srcFolderName,
                        _srcFileName,
                        _destFolderName,
                        _destFileName,
                        destAccessCondition: null));
            }

            public void Dispose()
            {
                _directory?.Dispose();
            }
        }

        public class TheSaveFileMethod
        {
            private const string FolderName = "theFolderName";
            private const string FileName = "theFileName";
            private const string FileContent = "theFileContent";
            private const int TaskCount = 16;

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                using (var fakeFileStream = CreateFileStream())
                {
                    var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveFileAsync(folderName, FileName, fakeFileStream));

                    Assert.Equal("folderName", ex.ParamName);
                }
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                using (var fakeFileStream = CreateFileStream())
                {
                    var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveFileAsync(FolderName, fileName, fakeFileStream));

                    Assert.Equal("fileName", ex.ParamName);
                }
            }

            [Fact]
            public async Task WillThrowIfFileStreamIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveFileAsync(FolderName, FileName, null));

                Assert.Equal("packageFile", ex.ParamName);
            }

            [Fact]
            public async Task WillCreateTheConfiguredFileStorageDirectoryIfItDoesNotExist()
            {
                const string folderName = FolderName;
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);
                using (var fakeMemoryStream = CreateFileStream())
                {
                    fakeFileSystemService.Setup(x => x.OpenWrite(It.IsAny<string>(), true)).Returns(fakeMemoryStream);
                    var service = CreateService(fileSystemService: fakeFileSystemService);

                    using (var fakePackageStream = CreateFileStream())
                    {
                        await service.SaveFileAsync(FolderName, FileName, fakePackageStream);
                    }

                    fakeFileSystemService.Verify(x => x.CreateDirectory($"{FakeConfiguredFileStorageDirectory}\\{folderName}"));
                }
            }

            [Fact]
            public async Task WillCreateTheFolderPathIfItDoesNotExist()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);
                using (var fakeMemoryStream = CreateFileStream())
                {
                    fakeFileSystemService.Setup(x => x.OpenWrite(It.IsAny<string>(), true)).Returns(fakeMemoryStream);
                    var service = CreateService(fileSystemService: fakeFileSystemService);

                    using (var fakePackageStream = CreateFileStream())
                    {
                        await service.SaveFileAsync(FolderName, FileName, fakePackageStream);
                    }

                    fakeFileSystemService.Verify(x => x.CreateDirectory(Path.Combine(FakeConfiguredFileStorageDirectory, FolderName)));
                }
            }

            [Fact]
            public async Task WillSaveThePackageFileToTheSpecifiedFolder()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
                using (var fakeMemoryStream = CreateFileStream())
                {
                    fakeFileSystemService.Setup(x => x.OpenWrite(It.IsAny<string>(), true)).Returns(fakeMemoryStream);
                    var service = CreateService(fileSystemService: fakeFileSystemService);

                    using (var fakePackageStream = CreateFileStream())
                    {
                        await service.SaveFileAsync(FolderName, FileName, fakePackageStream);
                    }

                    fakeFileSystemService.Verify(
                        x =>
                        x.OpenWrite(
                            Path.Combine(
                                FakeConfiguredFileStorageDirectory,
                                FolderName,
                                FileName),
                            true));
                }
            }

            [Fact]
            public async Task WillSaveThePackageFileBytes()
            {
                using (var fakePackageFile = CreateFileStream())
                {
                    using (var fakeFileStream = CreateFileStream())
                    {
                        var fakeFileSystemService = new Mock<IFileSystemService>();
                        fakeFileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
                        fakeFileSystemService.Setup(x => x.OpenWrite(It.IsAny<string>(), true)).Returns(fakeFileStream);
                        var service = CreateService(fileSystemService: fakeFileSystemService);

                        using (var fakePackageStream = CreateFileStream())
                        {
                            await service.SaveFileAsync(FolderName, FileName, fakePackageStream);
                        }

                        for (var i = 0; i < fakePackageFile.Length; i++)
                        {
                            Assert.Equal(fakePackageFile.GetBuffer()[i], fakeFileStream.GetBuffer()[i]);
                        }
                    }
                }
            }

            [Fact]
            public async Task WillOverwriteFileIfOverwriteTrue()
            {
                using (var fakePackageFile = CreateFileStream())
                {
                    using (var fakeFileStream = CreateFileStream())
                    {
                        var fakeFileSystemService = new Mock<IFileSystemService>();
                        fakeFileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
                        fakeFileSystemService.Setup(x => x.OpenWrite(It.IsAny<string>(), true)).Returns(fakeFileStream);
                        fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
                        var service = CreateService(fileSystemService: fakeFileSystemService);

                        await service.SaveFileAsync(FolderName, FileName, fakePackageFile);

                        for (var i = 0; i < fakePackageFile.Length; i++)
                        {
                            Assert.Equal(fakePackageFile.GetBuffer()[i], fakeFileStream.GetBuffer()[i]);
                        }

                        fakeFileSystemService.Verify();
                    }
                }
            }

            [Fact]
            public async Task WillThrowIfFileExistsAndOverwriteFalse()
            {
                using (var fakeFileStream = CreateFileStream())
                {
                    var fakeFileSystemService = new Mock<IFileSystemService>();
                    fakeFileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
                    fakeFileSystemService
                        .Setup(x => x.OpenWrite(It.IsAny<string>(), false))
                        .Throws(new IOException("The file already exists"));
                    fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
                    var service = CreateService(fileSystemService: fakeFileSystemService);

                    await Assert.ThrowsAsync<FileAlreadyExistsException>(async () => await service.SaveFileAsync(FolderName, FileName, fakeFileStream, false));

                    fakeFileSystemService.Verify();
                }
            }

            [Fact]
            public async Task WillOverwriteFileIfOverwriteTrueAndRealFileSystemIsUsed()
            {
                // Arrange
                using (var testDirectory = TestDirectory.Create())
                {
                    var fileSystemService = new FileSystemService();

                    var configuration = new Mock<IAppConfiguration>();
                    configuration
                        .Setup(x => x.FileStorageDirectory)
                        .Returns(testDirectory);

                    var service = new FileSystemFileStorageService(
                        configuration.Object,
                        fileSystemService);

                    var directory = Path.Combine(testDirectory, FolderName);
                    var filePath = Path.Combine(directory, FileName);
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(filePath, string.Empty);

                    // Act
                    await service.SaveFileAsync(
                        FolderName,
                        FileName,
                        new MemoryStream(Encoding.ASCII.GetBytes(FileContent)),
                        overwrite: true);

                    Assert.True(File.Exists(filePath), $"The file at path {filePath} should exist, but does not.");
                    Assert.Equal(FileContent, File.ReadAllText(filePath));
                }
            }

            [Fact]
            public async Task WillThrowIfFileExistsAndOverwriteFalseAndRealFileSystemIsUsed()
            {
                // Arrange
                using (var testDirectory = TestDirectory.Create())
                {
                    var fileSystemService = new FileSystemService();

                    var configuration = new Mock<IAppConfiguration>();
                    configuration
                        .Setup(x => x.FileStorageDirectory)
                        .Returns(testDirectory);

                    var service = new FileSystemFileStorageService(
                        configuration.Object,
                        fileSystemService);

                    var directory = Path.Combine(testDirectory, FolderName);
                    var filePath = Path.Combine(directory, FileName);
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(filePath, FileContent);

                    // Act
                    var exception = await Assert.ThrowsAsync<FileAlreadyExistsException>(() => service.SaveFileAsync(
                        FolderName,
                        FileName,
                        new MemoryStream(),
                        overwrite: false));

                    Assert.True(File.Exists(filePath), $"The file at path {filePath} should exist, but does not.");
                    Assert.Equal(FileContent, File.ReadAllText(filePath));
                }
            }

            [Fact]
            public async Task WillThrowIfFileExistsWhenManyThreadsAreTryingToSaveWithoutOverwriting()
            {
                // Arrange
                using (var testDirectory = TestDirectory.Create())
                {
                    var fileSystemService = new FileSystemService();

                    var configuration = new Mock<IAppConfiguration>();
                    configuration
                        .Setup(x => x.FileStorageDirectory)
                        .Returns(testDirectory);

                    var service = new FileSystemFileStorageService(
                        configuration.Object,
                        fileSystemService);

                    for (var i = 0; i < 10; i++)
                    {
                        var fileName = FileName + i;
                        var barrier = new Barrier(TaskCount);
                        var tasks = new List<Task<bool>>();

                        // Act
                        for (var taskIndex = 0; taskIndex < TaskCount; taskIndex++)
                        {
                            var task = SaveFileAsync(service, fileName, barrier);
                            tasks.Add(task);
                        }

                        var results = await Task.WhenAll(tasks);

                        // Assert
                        // One task should succeed. One should fail.
                        Assert.Equal(1, results.Count(success => success));
                        Assert.Equal(TaskCount - 1, results.Count(success => !success));
                    }
                }
            }

            private static async Task<bool> SaveFileAsync(FileSystemFileStorageService service, string fileName, Barrier barrier)
            {
                await Task.Yield();

                try
                {
                    barrier.SignalAndWait();
                    await service.SaveFileAsync(
                        FolderName,
                        fileName,
                        new MemoryStream(),
                        overwrite: false);

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public class TheSetMetadataAsyncMethod
        {
            [Fact]
            public async Task NoOps()
            {
                var service = CreateService();

                await service.SetMetadataAsync(folderName: null, fileName: null, updateMetadataAsync: null);
            }
        }

        public class TheSetPropertiesAsyncMethod
        {
            [Fact]
            public async Task NoOps()
            {
                var service = CreateService();

                await service.SetPropertiesAsync(folderName: null, fileName: null, updatePropertiesAsync: null);
            }
        }
    }
}
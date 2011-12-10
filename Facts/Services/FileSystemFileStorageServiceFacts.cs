using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Mvc;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class FileSystemFileStorgeServiceFacts
    {
        public class TheCreateDownloadFileActionResultMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.CreateDownloadFileActionResult(
                    folderName, 
                    "theFileName"));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.CreateDownloadFileActionResult(
                    Constants.PackagesFolderName, 
                    fileName));

                Assert.Equal("fileName", ex.ParamName);
            }
            
            [Fact]
            public void WillReturnAFilePathResultWithTheFilePath()
            {
                var service = CreateService();

                var result = service.CreateDownloadFileActionResult(Constants.PackagesFolderName, "theFileName") as FilePathResult;

                Assert.NotNull(result);
                Assert.Equal(
                    Path.Combine(fakeConfiguredFileStorageDirectory, Constants.PackagesFolderName, "theFileName"),
                    result.FileName);
            }

            [Fact]
            public void WillReturnAnHttpNotFoundResultWhenTheFileDoesNotExist()
            {
                var fakeFileSystemSvc = new Mock<IFileSystemService>();
                fakeFileSystemSvc.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);
                var service = CreateService(fileSystemSvc: fakeFileSystemSvc);

                var result = service.CreateDownloadFileActionResult(Constants.PackagesFolderName, "theFileName") as HttpNotFoundResult;

                Assert.NotNull(result);
            }

            [Fact]
            public void WillSetTheResultContentTypeForThePackagesFolder()
            {
                var service = CreateService();

                var result = service.CreateDownloadFileActionResult(Constants.PackagesFolderName, "theFileName") as FilePathResult;

                Assert.NotNull(result);
                Assert.Equal(Constants.PackageContentType, result.ContentType);
            }

            [Fact]
            public void WillSetTheResultDownloadFilePath()
            {
                var service = CreateService();

                var result = service.CreateDownloadFileActionResult(Constants.PackagesFolderName, "theFileName") as FilePathResult;

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
            public void WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.DeleteFile(
                    folderName,
                    "theFileName"));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.DeleteFile(
                    Constants.PackagesFolderName,
                    fileName));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public void WillDeleteTheFileIfItExists()
            {
                var fakeFileSystemSvc = new Mock<IFileSystemService>();
                fakeFileSystemSvc.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
                var service = CreateService(fileSystemSvc: fakeFileSystemSvc);

                service.DeleteFile(Constants.PackagesFolderName, "theFileName");

                fakeFileSystemSvc.Verify(x => x.DeleteFile(
                    Path.Combine(fakeConfiguredFileStorageDirectory, Constants.PackagesFolderName, "theFileName")));
            }

            [Fact]
            public void WillNotDeleteTheFileIfItDoesNotExist()
            {
                var deleteWasInvoked = false;
                var fakeFileSystemSvc = new Mock<IFileSystemService>();
                fakeFileSystemSvc.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);
                fakeFileSystemSvc.Setup(x => x.DeleteFile(It.IsAny<string>())).Callback(() => deleteWasInvoked = true);
                var service = CreateService(fileSystemSvc: fakeFileSystemSvc);

                service.DeleteFile(Constants.PackagesFolderName, "theFileName");

                Assert.False(deleteWasInvoked);
            }
        }

        public class TheGetFileMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.GetFile(
                    folderName,
                    "theFileName"));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.GetFile(
                    Constants.PackagesFolderName,
                    fileName));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public void WillCheckWhetherTheFileExists()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
                var service = CreateService(fileSystemSvc: fakeFileSystemService);
                var expectedPath = Path.Combine(
                    fakeConfiguredFileStorageDirectory,
                    "theFolderName",
                    "theFileName");

                service.GetFile("theFolderName", "theFileName");

                fakeFileSystemService.Verify(x => x.FileExists(expectedPath));
            }

            [Fact]
            public void WillReadTheRequestedFileWhenItExists()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
                var service = CreateService(fileSystemSvc: fakeFileSystemService);
                var expectedPath = Path.Combine(
                    fakeConfiguredFileStorageDirectory,
                    "theFolderName",
                    "theFileName");

                service.GetFile("theFolderName", "theFileName");

                fakeFileSystemService.Verify(x => x.OpenRead(expectedPath));
            }

            [Fact]
            public void WillReturnTheRequestFileStreamWhenItExists()
            {
                var expectedPath = Path.Combine(
                    fakeConfiguredFileStorageDirectory,
                    "theFolderName",
                    "theFileName");
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
                var fakeFileStream = new MemoryStream();
                fakeFileSystemService.Setup(x => x.OpenRead(expectedPath)).Returns(fakeFileStream);
                var service = CreateService(fileSystemSvc: fakeFileSystemService);

                var fileStream = service.GetFile("theFolderName", "theFileName");

                Assert.Same(fakeFileStream, fileStream);
            }

            [Fact]
            public void WillReturnNullWhenRequestedFileDoesNotExist()
            {
                var fakeFileSystemService = new Mock<IFileSystemService>();
                fakeFileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);
                var service = CreateService(fileSystemSvc: fakeFileSystemService);
                var expectedPath = Path.Combine(
                    fakeConfiguredFileStorageDirectory,
                    "theFolderName",
                    "theFileName");

                var fileStream = service.GetFile("theFolderName", "theFileName");

                Assert.Null(fileStream);
            }
        }

        public class TheSaveFileMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.SaveFile(folderName, "theFileName", CreateFileStream()));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.SaveFile("theFolderName", fileName, CreateFileStream()));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfFileStreamIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.SaveFile("theFolderName", "theFileName", null));

                Assert.Equal("packageFile", ex.ParamName);
            }

            [Fact]
            public void WillCreateTheConfiguredFileStorageDirectoryIfItDoesNotExist()
            {
                var fakeFileSystemSvc = new Mock<IFileSystemService>();
                fakeFileSystemSvc.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);
                fakeFileSystemSvc.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(new MemoryStream(new byte[8]));
                var service = CreateService(fileSystemSvc: fakeFileSystemSvc);

                service.SaveFile("theFolderName", "theFileName", CreateFileStream());

                fakeFileSystemSvc.Verify(x => x.CreateDirectory(fakeConfiguredFileStorageDirectory));
            }

            [Fact]
            public void WillCreateTheFolderPathIfItDoesNotExist()
            {
                var fakeFileSystemSvc = new Mock<IFileSystemService>();
                fakeFileSystemSvc.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);
                fakeFileSystemSvc.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(new MemoryStream(new byte[8]));
                var service = CreateService(fileSystemSvc: fakeFileSystemSvc);

                service.SaveFile("theFolderName", "theFileName", CreateFileStream());

                fakeFileSystemSvc.Verify(x => x.CreateDirectory(Path.Combine(fakeConfiguredFileStorageDirectory, "theFolderName")));
            }

            [Fact]
            public void WillSaveThePackageFileToTheSpecifiedFolder()
            {
                var fakeFileSystemSvc = new Mock<IFileSystemService>();
                fakeFileSystemSvc.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
                fakeFileSystemSvc.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(new MemoryStream(new byte[8]));
                var service = CreateService(fileSystemSvc: fakeFileSystemSvc);

                service.SaveFile("theFolderName", "theFileName", CreateFileStream());

                fakeFileSystemSvc.Verify(x =>
                    x.OpenWrite(
                        Path.Combine(
                            fakeConfiguredFileStorageDirectory,
                            "theFolderName",
                            "theFileName")));
            }

            [Fact]
            public void WillSaveThePackageFileBytes()
            {
                var fakePackageFile = CreateFileStream();
                var fakeFileStream = new MemoryStream(new byte[8], 0, 8, true, true);
                var fakeFileSystemSvc = new Mock<IFileSystemService>();
                fakeFileSystemSvc.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
                fakeFileSystemSvc.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(fakeFileStream);
                var service = CreateService(fileSystemSvc: fakeFileSystemSvc);

                service.SaveFile("theFolderName", "theFileName", CreateFileStream());

                for (var i = 0; i < fakePackageFile.Length; i++)
                    Assert.Equal(fakePackageFile.GetBuffer()[i], fakeFileStream.GetBuffer()[i]);
            }
        }

        static MemoryStream CreateFileStream()
        {
            return new MemoryStream(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }, 0, 8, true, true);
        }

        const string fakeConfiguredFileStorageDirectory = "theFileStorageDirectory";

        static FileSystemFileStorageService CreateService(
            Mock<IConfiguration> configuration = null,
            Mock<IFileSystemService> fileSystemSvc = null)
        {
            if (configuration == null)
            {
                configuration = new Mock<IConfiguration>();
                configuration.Setup(x => x.FileStorageDirectory).Returns(fakeConfiguredFileStorageDirectory);
            }

            if (fileSystemSvc == null)
            {
                fileSystemSvc = new Mock<IFileSystemService>();
                fileSystemSvc.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
            }

            return new FileSystemFileStorageService(
                configuration.Object,
                fileSystemSvc.Object);
        }
    }
}

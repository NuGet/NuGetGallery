using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Moq;
using NuGet;
using Xunit;

namespace NuGetGallery.Services
{
    public class NuGetExeDownloaderServiceFacts
    {
        private static readonly string _exePath = @"x:\NuGetGallery\nuget.exe";

        [Fact]
        public void CreateNuGetExeDownloadDoesNotExtractFileIfItAlreadyExistsAndIsRecent()
        {
            // Arrange
            var fileSystem = new Mock<IFileSystemService>(MockBehavior.Strict);
            fileSystem.Setup(s => s.FileExists(_exePath)).Returns(true).Verifiable();
            fileSystem.Setup(s => s.GetCreationTimeUtc(_exePath))
                      .Returns(DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(60)))
                      .Verifiable();

            // Act
            var downloaderSvc = GetDownloaderService(fileSystemSvc: fileSystem);
            var result = downloaderSvc.CreateNuGetExeDownloadActionnResult();

            // Assert
            fileSystem.Verify();
            AssertActionResult(result);
        }

        [Fact]
        public void CreateNuGetExeDownloadExtractsFileIfItDoesNotExist()
        {
            // Arrange
            var fileSystem = new Mock<IFileSystemService>(MockBehavior.Strict);
            fileSystem.Setup(s => s.FileExists(_exePath)).Returns(false);
            fileSystem.Setup(s => s.OpenWrite(_exePath)).Returns(Stream.Null);

            var package = new Package { Version = "2.0.0" };
            var packageService = new Mock<IPackageService>(MockBehavior.Strict);
            packageService.Setup(s => s.FindPackageByIdAndVersion("NuGet.CommandLine", null, false))
                          .Returns(package)
                          .Verifiable();
            var packageFileSvc = new Mock<IPackageFileService>(MockBehavior.Strict);
            packageFileSvc.Setup(s => s.DownloadPackageFile(package))
                          .Returns(CreateCommandLinePackage)
                          .Verifiable();

            // Act
            var downloaderSvc = GetDownloaderService(packageService, packageFileSvc, fileSystem);
            var result = downloaderSvc.CreateNuGetExeDownloadActionnResult();

            // Assert
            packageFileSvc.Verify();
            packageService.Verify();
            AssertActionResult(result);
        }

        [Fact]
        public void CreateNuGetExeDownloadExtractsFileIfItExistsButIsNotRecent()
        {
            // Arrange
            var fileSystem = new Mock<IFileSystemService>(MockBehavior.Strict);
            fileSystem.Setup(s => s.FileExists(_exePath)).Returns(true);
            fileSystem.Setup(s => s.GetCreationTimeUtc(_exePath))
                      .Returns(DateTime.UtcNow.Subtract(TimeSpan.FromHours(32)));
            fileSystem.Setup(s => s.OpenWrite(_exePath)).Returns(Stream.Null);

            var package = new Package { Version = "2.0.0" };
            var packageService = new Mock<IPackageService>(MockBehavior.Strict);
            packageService.Setup(s => s.FindPackageByIdAndVersion("NuGet.CommandLine", null, false))
                          .Returns(package)
                          .Verifiable();
            var packageFileSvc = new Mock<IPackageFileService>(MockBehavior.Strict);
            packageFileSvc.Setup(s => s.DownloadPackageFile(package))
                          .Returns(CreateCommandLinePackage)
                          .Verifiable();

            // Act
            var downloaderSvc = GetDownloaderService(packageService, packageFileSvc, fileSystem);
            var result = downloaderSvc.CreateNuGetExeDownloadActionnResult();

            // Assert
            packageFileSvc.Verify();
            packageService.Verify();
            AssertActionResult(result);
        }

        [Fact]
        public void UpdateExecutableExtractsExeToDisk()
        {
            // Arrange
            var fileSystem = new Mock<IFileSystemService>(MockBehavior.Strict);
            fileSystem.Setup(s => s.OpenWrite(_exePath)).Returns(Stream.Null);

            var nugetPackage = new Mock<IPackage>();
            nugetPackage.Setup(s => s.GetFiles()).Returns(new[] { CreateExePackageFile() }.AsQueryable());

            // Act
            var downloaderSvc = GetDownloaderService(fileSystemSvc: fileSystem);
            downloaderSvc.UpdateExecutable(nugetPackage.Object);

            // Assert
            fileSystem.Verify();
        }

        private static void AssertActionResult(ActionResult result)
        {
            Assert.IsType<FilePathResult>(result);
            var filePathResult = (FilePathResult)result;
            Assert.Equal(_exePath, filePathResult.FileName);
            Assert.Equal(@"application/octet-stream", filePathResult.ContentType);
            Assert.Equal(@"NuGet.exe", filePathResult.FileDownloadName);
        }

        private static Stream CreateCommandLinePackage()
        {
            var packageBuilder = new PackageBuilder
                                 {
                                     Id = "NuGet.CommandLine",
                                     Version = new SemanticVersion("2.0.0"),
                                     Description = "Some desc"
                                 };
            packageBuilder.Authors.Add("test");
            var exeFile = CreateExePackageFile();
            packageBuilder.Files.Add(exeFile);

            var memoryStream = new MemoryStream();
            packageBuilder.Save(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);

            return memoryStream;
        }

        private static IPackageFile CreateExePackageFile()
        {
            var exeFile = new Mock<IPackageFile>();
            exeFile.Setup(s => s.Path).Returns(@"tools\NuGet.exe");
            exeFile.Setup(s => s.GetStream()).Returns(Stream.Null);
            return exeFile.Object;
        }

        private static NuGetExeDownloaderService GetDownloaderService(
            Mock<IPackageService> packageSvc = null,
            Mock<IPackageFileService> packageFileSvc = null,
            Mock<IFileSystemService> fileSystemSvc = null)
        {
            packageSvc = packageSvc ?? new Mock<IPackageService>(MockBehavior.Strict);
            packageFileSvc = packageFileSvc ?? new Mock<IPackageFileService>(MockBehavior.Strict);
            fileSystemSvc = fileSystemSvc ?? new Mock<IFileSystemService>(MockBehavior.Strict);

            return new NuGetExeDownloaderService(packageSvc.Object, packageFileSvc.Object, fileSystemSvc.Object)
            {
                NuGetExePath = _exePath
            };
        }
    }
}

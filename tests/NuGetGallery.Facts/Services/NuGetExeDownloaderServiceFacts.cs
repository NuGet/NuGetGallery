using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGet;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGetGallery
{
    public class NuGetExeDownloaderServiceFacts
    {
        private static readonly Uri HttpRequestUrl = new Uri("http://nuget.org/nuget.exe");
        private static readonly Uri HttpsRequestUrl = new Uri("https://nuget.org/nuget.exe");

        [Fact]
        public async Task CreateNuGetExeDownloadDoesNotExtractFileIfItAlreadyExists()
        {
            // Arrange
            var fileStorage = new Mock<IFileStorageService>(MockBehavior.Strict);
            fileStorage.Setup(s => s.FileExistsAsync("downloads", "nuget.exe"))
                .Returns(Task.FromResult(true)).Verifiable();

            fileStorage.Setup(s => s.CreateDownloadFileActionResultAsync(HttpRequestUrl, "downloads", "nuget.exe"))
                .Returns(Task.FromResult(Mock.Of<ActionResult>()))
                .Verifiable();

            // Act
            var downloaderService = GetDownloaderService(fileStorageService: fileStorage);
            await downloaderService.CreateNuGetExeDownloadActionResultAsync(HttpRequestUrl);

            // Assert
            fileStorage.Verify();
        }

        [Fact]
        public async Task CreateNuGetExeDownloadExtractsFileIfItDoesNotExist()
        {
            // Arrange
            var fileStorage = new Mock<IFileStorageService>(MockBehavior.Strict);
            fileStorage.Setup(s => s.FileExistsAsync("downloads", "nuget.exe")).Returns(Task.FromResult(false));
            fileStorage.Setup(s => s.SaveFileAsync("downloads", "nuget.exe", It.IsAny<Stream>()))
                .Returns(Task.FromResult(0))
                .Verifiable();
            fileStorage.Setup(s => s.CreateDownloadFileActionResultAsync(HttpRequestUrl, "downloads", "nuget.exe"))
                .Returns(Task.FromResult(Mock.Of<ActionResult>()))
                .Verifiable();

            var package = new Package { Version = "2.0.0" };
            var packageService = new Mock<IPackageService>(MockBehavior.Strict);
            packageService.Setup(s => s.FindPackageByIdAndVersion("NuGet.CommandLine", null, false))
                .Returns(package)
                .Verifiable();
            var packageFileService = new Mock<IPackageFileService>(MockBehavior.Strict);
            packageFileService.Setup(s => s.DownloadPackageFileAsync(package))
                .Returns(Task.FromResult(CreateCommandLinePackage()))
                .Verifiable();

            // Act
            var downloaderService = GetDownloaderService(packageService, packageFileService, fileStorage);
            await downloaderService.CreateNuGetExeDownloadActionResultAsync(HttpRequestUrl);

            // Assert
            packageFileService.Verify();
            packageService.Verify();
        }

        [Fact]
        public async Task UpdateExecutableExtractsExeToFileStorage()
        {
            // Arrange
            var fileStorage = new Mock<IFileStorageService>(MockBehavior.Strict);
            fileStorage.Setup(s => s.SaveFileAsync("downloads", "nuget.exe", It.IsAny<Stream>()))
                .Returns(Task.FromResult(0))
                .Verifiable();

            var nugetPackage = new Mock<INupkg>();
            nugetPackage.Setup(s => s.GetFiles()).Returns(new[] { @"tools\NuGet.exe" });
            nugetPackage
                .Setup(s => s.GetSizeVerifiedFileStream("nuget.exe", 10000))
                .Returns((Stream)null);

            // Act
            var downloaderService = GetDownloaderService(fileStorageService: fileStorage);
            await downloaderService.UpdateExecutableAsync(nugetPackage.Object);

            // Assert
            fileStorage.Verify();
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
            Mock<IPackageService> packageService = null,
            Mock<IPackageFileService> packageFileService = null,
            Mock<IFileStorageService> fileStorageService = null)
        {
            packageService = packageService ?? new Mock<IPackageService>(MockBehavior.Strict);
            packageFileService = packageFileService ?? new Mock<IPackageFileService>(MockBehavior.Strict);
            fileStorageService = fileStorageService ?? new Mock<IFileStorageService>(MockBehavior.Strict);

            return new NuGetExeDownloaderService(packageService.Object, packageFileService.Object, fileStorageService.Object);
        }
    }
}
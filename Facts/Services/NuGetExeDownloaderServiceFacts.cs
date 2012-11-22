using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGet;
using Xunit;

namespace NuGetGallery.Services
{
    public class NuGetExeDownloaderServiceFacts
    {
        [Fact]
        public async Task CreateNuGetExeDownloadDoesNotExtractFileIfItAlreadyExists()
        {
            // Arrange
            var fileStorage = new Mock<IFileStorageService>(MockBehavior.Strict);
            fileStorage.Setup(s => s.FileExistsAsync("downloads", "nuget.exe"))
                .Returns(Task.FromResult(true)).Verifiable();

            fileStorage.Setup(s => s.CreateDownloadFileActionResultAsync("downloads", "nuget.exe"))
                .Returns(Task.FromResult(Mock.Of<ActionResult>()))
                .Verifiable();

            // Act
            var downloaderSvc = GetDownloaderService(fileStorageSvc: fileStorage);
            await downloaderSvc.CreateNuGetExeDownloadActionResultAsync();

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
            fileStorage.Setup(s => s.CreateDownloadFileActionResultAsync("downloads", "nuget.exe"))
                .Returns(Task.FromResult(Mock.Of<ActionResult>()))
                .Verifiable();

            var package = new Package { Version = "2.0.0" };
            var packageService = new Mock<IPackageService>(MockBehavior.Strict);
            packageService.Setup(s => s.FindPackageByIdAndVersion("NuGet.CommandLine", null, false))
                .Returns(package)
                .Verifiable();
            var packageFileSvc = new Mock<IPackageFileService>(MockBehavior.Strict);
            packageFileSvc.Setup(s => s.DownloadPackageFileAsync(package))
                .Returns(Task.FromResult(CreateCommandLinePackage()))
                .Verifiable();

            // Act
            var downloaderSvc = GetDownloaderService(packageService, packageFileSvc, fileStorage);
            await downloaderSvc.CreateNuGetExeDownloadActionResultAsync();

            // Assert
            packageFileSvc.Verify();
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

            var nugetPackage = new Mock<IPackage>();
            nugetPackage.Setup(s => s.GetFiles()).Returns(new[] { CreateExePackageFile() }.AsQueryable());

            // Act
            var downloaderSvc = GetDownloaderService(fileStorageSvc: fileStorage);
            await downloaderSvc.UpdateExecutableAsync(nugetPackage.Object);

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
            Mock<IPackageService> packageSvc = null,
            Mock<IPackageFileService> packageFileSvc = null,
            Mock<IFileStorageService> fileStorageSvc = null)
        {
            packageSvc = packageSvc ?? new Mock<IPackageService>(MockBehavior.Strict);
            packageFileSvc = packageFileSvc ?? new Mock<IPackageFileService>(MockBehavior.Strict);
            fileStorageSvc = fileStorageSvc ?? new Mock<IFileStorageService>(MockBehavior.Strict);

            return new NuGetExeDownloaderService(packageSvc.Object, packageFileSvc.Object, fileStorageSvc.Object);
        }
    }
}
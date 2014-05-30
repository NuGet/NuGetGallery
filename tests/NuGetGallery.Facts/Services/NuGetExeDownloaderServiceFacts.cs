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
            fileStorage.Setup(s => s.CreateDownloadFileActionResultAsync(HttpRequestUrl, "downloads", "nuget.exe"))
                .Returns(Task.FromResult(Mock.Of<ActionResult>()))
                .Verifiable();

            // Act
            var downloaderService = GetDownloaderService(fileStorageService: fileStorage);
            await downloaderService.CreateNuGetExeDownloadActionResultAsync(HttpRequestUrl);

            // Assert
            fileStorage.Verify();
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGet;
using NuGetGallery.Helpers;
using NuGetGallery.Migrations;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class PackageFilesControllerFacts
    {
        public class TheContentsAction
        {
            [Fact]
            public async Task WillReturnNotFoundIfPackageDoesNotExist()
            {
                // Arrange
                var controller = CreateController();

                // Act
                ActionResult result = await controller.Contents("A", "1.0");

                // Arrange
                Assert.True(result is HttpNotFoundResult);
            }

            [Fact]
            public async Task WillReturnPackageTooBigViewIfPackageIsTooBig()
            {
                // Arrange
                var package = new Package
                {
                    PackageFileSize = 3L * 1024 * 1024 + 1
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersion("A", "1.0", It.IsAny<bool>())).Returns(package);
                
                var controller = CreateController(packageService: packageService);
                

                // Act
                ViewResult result = await controller.Contents("A", "1.0") as ViewResult;

                // Arrange
                Assert.NotNull(result);
                Assert.Equal("PackageTooBig", result.ViewName);
                Assert.Equal(package, result.Model);
            }

            [Fact]
            public async Task WillReturnViewWhenPackageIsValidAndSmallerThanSizeLimit()
            {
                // Arrange
                var package = new Package
                {
                    Version = "1.0",
                    PackageFileSize = 3L * 1024 * 1024,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "A"
                    }
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersion("A", "1.0", It.IsAny<bool>())).Returns(package);

                var packageStream = CreatePackageStream("A", "1.0");
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadPackageFileAsync(package)).Returns(Task.FromResult(packageStream));

                var controller = CreateController(packageService, packageFileService);

                // Act
                ViewResult result = await controller.Contents("A", "1.0") as ViewResult;

                // Arrange
                Assert.NotNull(result);
                Assert.Equal("", result.ViewName);

                var model = result.Model as PackageContentsViewModel;
                Assert.NotNull(model);
                Assert.Equal("A", model.PackageMetadata.Id);
                Assert.Equal(new SemanticVersion("1.0"), model.PackageMetadata.Version);
                Assert.Equal("dotnetjunky", model.FlattenedAuthors);

                Assert.NotNull(model.RootFolder);
            }

            [Fact]
            public async Task WillGetPackageFileFromCacheIfAvailable()
            {
                // Arrange
                var package = new Package
                {
                    Version = "1.0",
                    PackageFileSize = 3L * 1024 * 1024,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "A"
                    }
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersion("A", "1.0", It.IsAny<bool>())).Returns(package);

                var packageStream = (MemoryStream)CreatePackageStream("A", "1.0");

                var buffer = packageStream.ToArray();

                var cacheService = new Mock<IPackageCacheService>(MockBehavior.Strict);
                cacheService.Setup(p => p.GetBytes("a.1.0")).Returns(buffer);

                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadPackageFileAsync(package)).Throws(new InvalidOperationException());

                var controller = CreateController(packageService, packageFileService, cacheService);

                // Act
                ViewResult result = await controller.Contents("A", "1.0") as ViewResult;

                // Arrange
                Assert.NotNull(result);
                Assert.Equal("", result.ViewName);

                var model = result.Model as PackageContentsViewModel;
                Assert.NotNull(model);
                Assert.Equal("A", model.PackageMetadata.Id);
                Assert.Equal(new SemanticVersion("1.0"), model.PackageMetadata.Version);
                Assert.Equal("dotnetjunky", model.FlattenedAuthors);

                Assert.NotNull(model.RootFolder);
            }
        }

        public class TheShowFileContentAction
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task WillReturnNotFoundIfFilePathIsNullOrEmpty(string filePath)
            {
                // Arrange
                var controller = CreateController();

                // Act
                ActionResult result = await controller.ShowFileContent("B", "1.0.0-alpha", filePath);

                // Arrange
                Assert.True(result is HttpNotFoundResult);
            }

            [Fact]
            public async Task WillReturnNotFoundIfPackageIsNotFound()
            {
                // Arrange
                var controller = CreateController();

                // Act
                ActionResult result = await controller.ShowFileContent("B", "1.0.0-alpha", "content\\foo.txt");

                // Arrange
                Assert.True(result is HttpNotFoundResult);
            }

            [Fact]
            public async Task WillReturnNotFoundIfPackageIsTooBig()
            {
                // Arrange
                var package = new Package
                {
                    PackageFileSize = 3L * 1024 * 1024 + 1
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersion("B", "2.0.0-beta", It.IsAny<bool>())).Returns(package);

                var controller = CreateController(packageService: packageService);

                // Act
                ActionResult result = await controller.ShowFileContent("B", "2.0.0-beta", "content\\foo.txt");

                // Arrange
                Assert.True(result is HttpNotFoundResult);
            }

            [Theory]
            [InlineData("content\\one.jpg", "image/jpg")]
            [InlineData("content\\one.jpeg", "image/jpeg")]
            [InlineData("data\\two.gif", "image/gif")]
            [InlineData("three.BMP", "image/bmp")]
            [InlineData("four.PNG", "image/png")]
            public async Task WillReturnImageResultForImageFile(string imagePath, string expectedMimeType)
            {
                // Arrange
                var package = new Package
                {
                    Version = "2.0.0-beta",
                    PackageFileSize = 3L * 1024 * 1024,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "B"
                    }
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersion("B", "2.0.0-beta", It.IsAny<bool>())).Returns(package);

                var fileStream = new MemoryStream();
                fileStream.Write(new byte[] { 1 }, 0, 1);
                fileStream.Seek(0, SeekOrigin.Begin);

                var file = new Mock<IPackageFile>();
                file.Setup(f => f.Path).Returns(imagePath);
                file.Setup(f => f.GetStream()).Returns(fileStream);

                var packageStream = CreatePackageStream("B", "2.0.0-beta", new [] { file.Object });
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadPackageFileAsync(package)).Returns(Task.FromResult(packageStream));

                var controller = CreateController(packageService, packageFileService);

                // Act
                ImageResult result = await controller.ShowFileContent("B", "2.0.0-beta", imagePath) as ImageResult;

                // Arrange
                Assert.NotNull(result);
                Assert.Equal(1, result.ImageStream.Length);
                Assert.Equal(expectedMimeType, result.ContentType, StringComparer.OrdinalIgnoreCase);
            }

            [Theory]
            [PropertyData("GenerateBinaryFilePathData")]
            public async Task WillReturnWarningMessageForBinaryFile(string filePath)
            {
                // Arrange
                var package = new Package
                {
                    Version = "2.0.0-beta",
                    PackageFileSize = 3L * 1024 * 1024,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "B"
                    }
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersion("B", "2.0.0-beta", It.IsAny<bool>())).Returns(package);

                var file = new Mock<IPackageFile>();
                file.Setup(f => f.Path).Returns(filePath);
                file.Setup(f => f.GetStream()).Returns(Stream.Null);

                var packageStream = CreatePackageStream("B", "2.0.0-beta", new [] { file.Object });
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadPackageFileAsync(package)).Returns(Task.FromResult(packageStream));

                var controller = CreateController(packageService, packageFileService);

                // Act
                var result = await controller.ShowFileContent("B", "2.0.0-beta", filePath) as ContentResult;

                // Arrange
                Assert.NotNull(result);
                Assert.Equal("*** The requested file is a binary file. ***", result.Content);
                Assert.Equal(System.Text.Encoding.UTF8, result.ContentEncoding);
                Assert.Equal("text/plain", result.ContentType);
            }

            [Theory]
            [InlineData("content\\foo.txt", "file content")]
            [InlineData("tools\\bar.ps1", "PS scripts")]
            public async Task WillReturnFileContentForTextFile(string filePath, string fileContent)
            {
                // Arrange
                var package = new Package
                {
                    Version = "2.0.0-beta",
                    PackageFileSize = 3L * 1024 * 1024,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "B"
                    }
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersion("B", "2.0.0-beta", It.IsAny<bool>())).Returns(package);

                var file = new Mock<IPackageFile>();
                file.Setup(f => f.Path).Returns(filePath);
                file.Setup(f => f.GetStream()).Returns(fileContent.AsStream());

                var packageStream = CreatePackageStream("B", "2.0.0-beta", new[] { file.Object });
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadPackageFileAsync(package)).Returns(Task.FromResult(packageStream));

                var controller = CreateController(packageService, packageFileService);

                // Act
                var result = await controller.ShowFileContent("B", "2.0.0-beta", filePath) as ContentResult;

                // Arrange
                Assert.NotNull(result);
                Assert.Equal(fileContent, result.Content);
                Assert.Equal(System.Text.Encoding.UTF8, result.ContentEncoding);
                Assert.Equal("text/plain", result.ContentType);
            }

            public static IEnumerable<object[]> GenerateBinaryFilePathData
            {
                get
                {
                    return FileHelper.BinaryFileExtensions.Select(s => new string[] { "content\\foo" + s });
                }
            }
        }

        public class TheDownloadFileContentAction
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task WillReturnNotFoundIfFilePathIsNullOrEmpty(string filePath)
            {
                // Arrange
                var controller = CreateController();

                // Act
                ActionResult result = await controller.DownloadFileContent("B", "1.0.0-alpha", filePath);

                // Arrange
                Assert.True(result is HttpNotFoundResult);
            }

            [Fact]
            public async Task WillReturnNotFoundIfPackageIsNotFound()
            {
                // Arrange
                var controller = CreateController();

                // Act
                ActionResult result = await controller.DownloadFileContent("B", "1.0.0-alpha", "content\\foo.txt");

                // Arrange
                Assert.True(result is HttpNotFoundResult);
            }

            [Fact]
            public async Task WillReturnFileContentResult()
            {
                // Arrange
                var package = new Package
                {
                    Version = "2.0.0-beta",
                    PackageFileSize = 3L * 1024 * 1024,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "B"
                    }
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersion("B", "2.0.0-beta", It.IsAny<bool>())).Returns(package);

                var file = new Mock<IPackageFile>();
                file.Setup(f => f.Path).Returns("content\\foo.txt");
                file.Setup(f => f.GetStream()).Returns(Stream.Null);

                var packageStream = CreatePackageStream("B", "2.0.0-beta", new[] { file.Object });
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadPackageFileAsync(package)).Returns(Task.FromResult(packageStream));

                var controller = CreateController(packageService, packageFileService);

                // Act
                var result = await controller.DownloadFileContent("B", "2.0.0-beta", "content\\foo.txt") as FileResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal("application/octet-stream", result.ContentType);
                Assert.Equal("foo.txt", result.FileDownloadName);
            }
        }

        private static PackageFilesController CreateController(
            Mock<IPackageService> packageService = null,
            Mock<IPackageFileService> packageFileService = null,
            Mock<IPackageCacheService> cacheService = null)
        {
            packageService = packageService ?? new Mock<IPackageService>();
            packageFileService = packageFileService ?? new Mock<IPackageFileService>();

            if (cacheService == null) 
            {
                cacheService = new Mock<IPackageCacheService>();
                cacheService.Setup(c => c.GetBytes(It.IsAny<string>())).Returns((byte[])null);
            }

            return new PackageFilesController(packageService.Object, packageFileService.Object, cacheService.Object);
        }

        private static Stream CreatePackageStream(string id, string version, IEnumerable<IPackageFile> files = null)
        {
            var packageBuilder = new PackageBuilder
            {
                Id = id,
                Version = new SemanticVersion(version),
                Description = "description"
            };
            packageBuilder.Authors.Add("dotnetjunky");
            packageBuilder.Files.AddRange(files ?? new IPackageFile[0]);
            packageBuilder.DependencySets.Add(new PackageDependencySet(null, new NuGet.PackageDependency[] { new NuGet.PackageDependency("B") }));

            var stream = new MemoryStream();
            packageBuilder.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
    }
}

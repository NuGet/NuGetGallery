// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace NuGetGallery
{
    public class PackageRecommendationServiceFacts
    {
        private static PackageRecommendationService CreateService(
            Mock<IPackageService> packageService = null,
            Mock<IReportService> reportService = null)
        {
            packageService = packageService ?? new Mock<IPackageService>();
            reportService = reportService ?? new Mock<IReportService>();

            return new PackageRecommendationService(
                packageService.Object,
                reportService.Object);
        }

        private static Mock<IPackageService> CreatePackageService(IEnumerable<string> packageIds)
        {
            var packageService = new Mock<IPackageService>();
            packageService
                .Setup(ps => ps.FindAbsoluteLatestPackageById(
                    /* id */ It.IsIn(packageIds),
                    /* semVerLevelKey */ null))
                .Returns<string, int?>((id, _) => CreatePackage(id));
            return packageService;
        }

        private static Mock<IReportService> CreateReportService(
            params RecommendedPackages[] recommendationSets)
        {
            var targetIds = recommendationSets.Select(rp => rp.Id);
            var idMap = targetIds.ToDictionary(
                keySelector: id => id,
                elementSelector: id => PackageRecommendationService.GetReportName(id));
            var reportMap = recommendationSets.ToDictionary(
                keySelector: rp => rp.Id,
                elementSelector: rp => CreateReport(rp.Id, rp.Recommendations));

            Task<ReportBlob> GetReportByName(string reportName)
            {
                var targetId = idMap[reportName];
                return Task.FromResult(reportMap[targetId]);
            }

            var reportNames = idMap.Values;
            var reportService = new Mock<IReportService>();
            reportService
                .Setup(rs => rs.Load(It.IsIn<string>(reportNames)))
                .Returns<string>(GetReportByName);
            return reportService;
        }

        private static Package CreatePackage(string packageId)
        {
            var packageRegistration = new PackageRegistration { Id = packageId };
            return new Package { PackageRegistration = packageRegistration };
        }

        private static ReportBlob CreateReport(string id, IEnumerable<string> recommendations)
        {
            string json = JsonConvert.SerializeObject(new { id, recommendations });
            return new ReportBlob(json);
        }

        public class TheGetRecommendedPackagesMethod
        {
            [Fact]
            public async void WillReturnEmptyListIfReportIsNotFound()
            {
                var reportService = new Mock<IReportService>();
                reportService
                    .Setup(rs => rs.Load(It.IsAny<string>()))
                    .ThrowsAsync(new ReportNotFoundException());
                var recommendationService = CreateService(
                    reportService: reportService);

                var result = await recommendationService.GetRecommendedPackagesAsync(
                    CreatePackage("Newtonsoft.Json"),
                    currentUser: null);

                Assert.Empty(result);
            }

            [Fact]
            public async void WillReturnPackagesListedInReport()
            {
                string targetId = "Newtonsoft.Json";
                var recommendationIds = new[]
                {
                    "SammysJsonLibrary",
                    "ElliesJsonLibrary",
                    "JimmysJsonLibrary"
                };
                var allIds = new[] { targetId }.Concat(recommendationIds);

                var packageService = CreatePackageService(allIds);
                var reportService = CreateReportService(
                    new RecommendedPackages
                    {
                        Id = targetId,
                        Recommendations = recommendationIds
                    });

                var recommendationService = CreateService(
                    packageService: packageService,
                    reportService: reportService);

                var result = await recommendationService.GetRecommendedPackagesAsync(
                    CreatePackage(targetId),
                    currentUser: null);

                Assert.Equal(recommendationIds, result);
            }

            [Fact]
            public async void WillNotReturnPackagesNoLongerInDatabase()
            {
                string targetId = "Newtonsoft.Json";
                string excludedId = "SammysJsonLibrary";
                var recommendationIds = new[]
                {
                    "SammysJsonLibrary",
                    "ElliesJsonLibrary",
                    "JimmysJsonLibrary"
                };
                var registeredIds = new[] { targetId }.Concat(recommendationIds).Except(new[] { excludedId });

                var packageService = CreatePackageService(registeredIds);
                var reportService = CreateReportService(
                    new RecommendedPackages
                    {
                        Id = targetId,
                        Recommendations = recommendationIds
                    });

                var recommendationService = CreateService(
                    packageService: packageService,
                    reportService: reportService);

                var result = await recommendationService.GetRecommendedPackagesAsync(
                    CreatePackage(targetId),
                    currentUser: null);

                Assert.Equal(recommendationIds.Intersect(registeredIds), result);
            }

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
                fileStorageSvc.Setup(x => x.DeleteFileAsync(CoreConstants.PackagesFolderName, It.IsAny<string>()))
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

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingPackageRegistrationId()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = null };
                var package = new Package { PackageRegistration = packageRegistraion };

                var ex = Assert.Throws<ArgumentException>(() => service.CreateDownloadPackageActionResultAsync(new Uri("http://fake"), package).Wait());

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingVersion()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistraion, Version = null };

                var ex = Assert.Throws<ArgumentException>(() => service.CreateDownloadPackageActionResultAsync(new Uri("http://fake"), package).Wait());

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillGetAResultFromTheFileStorageServiceUsingThePackagesFolder()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);
                fileStorageSvc.Setup(x => x.CreateDownloadFileActionResultAsync(new Uri("http://fake"), CoreConstants.PackagesFolderName, It.IsAny<string>()))
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
                fileServiceMock.Verify(fs => fs.DeleteFileAsync(CoreConstants.PackageReadMesFolderName, $"active/test/1.0.0.md"), Times.Once);
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
                fileServiceMock.Verify(f => f.SaveFileAsync(CoreConstants.PackageReadMesFolderName, "active/foo/1.0.0.md", It.IsAny<Stream>(), true),
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

                    fileServiceMock.Verify(f => f.GetFileAsync(CoreConstants.PackageReadMesFolderName, $"active/foo/1.1.1.md"), Times.Once);
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

                fileServiceMock.Verify(f => f.GetFileAsync(CoreConstants.PackageReadMesFolderName, $"active/foo/1.1.1.md"), Times.Once);
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

        static MemoryStream CreatePackageFileStream()
        {
            return new MemoryStream(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }, 0, 8, true, true);
        }

        static PackageFileService CreateService(Mock<IFileStorageService> fileStorageSvc = null)
        {
            fileStorageSvc = fileStorageSvc ?? new Mock<IFileStorageService>();

            return new PackageFileService(
                fileStorageSvc.Object);
        }
    }
}

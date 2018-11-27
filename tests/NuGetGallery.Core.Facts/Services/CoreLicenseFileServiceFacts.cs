// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery.Services
{
    public class CoreLicenseFileServiceFacts
    {
        public class TheSaveLicenseFileAsyncMethod
        {
            [Fact]
            public async Task WillThrowIfPackageIsNull()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveLicenseFileAsync(null, Stream.Null));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfLicenseTypeIsAbsent()
            {
                var service = CreateService();
                var package = CreatePackage();
                package.EmbeddedLicenseType = EmbeddedLicenseFileType.Absent;
                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveLicenseFileAsync(package, Stream.Null));
                Assert.Equal("package", ex.ParamName);
                Assert.Contains("license", ex.Message);
            }

            [Fact]
            public async Task WillThrowIfStreamIsNull()
            {
                var service = CreateService();
                var package = CreatePackage();
                package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveLicenseFileAsync(package, null));
                Assert.Equal("licenseFile", ex.ParamName);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.PlainText)]
            [InlineData(EmbeddedLicenseFileType.Markdown)]
            public async Task WillThrowIfPackageIsMissingPackageRegistration(EmbeddedLicenseFileType licenseFileType)
            {
                var service = CreateService();
                var package = new Package { PackageRegistration = null, EmbeddedLicenseType = licenseFileType };

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveLicenseFileAsync(package, Stream.Null));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.PlainText)]
            [InlineData(EmbeddedLicenseFileType.Markdown)]
            public async Task WillThrowIfPackageIsMissingPackageRegistrationId(EmbeddedLicenseFileType licenseFileType)
            {
                var service = CreateService();
                var packageRegistration = new PackageRegistration { Id = null };
                var package = new Package { PackageRegistration = packageRegistration, EmbeddedLicenseType = licenseFileType };

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveLicenseFileAsync(package, Stream.Null));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.PlainText)]
            [InlineData(EmbeddedLicenseFileType.Markdown)]
            public async Task WillThrowIfPackageIsMissingNormalizedVersionAndVersion(EmbeddedLicenseFileType licenseFileType)
            {
                var service = CreateService();
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistration, NormalizedVersion = null, Version = null, EmbeddedLicenseType = licenseFileType };

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveLicenseFileAsync(package, Stream.Null));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.PlainText, "text/plain")]
            [InlineData(EmbeddedLicenseFileType.Markdown, "text/markdown")]
            public async Task WillUseNormalizedRegularVersionIfNormalizedVersionMissing(EmbeddedLicenseFileType licenseFileType, string expectedContentType)
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistration, NormalizedVersion = null, Version = "01.01.01", EmbeddedLicenseType = licenseFileType };

                fileStorageSvc.Setup(x => x.SaveFileAsync(CoreConstants.Folders.PackagesContentFolderName, BuildLicenseFileName("theId", "1.1.1"), expectedContentType, It.IsAny<Stream>(), true))
                    .Completes()
                    .Verifiable();

                await service.SaveLicenseFileAsync(package, Stream.Null);

                fileStorageSvc.VerifyAll();
            }
        }

        public class ExtractAndSaveLicenseFileAsync
        {
            private static MemoryStream GeneratePackageAsync(string licenseFileName = null)
            {
                return PackageServiceUtility.CreateNuGetPackageStream();
            }
        }

        public class TheDownloadLicenseFileAsyncMethod
        {
            [Fact]
            public async Task WillThrowIfPackageIsNull()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.DownloadLicenseFileAsync(null));

                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.PlainText)]
            [InlineData(EmbeddedLicenseFileType.Markdown)]
            public async Task WillThrowIfPackageIsMissingPackageRegistration(EmbeddedLicenseFileType licenseFileType)
            {
                var service = CreateService();
                var package = new Package { PackageRegistration = null, EmbeddedLicenseType = licenseFileType };

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.DownloadLicenseFileAsync(package));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.PlainText)]
            [InlineData(EmbeddedLicenseFileType.Markdown)]
            public async Task WillThrowIfPackageIsMissingPackageRegistrationId(EmbeddedLicenseFileType licenseFileType)
            {
                var service = CreateService();
                var packageRegistration = new PackageRegistration { Id = null };
                var package = new Package { PackageRegistration = packageRegistration, EmbeddedLicenseType = licenseFileType };

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.DownloadLicenseFileAsync(package));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.PlainText)]
            [InlineData(EmbeddedLicenseFileType.Markdown)]
            public async Task WillThrowIfPackageIsMissingNormalizedVersionAndVersion(EmbeddedLicenseFileType licenseFileType)
            {
                var service = CreateService();
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistration, NormalizedVersion = null, Version = null, EmbeddedLicenseType = licenseFileType };

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => service.DownloadLicenseFileAsync(package));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.PlainText)]
            [InlineData(EmbeddedLicenseFileType.Markdown)]
            public async Task WillUseNormalizedRegularVersionIfNormalizedVersionMissing(EmbeddedLicenseFileType licenseFileType)
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistration, NormalizedVersion = null, Version = "01.01.01", EmbeddedLicenseType = licenseFileType };

                await service.DownloadLicenseFileAsync(package);

                fileStorageSvc
                    .Verify(fss => fss.GetFileAsync(CoreConstants.Folders.PackagesContentFolderName, BuildLicenseFileName("theId", "1.1.1")),
                        Times.Once);
                fileStorageSvc
                    .Verify(fss => fss.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                        Times.Once);
            }
        }

        public class TheDeleteLicenseFileAsyncMethod
        {
            [Fact]
            public async Task WillThrowIfIdIsNull()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.DeleteLicenseFileAsync(null, "1.2.3"));
                Assert.Equal("id", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfIdIsEmpty()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteLicenseFileAsync("", "1.2.3"));
                Assert.Equal("id", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfVersionIsNull()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.DeleteLicenseFileAsync("theId", null));
                Assert.Equal("version", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfVersionIsEmpty()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteLicenseFileAsync("theId", ""));
                Assert.Equal("version", ex.ParamName);
            }

            [Fact]
            public async Task WillNormalizeVersion()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);

                await service.DeleteLicenseFileAsync("theId", "01.02.03");

                fileStorageSvc
                    .Verify(fss => fss.DeleteFileAsync(CoreConstants.Folders.PackagesContentFolderName, BuildLicenseFileName("theId", "1.2.3")), Times.Once);
                fileStorageSvc
                    .Verify(fss => fss.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            }
        }

        static Package CreatePackage()
        {
            var packageRegistration = new PackageRegistration { Id = "theId", Packages = new HashSet<Package>() };
            var package = new Package { Version = "theVersion", NormalizedVersion = "theNormalizedVersion", PackageRegistration = packageRegistration };
            packageRegistration.Packages.Add(package);
            return package;
        }

        static CoreLicenseFileService CreateService(Mock<ICoreFileStorageService> fileStorageService = null)
        {
            fileStorageService = fileStorageService ?? new Mock<ICoreFileStorageService>();

            return new CoreLicenseFileService(
                fileStorageService.Object, new PackageFileMetadataService());
        }

        private static string BuildLicenseFileName(string id, string version)
        {
            return string.Format(CoreConstants.PackageContentFileSavePathTemplate + "/license", id.ToLowerInvariant(), version.ToLowerInvariant());
        }
    }
}

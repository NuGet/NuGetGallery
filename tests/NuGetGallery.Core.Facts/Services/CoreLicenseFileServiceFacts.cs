// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
            [InlineData(EmbeddedLicenseFileType.Markdown, "text/plain")]
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
            [Fact]
            public async Task ThrowsWhenPackageIsNull()
            {
                var service = CreateService();
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.ExtractAndSaveLicenseFileAsync(
                    package: null,
                    packageStream: Mock.Of<Stream>()));

                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task ThrowsWhenPackageStreamIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.ExtractAndSaveLicenseFileAsync(
                    package: Mock.Of<Package>(),
                    packageStream: null));

                Assert.Equal("packageStream", ex.ParamName);
            }

            [Theory]
            [InlineData(null, null)]
            [InlineData(" ", null)]
            [InlineData(null, "MIT")] // should also throw for license expression
            public async Task ThrowsWhenNoLicenseFileSpecified(string licenseFileName, string licenseExpression)
            {
                var service = CreateService();
                var packageStream = GeneratePackageAsync(licenseFileName, licenseExpression);
                var package = PackageServiceUtility.CreateTestPackage();
                package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText; // tested method should ignore the package settings and check .nuspec

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExtractAndSaveLicenseFileAsync(package, packageStream));

                Assert.Contains("No license file", ex.Message);
            }

            [Fact]
            public async Task ThrowsOnMissingLicenseFile()
            {
                var service = CreateService();
                const string LicenseFileName = "license.txt";
                var packageStream = GeneratePackageAsync(LicenseFileName, null, false);
                var package = PackageServiceUtility.CreateTestPackage();

                var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => service.ExtractAndSaveLicenseFileAsync(package, packageStream));
                Assert.Contains(LicenseFileName, ex.Message); // current implementation of the client does not properly set the FileName property
            }

            [Theory]
            [InlineData("license.txt")]
            [InlineData("foo\\bar.txt")]
            [InlineData("foo/bar.txt")]
            public async Task SavesLicenseFile(string licenseFilenName)
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageSvc);
                var packageStream = GeneratePackageAsync(licenseFilenName);
                var package = PackageServiceUtility.CreateTestPackage();
                package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
                var savedLicenseBytes = new byte[LicenseFileContents.Length];
                var expectedFileName = BuildLicenseFileName(package.Id, package.Version);

                fileStorageSvc.Setup(x => x.SaveFileAsync(
                        CoreConstants.Folders.PackagesContentFolderName,
                        expectedFileName,
                        "text/plain",
                        It.IsAny<Stream>(),
                        true))
                    .Completes()
                    .Callback<string, string, string, Stream, bool>((_, __, ___, s, ____) => s.Read(savedLicenseBytes, 0, savedLicenseBytes.Length))
                    .Verifiable();

                await service.ExtractAndSaveLicenseFileAsync(package, packageStream);

                fileStorageSvc
                    .VerifyAll();
                Assert.Equal(LicenseFileContents, savedLicenseBytes);
            }

            private static byte[] LicenseFileContents => Encoding.UTF8.GetBytes("Sample license text");

            private static MemoryStream GeneratePackageAsync(string licenseFileName = null, string licenseExpression = null, bool saveLicenseFile = true)
            {
                return PackageServiceUtility.CreateNuGetPackageStream(
                    licenseExpression: licenseExpression,
                    licenseFilename: licenseFileName,
                    licenseFileContents: licenseFileName != null && saveLicenseFile ? LicenseFileContents : null);
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
            var contentFileMetadataService = new Mock<IContentFileMetadataService>();
            contentFileMetadataService
                .SetupGet(c => c.PackageContentFolderName)
                .Returns(CoreConstants.Folders.PackagesContentFolderName);

            contentFileMetadataService
                .SetupGet(c => c.PackageContentPathTemplate)
                .Returns(CoreConstants.PackageContentFileSavePathTemplate);

            return new CoreLicenseFileService(
                fileStorageService.Object, contentFileMetadataService.Object);
        }

        private static string BuildLicenseFileName(string id, string version)
        {
            return string.Format(CoreConstants.PackageContentFileSavePathTemplate + "/license", id.ToLowerInvariant(), version.ToLowerInvariant());
        }
    }
}

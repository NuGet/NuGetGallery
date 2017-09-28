// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class CorePackageFileServiceFacts
    {
        private const string ValidationFolderName = "validation";
        private const string Id = "NuGet.Versioning";
        private const string Version = "4.3.0.0-BETA+1";
        private const string NormalizedVersion = "4.3.0-BETA";
        private const string LowercaseId = "nuget.versioning";
        private const string LowercaseVersion = "4.3.0-beta";
        private static readonly string ValidationFileName = $"{LowercaseId}.{LowercaseVersion}.nupkg";

        public class TheSavePackageFileMethod
        {
            [Fact]
            public void WillThrowIfPackageIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.SavePackageFileAsync(null, Stream.Null).Wait());

                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageFileIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.SavePackageFileAsync(new Package(), null).Wait());

                Assert.Equal("packageFile", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingPackageRegistration()
            {
                var service = CreateService();
                var package = new Package { PackageRegistration = null };

                var ex = Assert.Throws<ArgumentException>(() => service.SavePackageFileAsync(package, CreatePackageFileStream()).Wait());

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingPackageRegistrationId()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = null };
                var package = new Package { PackageRegistration = packageRegistraion };

                var ex = Assert.Throws<ArgumentException>(() => service.SavePackageFileAsync(package, CreatePackageFileStream()).Wait());

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingNormalizedVersionAndVersion()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistraion, NormalizedVersion = null, Version = null };

                var ex = Assert.Throws<ArgumentException>(() => service.SavePackageFileAsync(package, CreatePackageFileStream()).Wait());

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillUseNormalizedRegularVersionIfNormalizedVersionMissing()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);
                var packageRegistraion = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistraion, NormalizedVersion = null, Version = "01.01.01" };

                fileStorageSvc.Setup(x => x.SaveFileAsync(It.IsAny<string>(), BuildFileName("theId", "1.1.1", CoreConstants.NuGetPackageFileExtension, CoreConstants.PackageFileSavePathTemplate), It.IsAny<Stream>(), It.Is<bool>(b => !b)))
                    .Completes()
                    .Verifiable();

                await service.SavePackageFileAsync(package, CreatePackageFileStream());

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillSaveTheFileViaTheFileStorageServiceUsingThePackagesFolder()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);
                fileStorageSvc.Setup(x => x.SaveFileAsync(CoreConstants.PackagesFolderName, It.IsAny<string>(), It.IsAny<Stream>(), It.Is<bool>(b => !b)))
                    .Completes()
                    .Verifiable();

                await service.SavePackageFileAsync(CreatePackage(), CreatePackageFileStream());

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillSaveTheFileViaTheFileStorageServiceUsingAFileNameWithIdAndNormalizedersion()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);
                fileStorageSvc.Setup(x => x.SaveFileAsync(It.IsAny<string>(), BuildFileName("theId", "theNormalizedVersion", CoreConstants.NuGetPackageFileExtension, CoreConstants.PackageFileSavePathTemplate), It.IsAny<Stream>(), It.Is<bool>(b => !b)))
                    .Completes()
                    .Verifiable();

                await service.SavePackageFileAsync(CreatePackage(), CreatePackageFileStream());

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillSaveTheFileStreamViaTheFileStorageService()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var fakeStream = new MemoryStream();
                var service = CreateService(fileStorageService: fileStorageSvc);
                fileStorageSvc.Setup(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), fakeStream, It.Is<bool>(b => !b)))
                    .Completes()
                    .Verifiable();

                await service.SavePackageFileAsync(CreatePackage(), fakeStream);

                fileStorageSvc.VerifyAll();
            }
        }
        
        public class TheSaveValidationPackageFileMethod : FactsBase
        {
            [Fact]
            public async Task WillThrowIfPackageIsNull()
            {
                _package = null;

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => _service.SaveValidationPackageFileAsync(_package, _packageFile));

                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfPackageFileIsNull()
            {
                _packageFile = null;

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => _service.SaveValidationPackageFileAsync(_package, _packageFile));

                Assert.Equal("packageFile", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfPackageIsMissingPackageRegistration()
            {
                _package.PackageRegistration = null;

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => _service.SaveValidationPackageFileAsync(_package, _packageFile));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfPackageIsMissingPackageRegistrationId()
            {
                _package.PackageRegistration.Id = null;

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => _service.SaveValidationPackageFileAsync(_package, _packageFile));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfPackageIsMissingNormalizedVersionAndVersion()
            {
                _package.Version = null;
                _package.NormalizedVersion = null;

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => _service.SaveValidationPackageFileAsync(_package, _packageFile));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillUseNormalizedRegularVersionIfNormalizedVersionMissing()
            {
                _package.NormalizedVersion = null;

                await _service.SaveValidationPackageFileAsync(_package, _packageFile);

                _fileStorageService.Verify(
                    x => x.SaveFileAsync(ValidationFolderName, ValidationFileName, _packageFile, false),
                    Times.Once);
                _fileStorageService.Verify(
                    x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()),
                    Times.Once);
            }

            [Fact]
            public async Task WillSaveTheFileViaTheFileStorageService()
            {
                await _service.SaveValidationPackageFileAsync(_package, _packageFile);

                _fileStorageService.Verify(
                    x => x.SaveFileAsync(ValidationFolderName, ValidationFileName, _packageFile, false),
                    Times.Once);
                _fileStorageService.Verify(
                    x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()),
                    Times.Once);
            }
        }

        public class TheDownloadValidationPackageFileMethod : FactsBase
        {
            [Fact]
            public async Task WillThrowIfPackageIsNull()
            {
                _package = null;

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => _service.DownloadValidationPackageFileAsync(_package));

                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfPackageIsMissingPackageRegistration()
            {
                _package.PackageRegistration = null;

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => _service.DownloadValidationPackageFileAsync(_package));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfPackageIsMissingPackageRegistrationId()
            {
                _package.PackageRegistration.Id = null;

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => _service.DownloadValidationPackageFileAsync(_package));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfPackageIsMissingNormalizedVersionAndVersion()
            {
                _package.Version = null;
                _package.NormalizedVersion = null;

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => _service.DownloadValidationPackageFileAsync(_package));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillUseNormalizedRegularVersionIfNormalizedVersionMissing()
            {
                _package.NormalizedVersion = null;

                await _service.DownloadValidationPackageFileAsync(_package);

                _fileStorageService.Verify(
                    x => x.GetFileAsync(ValidationFolderName, ValidationFileName),
                    Times.Once);
                _fileStorageService.Verify(
                    x => x.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
            }

            [Fact]
            public async Task WillDownloadTheFileViaTheFileStorageService()
            {
                await _service.DownloadValidationPackageFileAsync(_package);

                _fileStorageService.Verify(
                    x => x.GetFileAsync(ValidationFolderName, ValidationFileName),
                    Times.Once);
                _fileStorageService.Verify(
                    x => x.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
            }
        }

        public class TheDeletePackageFileMethod : FactsBase
        {
            [Fact]
            public async Task WillThrowIfIdIsNull()
            {
                string id = null;

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => _service.DeleteValidationPackageFileAsync(id, Version));

                Assert.Equal("id", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfVersionIsNull()
            {
                string version = null;

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => _service.DeleteValidationPackageFileAsync(Id, version));

                Assert.Equal("version", ex.ParamName);
            }

            [Fact]
            public async Task WillDeleteTheFileViaTheFileStorageService()
            {
                await _service.DeleteValidationPackageFileAsync(Id, Version);

                _fileStorageService.Verify(
                    x => x.DeleteFileAsync(ValidationFolderName, ValidationFileName),
                    Times.Once);
                _fileStorageService.Verify(
                    x => x.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
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

        static CorePackageFileService CreateService(Mock<ICoreFileStorageService> fileStorageService = null)
        {
            fileStorageService = fileStorageService ?? new Mock<ICoreFileStorageService>();

            return new CorePackageFileService(
                fileStorageService.Object);
        }

        public abstract class FactsBase
        {
            protected readonly Mock<ICoreFileStorageService> _fileStorageService;
            protected readonly CorePackageFileService _service;
            protected Package _package;
            protected Stream _packageFile;

            public FactsBase()
            {
                _fileStorageService = new Mock<ICoreFileStorageService>();
                _service = CreateService(fileStorageService: _fileStorageService);
                _package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = Id,
                    },
                    Version = Version,
                    NormalizedVersion = NormalizedVersion,
                };
                _packageFile = Stream.Null;
            }
        }
    }
}

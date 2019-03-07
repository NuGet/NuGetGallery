// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class CorePackageFileServiceFacts
    {
        private const string ValidationFolderName = "validation";
        private const string PackagesFolderName = "packages";
        private const string Id = "NuGet.Versioning";
        private const string Version = "4.3.0.0-BETA+1";
        private const string NormalizedVersion = "4.3.0-BETA";
        private const string LowercaseId = "nuget.versioning";
        private const string LowercaseVersion = "4.3.0-beta";
        private static readonly string ValidationFileName = $"{LowercaseId}.{LowercaseVersion}.nupkg";
        private const string PackageContent = "Hello, world.";
        private const string PackageHash = "rQw3wx1psxXzqB8TyM3nAQlK2RcluhsNwxmcqXE2YbgoDW735o8TPmIR4uWpoxUERddvFwjgRSGw7gNPCwuvJg==";

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

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingPackageRegistrationId()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = null };
                var package = new Package { PackageRegistration = packageRegistraion };

                var ex = Assert.Throws<ArgumentException>(() => service.SavePackageFileAsync(package, CreatePackageFileStream()).Wait());

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingNormalizedVersionAndVersion()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistraion, NormalizedVersion = null, Version = null };

                var ex = Assert.Throws<ArgumentException>(() => service.SavePackageFileAsync(package, CreatePackageFileStream()).Wait());

                Assert.StartsWith("The package is missing required data.", ex.Message);
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
                fileStorageSvc.Setup(x => x.SaveFileAsync(CoreConstants.Folders.PackagesFolderName, It.IsAny<string>(), It.IsAny<Stream>(), It.Is<bool>(b => !b)))
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

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WillSaveTheFileStreamViaTheFileStorageServiceAndOverwriteIfneeded(bool overwrite)
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var fakeStream = new MemoryStream();
                var service = CreateService(fileStorageService: fileStorageSvc);
                fileStorageSvc.Setup(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), fakeStream, overwrite))
                    .Completes()
                    .Verifiable();

                await service.SavePackageFileAsync(CreatePackage(), fakeStream, overwrite);

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

        public class TheDeleteValidationPackageFileMethod : FactsBase
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

        public class TheDeletePackageFileMethod : FactsBase
        {
            [Fact]
            public async Task WillThrowIfIdIsNull()
            {
                string id = null;

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => _service.DeletePackageFileAsync(id, Version));

                Assert.Equal("id", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfVersionIsNull()
            {
                string version = null;

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => _service.DeletePackageFileAsync(Id, version));

                Assert.Equal("version", ex.ParamName);
            }

            [Fact]
            public async Task WillDeleteTheFileViaTheFileStorageService()
            {
                await _service.DeletePackageFileAsync(Id, Version);

                _fileStorageService.Verify(
                    x => x.DeleteFileAsync(PackagesFolderName, ValidationFileName),
                    Times.Once);
                _fileStorageService.Verify(
                    x => x.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
            }
        }

        public class TheGetValidationPackageReadUriAsyncMethod : FactsBase
        {
            [Fact]
            public async Task WillThrowIfPackageIsNull()
            {
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _service.GetValidationPackageReadUriAsync(null, DateTimeOffset.UtcNow.AddHours(3)));

                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillUseTheFileStorageService()
            {
                DateTimeOffset endOfAccess = DateTimeOffset.UtcNow.AddHours(3);
                await _service.GetValidationPackageReadUriAsync(_package, endOfAccess);

                _fileStorageService.Verify(
                    x => x.GetFileReadUriAsync(ValidationFolderName, ValidationFileName, endOfAccess),
                    Times.Once());
                _fileStorageService.Verify(
                    x => x.GetFileReadUriAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
                    Times.Once());
            }
        }

        public class TheGetPackageReadUriMethod : FactsBase
        {
            [Fact]
            public async Task WillThrowIfPackageIsNull()
            {
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _service.GetPackageReadUriAsync(null));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillUseFileStorageService()
            {
                await _service.GetPackageReadUriAsync(_package);

                string filename = BuildFileName(_package.PackageRegistration.Id, _package.NormalizedVersion, CoreConstants.NuGetPackageFileExtension, CoreConstants.PackageFileSavePathTemplate);

                _fileStorageService.Verify(
                    x => x.GetFileReadUriAsync(PackagesFolderName, filename, null),
                    Times.Once());
                _fileStorageService.Verify(
                    x => x.GetFileReadUriAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>()),
                    Times.Once());
            }
        }

        public class TheDoesPackageFileExistMethod : FactsBase
        {
            [Fact]
            public async Task WillThrowIfPackageIsNull()
            {
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _service.DoesPackageFileExistAsync(null));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillUseFileStorageService()
            {
                string filename = BuildFileName(_package.PackageRegistration.Id, _package.NormalizedVersion, CoreConstants.NuGetPackageFileExtension, CoreConstants.PackageFileSavePathTemplate);

                _fileStorageService
                    .Setup(x => x.FileExistsAsync(PackagesFolderName, filename))
                    .ReturnsAsync(true);

                var result = await _service.DoesPackageFileExistAsync(_package);

                Assert.True(result);
                _fileStorageService.Verify(
                    x => x.FileExistsAsync(PackagesFolderName, filename),
                    Times.Once());
                _fileStorageService.Verify(
                    x => x.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once());
            }
        }

        public class TheDoesValidationPackageFileExistMethod : FactsBase
        {
            [Fact]
            public async Task WillThrowIfPackageIsNull()
            {
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _service.DoesValidationPackageFileExistAsync(null));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillUseFileStorageService()
            {
                _fileStorageService
                    .Setup(x => x.FileExistsAsync(ValidationFolderName, ValidationFileName))
                    .ReturnsAsync(true);

                var result = await _service.DoesValidationPackageFileExistAsync(_package);

                Assert.True(result);
                _fileStorageService.Verify(
                    x => x.FileExistsAsync(ValidationFolderName, ValidationFileName),
                    Times.Once());
                _fileStorageService.Verify(
                    x => x.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once());
            }
        }

        public class TheStorePackageFileInBackupLocationAsyncMethod
        {
            [Fact]
            public async Task WillThrowIfPackageIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.StorePackageFileInBackupLocationAsync(null, Stream.Null));

                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfPackageFileIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.StorePackageFileInBackupLocationAsync(new Package { PackageRegistration = new PackageRegistration() }, null));

                Assert.Equal("packageFile", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfPackageIsMissingPackageRegistration()
            {
                var service = CreateService();
                var package = new Package { PackageRegistration = null };

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.StorePackageFileInBackupLocationAsync(package, CreatePackageFileStream()));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfPackageIsMissingPackageRegistrationId()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = null };
                var package = new Package { PackageRegistration = packageRegistraion };

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.StorePackageFileInBackupLocationAsync(package, CreatePackageFileStream()));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfPackageIsMissingNormalizedVersionAndVersion()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistraion, NormalizedVersion = null, Version = null };

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.StorePackageFileInBackupLocationAsync(package, CreatePackageFileStream()));

                Assert.StartsWith("The package is missing required data.", ex.Message);
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task WillUseNormalizedRegularVersionIfNormalizedVersionMissing()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);
                var packageRegistraion = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistraion, NormalizedVersion = null, Version = "01.01.01" };

                fileStorageSvc.Setup(x => x.SaveFileAsync(It.IsAny<string>(), BuildBackupFileName("theId", "1.1.1", PackageHash), It.IsAny<Stream>(), It.Is<bool>(b => b)))
                    .Completes()
                    .Verifiable();

                await service.StorePackageFileInBackupLocationAsync(package, CreatePackageFileStream());

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillSeekTheStreamBeforeAndAfterHashing()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);
                string path = null;
                long position = -1;
                fileStorageSvc
                    .Setup(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult(true))
                    .Callback<string, string, Stream, bool>((_, p, s, ___) =>
                    {
                        path = p;
                        position = s.Position;
                    });
                
                var package = CreatePackage();
                package.PackageRegistration.Id = Id;
                package.NormalizedVersion = NormalizedVersion;
                var stream = CreatePackageFileStream();
                stream.Seek(0, SeekOrigin.End);

                await service.StorePackageFileInBackupLocationAsync(package, stream);

                Assert.Equal($"nuget.versioning/4.3.0-beta/{HttpServerUtility.UrlTokenEncode(Convert.FromBase64String(PackageHash))}..nupkg", path);
                Assert.Equal(0, position);
            }

            [Fact]
            public async Task WillUseLowercaseNormalizedIdAndVersionAndStreamHash()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);
                string path = null;
                fileStorageSvc
                    .Setup(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult(true))
                    .Callback<string, string, Stream, bool>((_, p, __, ___) => path = p);

                var package = CreatePackage();
                package.PackageRegistration.Id = Id;
                package.NormalizedVersion = NormalizedVersion;
                package.Hash = "NzMzMS1QNENLNEczSDQ1SA=="; // This hash should not be used.

                await service.StorePackageFileInBackupLocationAsync(package, CreatePackageFileStream());

                Assert.Equal($"nuget.versioning/4.3.0-beta/{HttpServerUtility.UrlTokenEncode(Convert.FromBase64String(PackageHash))}..nupkg", path);
            }

            [Fact]
            public async Task WillSaveTheFileViaTheFileStorageServiceUsingThePackagesFolder()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);
                fileStorageSvc.Setup(x => x.SaveFileAsync(CoreConstants.Folders.PackageBackupsFolderName, It.IsAny<string>(), It.IsAny<Stream>(), It.Is<bool>(b => b)))
                    .Completes()
                    .Verifiable();

                var package = CreatePackage();

                await service.StorePackageFileInBackupLocationAsync(package, CreatePackageFileStream());

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillSaveTheFileViaTheFileStorageServiceUsingAFileNameWithIdAndNormalizedersion()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var service = CreateService(fileStorageService: fileStorageSvc);
                fileStorageSvc.Setup(x => x.SaveFileAsync(It.IsAny<string>(), BuildBackupFileName("theId", "theNormalizedVersion", PackageHash), It.IsAny<Stream>(), It.Is<bool>(b => b)))
                    .Completes()
                    .Verifiable();

                var package = CreatePackage();

                await service.StorePackageFileInBackupLocationAsync(package, CreatePackageFileStream());

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillSaveTheFileStreamViaTheFileStorageService()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var fakeStream = new MemoryStream();
                var service = CreateService(fileStorageService: fileStorageSvc);
                fileStorageSvc.Setup(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), fakeStream, It.Is<bool>(b => b)))
                    .Completes()
                    .Verifiable();

                var package = CreatePackage();

                await service.StorePackageFileInBackupLocationAsync(package, fakeStream);

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillNotUploadThePackageIfItAlreadyExists()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var fakeStream = new MemoryStream();
                var service = CreateService(fileStorageService: fileStorageSvc);
                fileStorageSvc
                    .Setup(x => x.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(true);

                var package = CreatePackage();

                await service.StorePackageFileInBackupLocationAsync(package, fakeStream);

                fileStorageSvc.Verify(
                    x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()),
                    Times.Never);
            }

            [Fact]
            public async Task WillSwallowFileAlreadyExistsException()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var fakeStream = new MemoryStream();
                var service = CreateService(fileStorageService: fileStorageSvc);
                fileStorageSvc
                    .Setup(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                    .Throws(new FileAlreadyExistsException("File already exists."));

                var package = CreatePackage();

                await service.StorePackageFileInBackupLocationAsync(package, fakeStream);

                fileStorageSvc.Verify(
                    x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()),
                    Times.Once);
            }

            [Fact]
            public async Task WillNotSwallowOtherExceptions()
            {
                var fileStorageSvc = new Mock<ICoreFileStorageService>();
                var fakeStream = new MemoryStream();
                var service = CreateService(fileStorageService: fileStorageSvc);
                var exception = new ArgumentException("Bad!");
                fileStorageSvc
                    .Setup(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                    .Throws(exception);

                var package = CreatePackage();

                var actual = await Assert.ThrowsAsync<ArgumentException>(
                    () => service.StorePackageFileInBackupLocationAsync(package, fakeStream));
                Assert.Same(exception, actual);
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

        private static string BuildBackupFileName(string id, string version, string hash)
        {
            var hashBytes = Convert.FromBase64String(hash);

            return string.Format(
                CoreConstants.PackageFileBackupSavePathTemplate,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                HttpServerUtility.UrlTokenEncode(hashBytes),
                CoreConstants.NuGetPackageFileExtension);
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
            return new MemoryStream(Encoding.ASCII.GetBytes(PackageContent));
        }

        static CorePackageFileService CreateService(Mock<ICoreFileStorageService> fileStorageService = null)
        {
            fileStorageService = fileStorageService ?? new Mock<ICoreFileStorageService>();

            return new CorePackageFileService(
                fileStorageService.Object, new PackageFileMetadataService());
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

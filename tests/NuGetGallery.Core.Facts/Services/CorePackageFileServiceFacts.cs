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
                var service = CreateService(fileStorageSvc: fileStorageSvc);
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
                var service = CreateService(fileStorageSvc: fileStorageSvc);
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
                var service = CreateService(fileStorageSvc: fileStorageSvc);
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
                var service = CreateService(fileStorageSvc: fileStorageSvc);
                fileStorageSvc.Setup(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), fakeStream, It.Is<bool>(b => !b)))
                    .Completes()
                    .Verifiable();

                await service.SavePackageFileAsync(CreatePackage(), fakeStream);

                fileStorageSvc.VerifyAll();
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

        static CorePackageFileService CreateService(Mock<ICoreFileStorageService> fileStorageSvc = null)
        {
            fileStorageSvc = fileStorageSvc ?? new Mock<ICoreFileStorageService>();

            return new CorePackageFileService(
                fileStorageSvc.Object);
        }
    }
}

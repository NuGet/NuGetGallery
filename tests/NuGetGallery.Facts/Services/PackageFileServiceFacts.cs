// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Configuration;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class PackageFileServiceFacts
    {
        public class TheDeletePackageFileMethod
        {
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
                fileStorageSvc.Setup(x => x.DeleteFileAsync(Constants.PackagesFolderName, It.IsAny<string>()))
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
                fileStorageSvc.Setup(x => x.DeleteFileAsync(It.IsAny<string>(), BuildFileName("theId", "theVersion")))
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
                fileStorageSvc.Setup(x => x.CreateDownloadFileActionResultAsync(new Uri("http://fake"), Constants.PackagesFolderName, It.IsAny<string>()))
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
                fileStorageSvc.Setup(x => x.CreateDownloadFileActionResultAsync(new Uri("http://fake"), It.IsAny<string>(), BuildFileName("theId", "theNormalizedVersion")))
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
                fileStorageSvc.Setup(x => x.CreateDownloadFileActionResultAsync(new Uri("http://fake"), It.IsAny<string>(), BuildFileName("theId", "1.1.1")))
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
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);
                var packageRegistraion = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistraion, NormalizedVersion = null, Version = "01.01.01" };
                fileStorageSvc.Setup(x => x.SaveFileAsync(It.IsAny<string>(), BuildFileName("theId", "1.1.1"), It.IsAny<Stream>()))
                    .Completes()
                    .Verifiable();

                await service.SavePackageFileAsync(package, CreatePackageFileStream());

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillSaveTheFileViaTheFileStorageServiceUsingThePackagesFolder()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);
                fileStorageSvc.Setup(x => x.SaveFileAsync(Constants.PackagesFolderName, It.IsAny<string>(), It.IsAny<Stream>()))
                    .Completes()
                    .Verifiable();

                await service.SavePackageFileAsync(CreatePackage(), CreatePackageFileStream());

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillSaveTheFileViaTheFileStorageServiceUsingAFileNameWithIdAndNormalizedersion()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);
                fileStorageSvc.Setup(x => x.SaveFileAsync(It.IsAny<string>(), BuildFileName("theId", "theNormalizedVersion"), It.IsAny<Stream>()))
                    .Completes()
                    .Verifiable();

                await service.SavePackageFileAsync(CreatePackage(), CreatePackageFileStream());

                fileStorageSvc.VerifyAll();
            }

            [Fact]
            public async Task WillSaveTheFileStreamViaTheFileStorageService()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var fakeStream = new MemoryStream();
                var service = CreateService(fileStorageSvc: fileStorageSvc);
                fileStorageSvc.Setup(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), fakeStream))
                    .Completes()
                    .Verifiable();

                await service.SavePackageFileAsync(CreatePackage(), fakeStream);

                fileStorageSvc.VerifyAll();
            }
        }

        static string BuildFileName(
            string id,
            string version)
        {
            return string.Format(
                Constants.PackageFileSavePathTemplate, 
                id.ToLowerInvariant(), 
                SemanticVersionExtensions.Normalize(version).ToLowerInvariant(), // No matter what ends up getting passed in, the version should be normalized
                Constants.NuGetPackageFileExtension);
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

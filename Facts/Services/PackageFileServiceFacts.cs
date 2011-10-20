using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Mvc;
using Moq;
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

                var ex = Assert.Throws<ArgumentNullException>(() => service.DeletePackageFile(id, "theVersion"));

                Assert.Equal("id", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfVersionIsNullOrEmpty(string version)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.DeletePackageFile("theId", version));

                Assert.Equal("version", ex.ParamName);
            }

            [Fact]
            public void WillDeleteTheFileViaTheFileStorageServiceUsingThePackagesFolder()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);

                service.DeletePackageFile("theId", "theVersion");

                fileStorageSvc.Verify(x => x.DeleteFile(Const.PackagesFolderName, It.IsAny<string>()));
            }

            [Fact]
            public void WillDeleteTheFileViaTheFileStorageServiceUsingAFileNameWithIdAndVersion()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);

                service.DeletePackageFile("theId", "theVersion");

                fileStorageSvc.Verify(x => x.DeleteFile(It.IsAny<string>(), BuildFileName("theId", "theVersion")));
            }
        }
        
        public class TheCreateDownloadPackageActionResultMethod
        {
            [Fact]
            public void WillThrowIfPackageIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.CreateDownloadPackageActionResult(null));

                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingPackageRegistration()
            {
                var service = CreateService();
                var package = new Package { PackageRegistration = null };

                var ex = Assert.Throws<ArgumentException>(() => service.CreateDownloadPackageActionResult(package));

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingPackageRegistrationId()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = null };
                var package = new Package { PackageRegistration = packageRegistraion };

                var ex = Assert.Throws<ArgumentException>(() => service.CreateDownloadPackageActionResult(package));

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingVersion()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistraion, Version = null };

                var ex = Assert.Throws<ArgumentException>(() => service.CreateDownloadPackageActionResult(package));

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillGetAResultFromTheFileStorageServiceUsingThePackagesFolder()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);

                service.CreateDownloadPackageActionResult(CreatePackage());

                fileStorageSvc.Verify(x => x.CreateDownloadFileActionResult(Const.PackagesFolderName, It.IsAny<string>()));
            }

            [Fact]
            public void WillGetAResultFromTheFileStorageServiceUsingAFileNameWithIdAndVersion()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);

                service.CreateDownloadPackageActionResult(CreatePackage());

                fileStorageSvc.Verify(x => x.CreateDownloadFileActionResult(It.IsAny<string>(), BuildFileName("theId", "theVersion")));
            }

            [Fact]
            public void WillReturnTheResultFromTheFileStorageService()
            {
                var fakeResult = new RedirectResult("http://aUrl");
                var fileStorageSvc = new Mock<IFileStorageService>();
                fileStorageSvc.Setup(x => x.CreateDownloadFileActionResult(It.IsAny<string>(), It.IsAny<string>())).Returns(fakeResult);
                var service = CreateService(fileStorageSvc: fileStorageSvc);

                var result = service.CreateDownloadPackageActionResult(CreatePackage()) as RedirectResult;

                Assert.Equal(fakeResult, result);
            }
        }

        public class TheSavePackageFileMethod
        {
            [Fact]
            public void WillThrowIfPackageIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.SavePackageFile(null, null));

                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingPackageRegistration()
            {
                var service = CreateService();
                var package = new Package { PackageRegistration = null };

                var ex = Assert.Throws<ArgumentException>(() => service.SavePackageFile(package, CreatePackageFileStream()));

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingPackageRegistrationId()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = null };
                var package = new Package { PackageRegistration = packageRegistraion };

                var ex = Assert.Throws<ArgumentException>(() => service.SavePackageFile(package, CreatePackageFileStream()));

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingVersion()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistraion, Version = null };

                var ex = Assert.Throws<ArgumentException>(() => service.SavePackageFile(package, CreatePackageFileStream()));

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageFileIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.SavePackageFile(CreatePackage(), null));

                Assert.Equal("packageFile", ex.ParamName);
            }

            [Fact]
            public void WillSaveTheFileViaTheFileStorageServiceUsingThePackagesFolder()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);

                service.SavePackageFile(CreatePackage(), CreatePackageFileStream());

                fileStorageSvc.Verify(x => x.SaveFile(Const.PackagesFolderName, It.IsAny<string>(), It.IsAny<Stream>()));
            }

            [Fact]
            public void WillISaveTheFileViaTheFileStorageServiceUsingAFileNameWithIdAndVersion()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var service = CreateService(fileStorageSvc: fileStorageSvc);

                service.SavePackageFile(CreatePackage(), CreatePackageFileStream());

                fileStorageSvc.Verify(x => x.SaveFile(It.IsAny<string>(), BuildFileName("theId", "theVersion"), It.IsAny<Stream>()));
            }

            [Fact]
            public void WillSaveTheFileStreamViaTheFileStorageService()
            {
                var fileStorageSvc = new Mock<IFileStorageService>();
                var fakeStream = new MemoryStream();
                var service = CreateService(fileStorageSvc: fileStorageSvc);

                service.SavePackageFile(CreatePackage(), fakeStream);

                fileStorageSvc.Verify(x => x.SaveFile(It.IsAny<string>(), It.IsAny<string>(), fakeStream));
            }
        }

        static string BuildFileName(
            string id,
            string version)
        {
            return string.Format(Const.PackageFileSavePathTemplate, id, version, Const.PackageFileExtension);
        }

        static Package CreatePackage()
        {
            var packageRegistration = new PackageRegistration { Id = "theId", Packages = new HashSet<Package>() };
            var package = new Package { Version = "theVersion", PackageRegistration = packageRegistration };
            packageRegistration.Packages.Add(package);
            return package;
        }

        static MemoryStream CreatePackageFileStream()
        {
            return new MemoryStream(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }, 0, 8, true, true);
        }

        static PackageFileService CreateService(
            Mock<IConfiguration> configuration = null,
            Mock<IFileStorageService> fileStorageSvc = null)
        {
            if (configuration == null)
            {
                configuration = new Mock<IConfiguration>();
                configuration.Setup(x => x.FileStorageDirectory).Returns("thePackageFileDirectory");
            }

            fileStorageSvc = fileStorageSvc ?? new Mock<IFileStorageService>();

            return new PackageFileService(
                configuration.Object,
                fileStorageSvc.Object);
        }
    }
}

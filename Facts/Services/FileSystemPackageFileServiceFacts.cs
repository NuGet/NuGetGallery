using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Mvc;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class PackageFileServiceFacts
    {
        public class TheSavePackageFileMethod
        {
            [Fact]
            public void WillCreateThePackagesDirectoryIfItDoesNotExist()
            {
                var fileSystemSvc = new Mock<IFileSystemService>();
                fileSystemSvc.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);
                fileSystemSvc.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(new MemoryStream(new byte[8]));
                var service = CreateService(fileSystemSvc: fileSystemSvc);

                service.SavePackageFile(CreatePackage(), CreatePackageFileStream());

                fileSystemSvc.Verify(x => x.CreateDirectory("thePackageFileDirectory"));
            }

            [Fact]
            public void WillSaveThePackageFileToTheConfiguredDirectory()
            {
                var fileSystemSvc = new Mock<IFileSystemService>();
                fileSystemSvc.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
                fileSystemSvc.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(new MemoryStream(new byte[8]));
                var service = CreateService(fileSystemSvc: fileSystemSvc);

                service.SavePackageFile(CreatePackage(), CreatePackageFileStream());

                fileSystemSvc.Verify(x => x.OpenWrite(
                    Path.Combine("thePackageFileDirectory", string.Format(Const.PackageFileSavePathTemplate, "theId", "1.0.42", Const.PackageFileExtension))));
            }

            [Fact]
            public void WillSaveThePackageFileBytes()
            {
                var packageFileStream = CreatePackageFileStream();
                var saveStream = new MemoryStream(new byte[8], 0, 8, true, true);
                var fileSystemSvc = new Mock<IFileSystemService>();
                fileSystemSvc.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
                fileSystemSvc.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(saveStream);
                var service = CreateService(fileSystemSvc: fileSystemSvc);

                service.SavePackageFile(CreatePackage(), packageFileStream);

                for (var i = 0; i < packageFileStream.Length; i++)
                    Assert.Equal(packageFileStream.GetBuffer()[i], saveStream.GetBuffer()[i]);
            }

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
        }

        public class TheCreateDownloadPackageActionResultMethod
        {
            [Fact]
            public void WillReturnAFilePathResultWithThePackageFilePath()
            {
                var service = CreateService();

                var result = service.CreateDownloadPackageResult(CreatePackage()) as FilePathResult;

                Assert.NotNull(result);
                Assert.Equal(
                    Path.Combine("thePackageFileDirectory", string.Format(Const.PackageFileSavePathTemplate, "theId", "1.0.42", Const.PackageFileExtension)),
                    result.FileName);
            }

            [Fact]
            public void WillSetTheResultContentType()
            {
                var service = CreateService();

                var result = service.CreateDownloadPackageResult(CreatePackage()) as FilePathResult;

                Assert.NotNull(result);
                Assert.Equal(Const.PackageContentType, result.ContentType);
            }

            [Fact]
            public void WillSetTheResultDownloadFilePath()
            {
                var service = CreateService();

                var result = service.CreateDownloadPackageResult(CreatePackage()) as FilePathResult;

                Assert.NotNull(result);
                Assert.Equal(
                    string.Format(Const.PackageFileSavePathTemplate, "theId", "1.0.42", Const.PackageFileExtension),
                    result.FileDownloadName);
            }

            [Fact]
            public void WillThrowIfPackageIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.CreateDownloadPackageResult(null));

                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingPackageRegistration()
            {
                var service = CreateService();
                var package = new Package { PackageRegistration = null };

                var ex = Assert.Throws<ArgumentException>(() => service.CreateDownloadPackageResult(package));

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingPackageRegistrationId()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = null };
                var package = new Package { PackageRegistration = packageRegistraion };

                var ex = Assert.Throws<ArgumentException>(() => service.CreateDownloadPackageResult(package));

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfPackageIsMissingVersion()
            {
                var service = CreateService();
                var packageRegistraion = new PackageRegistration { Id = "theId" };
                var package = new Package { PackageRegistration = packageRegistraion, Version = null };

                var ex = Assert.Throws<ArgumentException>(() => service.CreateDownloadPackageResult(package));

                Assert.True(ex.Message.StartsWith("The package is missing required data."));
                Assert.Equal("package", ex.ParamName);
            }
        }

        static Package CreatePackage()
        {
            var packageRegistration = new PackageRegistration { Id = "theId", Packages = new HashSet<Package>() };
            var package = new Package { Version = "1.0.42", PackageRegistration = packageRegistration };
            packageRegistration.Packages.Add(package);
            return package;
        }

        static MemoryStream CreatePackageFileStream()
        {
            return new MemoryStream(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }, 0, 8, true, true);
        }

        static FileSystemPackageFileService CreateService(
            Mock<IConfiguration> configuration = null,
            Mock<IFileSystemService> fileSystemSvc = null)
        {
            if (configuration == null)
            {
                configuration = new Mock<IConfiguration>();
                configuration.Setup(x => x.PackageFileDirectory).Returns("thePackageFileDirectory");
            }

            fileSystemSvc = fileSystemSvc ?? new Mock<IFileSystemService>();

            return new FileSystemPackageFileService(
                configuration.Object,
                fileSystemSvc.Object);
        }
    }
}

using System.IO;
using Moq;
using Xunit;

namespace NuGetGallery {
    public class PackageFileServiceFacts {
        public class TheSavePackageFileMethod {
            [Fact]
            public void WillCreateThePackagesDirectoryIfItDoesNotExist() {
                var stream = PackageFileStream();
                var fileSystemSvc = new Mock<IFileSystemService>();
                fileSystemSvc.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);
                fileSystemSvc.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(new MemoryStream(new byte[stream.Length]));
                var service = CreateService(fileSystemSvc: fileSystemSvc);

                service.SavePackageFile("theId", "1.0.42", stream);

                fileSystemSvc.Verify(x => x.CreateDirectory("thePackageFileDirectory"));
            }

            [Fact]
            public void WillSaveThePackageFileToTheConfiguredDirectory() {
                var stream = PackageFileStream();
                var fileSystemSvc = new Mock<IFileSystemService>();
                fileSystemSvc.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
                fileSystemSvc.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(new MemoryStream(new byte[stream.Length]));
                var service = CreateService(fileSystemSvc: fileSystemSvc);

                service.SavePackageFile("theId", "1.0.42", stream);

                fileSystemSvc.Verify(x => x.OpenWrite(
                    Path.Combine("thePackageFileDirectory", string.Format(Const.SavePackageFilePathTemplate, "theId", "1.0.42", Const.PackageExtension))));
            }

            [Fact]
            public void WillSaveThePackageFileBytes() {
                var packageFileStream = PackageFileStream();
                var saveStream = new MemoryStream(new byte[8], 0, 8, true, true);
                var fileSystemSvc = new Mock<IFileSystemService>();
                fileSystemSvc.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
                fileSystemSvc.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(saveStream);
                var service = CreateService(fileSystemSvc: fileSystemSvc);

                service.SavePackageFile("theId", "1.0.42", packageFileStream);

                for (var i = 0; i < packageFileStream.Length; i++)
                    Assert.Equal(packageFileStream.GetBuffer()[i], saveStream.GetBuffer()[i]);
            }
        }

        static MemoryStream PackageFileStream() {
            return new MemoryStream(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }, 0, 8, true, true);
        }
        
        static FileSystemPackageFileService CreateService(
            Mock<IConfiguration> configuration = null,
            Mock<IEntityRepository<Package>> packageSvc = null,
            Mock<IFileSystemService> fileSystemSvc = null) {
            if (configuration == null) {
                configuration = new Mock<IConfiguration>();
                configuration.Setup(x => x.PackageFileDirectory).Returns("thePackageFileDirectory");
            }
            
            packageSvc = packageSvc ?? new Mock<IEntityRepository<Package>>();
            fileSystemSvc = fileSystemSvc ?? new Mock<IFileSystemService>();

            return new FileSystemPackageFileService(
                configuration.Object,
                packageSvc.Object,
                fileSystemSvc.Object);
        }
    }
}

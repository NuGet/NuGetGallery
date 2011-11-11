using System;
using System.IO;
using Moq;
using NuGet;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class PackageUploadFileServiceFacts
    {
        public class TheSaveUploadedFileMethod
        {
            [Fact]
            public void WillThrowIfTheUserKeyIsMissing()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() =>
                {
                    service.SaveUploadedFile(0, "thePackageId", "thePackageVersion", new MemoryStream());
                });

                Assert.Equal("userKey", ex.ParamName);
            }

            [Theory]
            [InlineData(new object[]{ (string)null })]
            [InlineData(new object[] { "" })]
            [InlineData(new object[] { " " })]
            public void WillThrowIfThePackageIdIsNullOrEmptyOrWhitespace(string packageId)
            {
                var fakePackage = new Mock<IPackageMetadata>();
                fakePackage.Setup(x => x.Id).Returns(packageId);
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() =>
                {
                    service.SaveUploadedFile(1, packageId, "thePackageVersion", new MemoryStream());
                });

                Assert.Equal("packageId", ex.ParamName);
            }

            [Theory]
            [InlineData(new object[] { (string)null })]
            [InlineData(new object[] { "" })]
            [InlineData(new object[] { " " })]
            public void WillThrowIfThePackageVersionIsNull(string packageVersion)
            {
                var fakePackage = new Mock<IPackageMetadata>();
                fakePackage.Setup(x => x.Id).Returns("theId");
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() =>
                {
                    service.SaveUploadedFile(1, "thePackageId", packageVersion, new MemoryStream());
                });

                Assert.Equal("packageVersion", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfThePackageFileStreamIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() =>
                {
                    service.SaveUploadedFile(1, "thePackageId", "thePackageVersion", null);
                });

                Assert.Equal("packageFileStream", ex.ParamName);
            }

            [Fact]
            public void WillSaveTheUploadToTheUploadsFolder()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);

                service.SaveUploadedFile(1, "thePackageId", "thePackageVersion", new MemoryStream());

                fakeFileStorageService.Verify(x => x.SaveFile(Const.PackageUploadsFolderName, It.IsAny<string>(), It.IsAny<Stream>()));
            }

            [Fact]
            public void WillUseTheUserKeyInTheFileName()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);
                var expectedFileName = string.Format(Const.PackageUploadFileNameTemplate, 1, Const.PackageFileExtension);

                service.SaveUploadedFile(1, "thePackageId", "thePackageVersion", new MemoryStream());

                fakeFileStorageService.Verify(x => x.SaveFile(It.IsAny<string>(), expectedFileName, It.IsAny<Stream>()));
            }

            [Fact]
            public void WillSaveTheUploadFileStream()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var fakeUploadFileStream = new MemoryStream();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);
                var expectedFileName = string.Format(Const.PackageUploadFileNameTemplate, 1, Const.PackageFileExtension);

                service.SaveUploadedFile(1, "thePackageId", "thePackageVersion", fakeUploadFileStream);

                fakeFileStorageService.Verify(x => x.SaveFile(It.IsAny<string>(), It.IsAny<string>(), fakeUploadFileStream));
            }
        }

        static PackageUploadFileService CreateService(Mock<IFileStorageService> fakeFileStorageService = null)
        {
            if (fakeFileStorageService == null)
                fakeFileStorageService = new Mock<IFileStorageService>();
            
            return new PackageUploadFileService(fakeFileStorageService.Object);
        }
    }
}

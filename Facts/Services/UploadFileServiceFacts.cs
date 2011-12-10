using System;
using System.IO;
using Moq;
using NuGet;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class UploadFileServiceFacts
    {
        public class TheDeleteUploadFileMethod
        {
            [Fact]
            public void WillThrowIfTheUserKeyIsMissing()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() =>
                {
                    service.DeleteUploadFile(0);
                });

                Assert.Equal("userKey", ex.ParamName);
            }

            [Fact]
            public void WillDeleteFromTheUploadToTheUploadsFolder()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);

                service.DeleteUploadFile(1);

                fakeFileStorageService.Verify(x => x.DeleteFile(Constants.UploadsFolderName, It.IsAny<string>()));
            }

            [Fact]
            public void WillUseTheUserKeyInTheFileName()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);
                var expectedFileName = String.Format(Constants.UploadFileNameTemplate, 1, Constants.NuGetPackageFileExtension);

                service.DeleteUploadFile(1);

                fakeFileStorageService.Verify(x => x.DeleteFile(It.IsAny<string>(), expectedFileName));
            }
        }

        public class TheGetUploadFileMethod
        {
            [Fact]
            public void WillThrowIfTheUserKeyIsMissing()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() =>
                {
                    service.GetUploadFile(0);
                });

                Assert.Equal("userKey", ex.ParamName);
            }

            [Fact]
            public void WillGetTheUploadFileFromTheUploadsFolder()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);

                service.GetUploadFile(1);

                fakeFileStorageService.Verify(x => x.GetFile(Constants.UploadsFolderName, It.IsAny<string>()));
            }

            [Fact]
            public void WillUseTheUserKeyInTheFileName()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);
                var expectedFileName = String.Format(Constants.UploadFileNameTemplate, 1, Constants.NuGetPackageFileExtension);

                service.GetUploadFile(1);

                fakeFileStorageService.Verify(x => x.GetFile(It.IsAny<string>(), expectedFileName));
            }

            [Fact]
            public void WillReturnTheUploadFileStream()
            {
                var expectedFileName = String.Format(Constants.UploadFileNameTemplate, 1, Constants.NuGetPackageFileExtension);
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var fakeFileStream = new MemoryStream();
                fakeFileStorageService.Setup(x => x.GetFile(Constants.UploadsFolderName, expectedFileName)).Returns(fakeFileStream);
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);

                var fileStream = service.GetUploadFile(1);

                Assert.Same(fakeFileStream, fileStream);
            }
        }
        
        public class TheSaveUploadFileMethod
        {
            [Fact]
            public void WillThrowIfTheUserKeyIsMissing()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() =>
                {
                    service.SaveUploadFile(0, new MemoryStream());
                });

                Assert.Equal("userKey", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfTheUploadFileStreamIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() =>
                {
                    service.SaveUploadFile(1, null);
                });

                Assert.Equal("packageFileStream", ex.ParamName);
            }

            [Fact]
            public void WillSaveTheUploadToTheUploadsFolder()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);

                service.SaveUploadFile(1, new MemoryStream());

                fakeFileStorageService.Verify(x => x.SaveFile(Constants.UploadsFolderName, It.IsAny<string>(), It.IsAny<Stream>()));
            }

            [Fact]
            public void WillUseTheUserKeyInTheFileName()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);
                var expectedFileName = String.Format(Constants.UploadFileNameTemplate, 1, Constants.NuGetPackageFileExtension);

                service.SaveUploadFile(1, new MemoryStream());

                fakeFileStorageService.Verify(x => x.SaveFile(It.IsAny<string>(), expectedFileName, It.IsAny<Stream>()));
            }

            [Fact]
            public void WillSaveTheUploadFileStream()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var fakeUploadFileStream = new MemoryStream();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);
                var expectedFileName = String.Format(Constants.UploadFileNameTemplate, 1, Constants.NuGetPackageFileExtension);

                service.SaveUploadFile(1, fakeUploadFileStream);

                fakeFileStorageService.Verify(x => x.SaveFile(It.IsAny<string>(), It.IsAny<string>(), fakeUploadFileStream));
            }
        }

        static UploadFileService CreateService(Mock<IFileStorageService> fakeFileStorageService = null)
        {
            if (fakeFileStorageService == null)
                fakeFileStorageService = new Mock<IFileStorageService>();
            
            return new UploadFileService(fakeFileStorageService.Object);
        }
    }
}

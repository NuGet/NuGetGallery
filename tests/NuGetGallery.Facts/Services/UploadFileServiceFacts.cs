// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class UploadFileServiceFacts
    {
        private static UploadFileService CreateService(Mock<IFileStorageService> fakeFileStorageService = null)
        {
            if (fakeFileStorageService == null)
            {
                fakeFileStorageService = new Mock<IFileStorageService>();
            }

            return new UploadFileService(fakeFileStorageService.Object);
        }

        public class TheDeleteUploadFileMethod
        {
            [Fact]
            public void WillThrowIfTheUserKeyIsMissing()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() => { service.DeleteUploadFileAsync(0); });

                Assert.Equal("userKey", ex.ParamName);
            }

            [Fact]
            public void WillDeleteFromTheUploadToTheUploadsFolder()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);

                service.DeleteUploadFileAsync(1);

                fakeFileStorageService.Verify(x => x.DeleteFileAsync(Constants.UploadsFolderName, It.IsAny<string>()));
            }

            [Fact]
            public void WillUseTheUserKeyInTheFileName()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);
                var expectedFileName = String.Format(Constants.UploadFileNameTemplate, 1, Constants.NuGetPackageFileExtension);

                service.DeleteUploadFileAsync(1);

                fakeFileStorageService.Verify(x => x.DeleteFileAsync(It.IsAny<string>(), expectedFileName));
            }
        }

        public class TheGetUploadFileMethod
        {
            [Fact]
            public void WillThrowIfTheUserKeyIsMissing()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() => { service.GetUploadFileAsync(0); });

                Assert.Equal("userKey", ex.ParamName);
            }

            [Fact]
            public void WillGetTheUploadFileFromTheUploadsFolder()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);

                service.GetUploadFileAsync(1);

                fakeFileStorageService.Verify(x => x.GetFileAsync(Constants.UploadsFolderName, It.IsAny<string>()));
            }

            [Fact]
            public void WillUseTheUserKeyInTheFileName()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);
                var expectedFileName = String.Format(Constants.UploadFileNameTemplate, 1, Constants.NuGetPackageFileExtension);

                service.GetUploadFileAsync(1);

                fakeFileStorageService.Verify(x => x.GetFileAsync(It.IsAny<string>(), expectedFileName));
            }

            [Fact]
            public async Task WillReturnTheUploadFileStream()
            {
                var expectedFileName = String.Format(Constants.UploadFileNameTemplate, 1, Constants.NuGetPackageFileExtension);
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var fakeFileStream = new MemoryStream();
                fakeFileStorageService.Setup(x => x.GetFileAsync(Constants.UploadsFolderName, expectedFileName))
                                      .Returns(Task.FromResult<Stream>(fakeFileStream));
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);

                var fileStream = await service.GetUploadFileAsync(1);

                Assert.Same(fakeFileStream, fileStream);
            }
        }

        public class TheSaveUploadFileMethod
        {
            [Fact]
            public void WillThrowIfTheUserKeyIsMissing()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() => { service.SaveUploadFileAsync(0, new MemoryStream()); });

                Assert.Equal("userKey", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfTheUploadFileStreamIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => { service.SaveUploadFileAsync(1, null); });

                Assert.Equal("packageFileStream", ex.ParamName);
            }

            [Fact]
            public void WillSaveTheUploadToTheUploadsFolder()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);

                service.SaveUploadFileAsync(1, new MemoryStream());

                fakeFileStorageService.Verify(x => x.SaveFileAsync(Constants.UploadsFolderName, It.IsAny<string>(), It.IsAny<Stream>()));
            }

            [Fact]
            public void WillUseTheUserKeyInTheFileName()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);
                var expectedFileName = String.Format(Constants.UploadFileNameTemplate, 1, Constants.NuGetPackageFileExtension);

                service.SaveUploadFileAsync(1, new MemoryStream());

                fakeFileStorageService.Verify(x => x.SaveFileAsync(It.IsAny<string>(), expectedFileName, It.IsAny<Stream>()));
            }

            [Fact]
            public void WillSaveTheUploadFileStream()
            {
                var fakeFileStorageService = new Mock<IFileStorageService>();
                var fakeUploadFileStream = new MemoryStream();
                var service = CreateService(fakeFileStorageService: fakeFileStorageService);

                service.SaveUploadFileAsync(1, fakeUploadFileStream);

                fakeFileStorageService.Verify(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), fakeUploadFileStream));
            }
        }
    }
}
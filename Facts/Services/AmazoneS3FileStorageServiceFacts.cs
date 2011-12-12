using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using Amazon.S3;
using Moq;
using Xunit;
using Xunit.Extensions;
using System.Net;
using Amazon.Runtime;
using Amazon.S3.Model;

namespace NuGetGallery
{
    public class AmazoneS3FileStorageServiceFacts
    {
        public class TheCreateDownloadFileActionResultMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public void WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.CreateDownloadFileActionResult(
                    folderName,
                    "theFileName"));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public void WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.CreateDownloadFileActionResult(
                    Const.PackagesFolderName,
                    fileName));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public void WillReturnARedirectResultWithAProperAmazonS3UrlPath()
            {
                var service = CreateService();

                var result = service.CreateDownloadFileActionResult(Const.PackagesFolderName, "theFileName") as RedirectResult;

                Assert.NotNull(result);
                Assert.Equal(
                   string.Format("http://{0}.s3.amazonaws.com/{1}", fakeBucketName, "theFileName"),
                    result.Url);
            }
        }

        public class TheDeleteFileMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.DeleteFile(
                    folderName,
                    "theFileName"));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.DeleteFile(
                    Const.PackagesFolderName,
                    fileName));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public void WillDeleteTheFileIfItExists()
            {
                bool fileWasDeleted = false;
                var fakeClient = new Mock<AmazonS3>();
                var fakeS3Client = new Mock<IAmazonS3Client>();
                fakeS3Client.Setup(x => x.CreateInstance()).Returns(fakeClient.Object);
                fakeClient.Setup(x => x.DeleteObject(It.IsAny<DeleteObjectRequest>())).Callback(() => fileWasDeleted = true);

                var service = CreateService(fakeS3Client: fakeS3Client);
                service.DeleteFile(Const.PackagesFolderName, "theFileName");

                Assert.True(fileWasDeleted);
            }

            [Fact]
            public void WillThrowAnErrorIfTheFileIfItDoesNotExist()
            {
                var fakeClient = new Mock<AmazonS3>();
                var fakeS3Client = new Mock<IAmazonS3Client>();
                fakeS3Client.Setup(x => x.CreateInstance()).Returns(fakeClient.Object);
                fakeClient.Setup(x => x.DeleteObject(It.IsAny<DeleteObjectRequest>())).Throws(new AmazonS3Exception("", HttpStatusCode.NotFound));

                var service = CreateService(fakeS3Client: fakeS3Client);
                var ex = Assert.Throws<AmazonS3Exception>(() => service.DeleteFile(Const.PackagesFolderName, "theFileName"));

                Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        public class TheGetFileMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.GetFile(
                    folderName,
                    "theFileName"));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.GetFile(
                    Const.PackagesFolderName,
                    fileName));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public void WillReadTheRequestedFileWhenItExists()
            {
                bool objectWasCalled = false;
                var fakeClient = new Mock<AmazonS3>();
                var fakeS3Client = new Mock<IAmazonS3Client>();
                fakeS3Client.Setup(x => x.CreateInstance()).Returns(fakeClient.Object);
                var service = CreateService(fakeS3Client: fakeS3Client);
                fakeClient.Setup(x => x.GetObject(It.IsAny<GetObjectRequest>())).Callback(() => objectWasCalled = true);

                service.GetFile("theFolderName", "theFileName");

                Assert.True(objectWasCalled);
            }

            [Fact]
            public void WillReturnTheRequestFileStreamWhenItExists()
            {
                var fakeClient = new Mock<AmazonS3>();
                var fakeS3Client = new Mock<IAmazonS3Client>();
                fakeS3Client.Setup(x => x.CreateInstance()).Returns(fakeClient.Object);

                var service = CreateService(fakeS3Client: fakeS3Client);

                Stream fakeFileStream = new MemoryStream();
                fakeClient.Setup(x => x.GetObject(It.IsAny<GetObjectRequest>())).Returns(new GetObjectResponse() { ResponseStream = fakeFileStream });

                var fileStream = service.GetFile("theFolderName", "theFileName");

                Assert.Same(fakeFileStream, fileStream);
            }

            [Fact]
            public void WillThrowAnErrorWhenRequestedFileDoesNotExist()
            {
                var fakeClient = new Mock<AmazonS3>();
                var fakeS3Client = new Mock<IAmazonS3Client>();
                fakeS3Client.Setup(x => x.CreateInstance()).Returns(fakeClient.Object);

                var service = CreateService(fakeS3Client: fakeS3Client);

                fakeClient.Setup(x => x.GetObject(It.IsAny<GetObjectRequest>())).Throws(new AmazonS3Exception("", HttpStatusCode.Unauthorized));

                var ex = Assert.Throws<AmazonS3Exception>(() => service.GetFile("theFolderName", "theFileName"));

                Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
            }
        }

        public class TheSaveFileMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.SaveFile(folderName, "theFileName", CreateFileStream()));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.SaveFile("theFolderName", fileName, CreateFileStream()));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfFileStreamIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.SaveFile("theFolderName", "theFileName", null));

                Assert.Equal("fileStream", ex.ParamName);
            }

            [Fact]
            public void WillSaveThePackageFileToAmazonS3Bucket()
            {
                var fakeClient = new Mock<AmazonS3>();
                var fakeS3Client = new Mock<IAmazonS3Client>();
                fakeS3Client.Setup(x => x.CreateInstance()).Returns(fakeClient.Object);
                var service = CreateService(fakeS3Client: fakeS3Client);
                bool fileWasSaved = false;

                service.SaveFile("theFolderName", "theFileName", CreateFileStream());

                fakeClient.Setup(x => x.PutObject(It.IsAny<PutObjectRequest>())).Callback(() => fileWasSaved = true);
                service.SaveFile("theFolderName", "theFileName", CreateFileStream());

                Assert.True(fileWasSaved);
            }
        }

        static MemoryStream CreateFileStream()
        {
            return new MemoryStream(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }, 0, 8, true, true);
        }

        const string fakeBucketName = "theBucket";

        static AmazonS3FileStorageService CreateService(
             Mock<IAmazonS3Client> fakeS3Client = null)
        {
            if (fakeS3Client == null)
                fakeS3Client = new Mock<IAmazonS3Client>();

            fakeS3Client.Setup(x => x.BucketName).Returns(fakeBucketName);

            return new AmazonS3FileStorageService(fakeS3Client.Object);
        }
    }
}
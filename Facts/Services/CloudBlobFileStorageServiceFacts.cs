using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using Microsoft.WindowsAzure.StorageClient;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class CloudBlobFileStorageServiceFacts
    {
        public class TheCtor
        {
            [Theory]
            [FolderNamesData]
            public void WillCreateABlobContainerForAllFoldersIfTheyDoNotExist(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                fakeBlobClient.Verify(x => x.GetContainerReference(folderName));
                fakeBlobContainer.Verify(x => x.CreateIfNotExist());
            }

            [Theory]
            [FolderNamesData(includePermissions: true)]
            public void WillSetPermissionsForAllFolderBlobContainers(
                string folderName, 
                bool isPublic)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);

                var service = CreateService(fakeBlobClient: fakeBlobClient);

                fakeBlobClient.Verify(x => x.GetContainerReference(folderName));
                fakeBlobContainer.Verify(x => x.SetPermissions(It.Is<BlobContainerPermissions>(p => p.PublicAccess == (isPublic ? BlobContainerPublicAccessType.Blob : BlobContainerPublicAccessType.Off))));
            }
        }
        
        public class TheCreateDownloadPackageResultMethod
        {
            [Theory]
            [FolderNamesData]
            public void WillGetTheBlobFromTheCorrectFolderContainer(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(container => 
                    {
                        if (container == folderName)
                            return fakeBlobContainer.Object;
                        else
                            return new Mock<ICloudBlobContainer>().Object; 
                    });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                service.CreateDownloadFileActionResult(folderName, "theFileName");

                fakeBlobContainer.Verify(x => x.GetBlobReference("theFileName"));
            }

            [Fact]
            public void WillReturnARedirectResultToTheBlobUri()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var result = service.CreateDownloadFileActionResult(Constants.PackagesFolderName, "theFileName") as RedirectResult;

                Assert.NotNull(result);
                Assert.Equal("http://theuri/", result.Url);
            }
        }

        public class TheDeletePackageFileMethod
        {
            [Theory]
            [FolderNamesData]
            public void WillGetTheBlobFromTheCorrectFolderContainer(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(container =>
                    {
                        if (container == folderName)
                            return fakeBlobContainer.Object;
                        else
                            return new Mock<ICloudBlobContainer>().Object;
                    });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                service.DeleteFile(folderName, "theFileName");

                fakeBlobContainer.Verify(x => x.GetBlobReference("theFileName"));
            }

            [Fact]
            public void WillDeleteTheBlobIfItExists()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                service.DeleteFile(Constants.PackagesFolderName, "theFileName");

                fakeBlob.Verify(x => x.DeleteIfExists());
            }
        }

        public class TheGetFileMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public void WillThrowIfFileNameIsNull(string fileName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var ex = Assert.Throws<ArgumentNullException>(() =>
                {
                    service.GetFile("theFolderName", fileName);
                });

                Assert.Equal("fileName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public void WillThrowIfFolderNameIsNull(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var ex = Assert.Throws<ArgumentNullException>(() =>
                {
                    service.GetFile(folderName, "theFileName");
                });

                Assert.Equal("folderName", ex.ParamName);
            }
            
            [Theory]
            [FolderNamesData]
            public void WillDownloadTheFile(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(container =>
                    {
                        if (container == folderName)
                            return fakeBlobContainer.Object;
                        else
                            return new Mock<ICloudBlobContainer>().Object;
                    });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                service.GetFile(folderName, "theFileName");

                fakeBlob.Verify(x => x.DownloadToStream(It.IsAny<Stream>()));
            }

            [Theory]
            [FolderNamesData]
            public void WillReturnTheStreamWhenTheFileExists(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(container =>
                    {
                        if (container == folderName)
                            return fakeBlobContainer.Object;
                        else
                            return new Mock<ICloudBlobContainer>().Object;
                    });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.DownloadToStream(It.IsAny<Stream>())).Callback<Stream>(x => { x.WriteByte(42); });
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var stream = service.GetFile(folderName, "theFileName");

                Assert.Equal(42, ((MemoryStream)stream).ToArray()[0]);
            }

            [Theory]
            [FolderNamesData]
            public void WillReturnNullIfFileDoesNotExist(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(container =>
                    {
                        if (container == folderName)
                            return fakeBlobContainer.Object;
                        else
                            return new Mock<ICloudBlobContainer>().Object;
                    });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.DownloadToStream(It.IsAny<Stream>())).Throws(new TestableStorageClientException { ErrorCode = StorageErrorCode.BlobNotFound });
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var stream = service.GetFile(folderName, "theFileName");

                Assert.Null(stream);
            }

            [Theory]
            [FolderNamesData]
            public void WillSetTheStreamPositionToZero(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(container =>
                    {
                        if (container == folderName)
                            return fakeBlobContainer.Object;
                        else
                            return new Mock<ICloudBlobContainer>().Object;
                    });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.DownloadToStream(It.IsAny<Stream>())).Callback<Stream>(x => { x.WriteByte(42); });
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var stream = service.GetFile(folderName, "theFileName");

                Assert.Equal(0, stream.Position);
            }
        }

        public class TheSaveFileMethod
        {
            [Theory]
            [FolderNamesData]
            public void WillGetTheBlobFromTheCorrectFolderContainer(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                   .Returns<string>(container =>
                   {
                       if (container == folderName)
                           return fakeBlobContainer.Object;
                       else
                           return new Mock<ICloudBlobContainer>().Object;
                   });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                service.SaveFile(folderName, "theFileName", new MemoryStream());

                fakeBlobContainer.Verify(x => x.GetBlobReference("theFileName"));
            }

            [Fact]
            public void WillDeleteTheBlobIfItExists()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                service.SaveFile(Constants.PackagesFolderName, "theFileName", new MemoryStream());

                fakeBlob.Verify(x => x.DeleteIfExists());
            }

            [Fact]
            public void WillUploadThePackageFileToTheBlob()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);
                var fakePackageFile = new MemoryStream();

                service.SaveFile(Constants.PackagesFolderName, "theFileName", fakePackageFile);

                fakeBlob.Verify(x => x.UploadFromStream(fakePackageFile));
            }

            [Theory]
            [FolderNamesData]
            public void WillSetTheBlobContentType(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                   .Returns<string>(container =>
                   {
                       if (container == folderName)
                           return fakeBlobContainer.Object;
                       else
                           return new Mock<ICloudBlobContainer>().Object;
                   });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                service.SaveFile(folderName, "theFileName", new MemoryStream());

                Assert.Equal(Constants.PackageContentType, fakeBlob.Object.Properties.ContentType);
                fakeBlob.Verify(x => x.SetProperties());
            }
        }

        class FolderNamesDataAttribute : DataAttribute
        {
            public FolderNamesDataAttribute(bool includePermissions = false)
            {
                IncludePermissions = includePermissions;
            }
            
            public override IEnumerable<object[]> GetData(
                MethodInfo methodUnderTest, 
                Type[] parameterTypes)
            {
                var folderNames = new List<object[]> 
                {
                    new object[] { Constants.PackagesFolderName, true },
                    new object[] { Constants.UploadsFolderName, false }
                };

                if (!IncludePermissions)
                    folderNames = folderNames.Select(fn => new object[] { fn.ElementAt(0) }).ToList();

                return folderNames;
            }

            public bool IncludePermissions { get; set; }
        }

        static CloudBlobFileStorageService CreateService(
            Mock<ICloudBlobClient> fakeBlobClient = null)
        {
            if (fakeBlobClient == null)
                fakeBlobClient = new Mock<ICloudBlobClient>();
            
            return new CloudBlobFileStorageService(fakeBlobClient.Object);
        }
    }
}

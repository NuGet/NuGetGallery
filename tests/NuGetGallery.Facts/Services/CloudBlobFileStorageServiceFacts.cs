using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using Moq;
using NuGetGallery.Configuration;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class CloudBlobFileStorageServiceFacts
    {
        private const string HttpRequestUrlString = "http://nuget.org/api/v2/something";
        private const string HttpsRequestUrlString = "https://nuget.org/api/v2/something";

        private static readonly Uri HttpRequestUrl = new Uri(HttpRequestUrlString);
        private static readonly Uri HttpsRequestUrl = new Uri(HttpsRequestUrlString);

        private static CloudBlobFileStorageService CreateService(
            Mock<ICloudBlobClient> fakeBlobClient = null)
        {
            if (fakeBlobClient == null)
            {
                fakeBlobClient = new Mock<ICloudBlobClient>();
            }

            return new CloudBlobFileStorageService(fakeBlobClient.Object, Mock.Of<IAppConfiguration>());
        }

        private class FolderNamesDataAttribute : DataAttribute
        {
            public FolderNamesDataAttribute(bool includePermissions = false)
            {
                IncludePermissions = includePermissions;
            }

            private bool IncludePermissions { get; set; }

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
                {
                    folderNames = folderNames.Select(fn => new[] { fn.ElementAt(0) }).ToList();
                }

                return folderNames;
            }
        }

        public class TheCreateDownloadFileActionResultAsyncMethod
        {
            [Theory]
            [InlineData(HttpRequestUrlString, "http://")]
            [InlineData(HttpsRequestUrlString, "https://")]
            public async Task WillReturnARedirectResultToTheBlobUri(string requestUrl, string scheme)
            {
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService();
                service.This_GetContainer = (name) => Task.FromResult<CloudBlobContainer>(null);
                service.Container_GetBlobReference = (c, fileName) => fakeBlob.Object;

                var result = await service.CreateDownloadFileActionResultAsync(new Uri(requestUrl), Constants.PackagesFolderName, "theFileName") as RedirectResult;

                Assert.NotNull(result);
                Assert.Equal(scheme + "theuri/", result.Url);
            }
        }

        public class TheDeleteFileAsyncMethod
        {
            [Fact]
            public async Task WillDeleteTheBlobIfItExists()
            {
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                //fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService();
                service.This_GetContainer = name => Task.FromResult<CloudBlobContainer>(null);
                service.Container_GetBlobReference = (c, blobName) => fakeBlob.Object;

                await service.DeleteFileAsync(Constants.PackagesFolderName, "theFileName");

                fakeBlob.Verify(x => x.DeleteIfExistsAsync());
            }
        }

        public class TheGetContainerMethod
        {
            [Theory]
            [FolderNamesData]
            public async Task WillCreateABlobContainerForDemandedFoldersIfTheyDoNotExist(string folderName)
            {
                bool createIfExistsWasCalled = false;
                bool setPermissionsWasCalled = false;

                var fakeBlobClient = new Mock<ICloudBlobClient>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns<CloudBlobContainer>(null);

                var service = CreateService(fakeBlobClient);
                service.Container_CreateIfNotExistAsync = c =>
                {
                    createIfExistsWasCalled = true;
                    return Task.FromResult(0);
                };
                service.Container_SetPermissionsAsync = (c, p) =>
                {
                    setPermissionsWasCalled = true;
                    return Task.FromResult(0);
                };

                var container = await service.GetContainer(folderName);

                Assert.True(createIfExistsWasCalled);
                Assert.True(setPermissionsWasCalled);
                Assert.Null(container);
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
                var service = CreateService();
                service.This_GetContainer = (name) => Task.FromResult<CloudBlobContainer>(null);

                var ex = TaskAssert.ThrowsAsync<ArgumentNullException>(
                    () => service.GetFileAsync("theFolderName", fileName));
                Assert.Equal("fileName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public void WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();
                service.This_GetContainer = (name) => Task.FromResult<CloudBlobContainer>(null);

                var ex = TaskAssert.ThrowsAsync<ArgumentNullException>(() => service.GetFileAsync(folderName, "theFileName"));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [FolderNamesData]
            public async Task WillReturnTheStreamWhenTheFileExists(string folderName)
            {
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()))
                    .Callback<Stream, AccessCondition>((x, _) => { x.WriteByte(42); })
                    .Returns(Task.FromResult(0));

                var service = CreateService();
                service.This_GetContainer = name => Task.FromResult<CloudBlobContainer>(null);
                service.Container_GetBlobReference = (c, blobName) => fakeBlob.Object;

                var stream = await service.GetFileAsync(folderName, "theFileName");

                Assert.Equal(42, ((MemoryStream)stream).ToArray()[0]);
            }

            [Theory]
            [FolderNamesData]
            public async Task WillReturnNullIfFileDoesNotExist(string folderName)
            {
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>())).Throws(
                    new TestableStorageClientException { ErrorCode = BlobErrorCodeStrings.BlobNotFound });

                var service = CreateService();
                service.This_GetContainer = name => Task.FromResult<CloudBlobContainer>(null);
                service.Container_GetBlobReference = (c, blobName) => fakeBlob.Object;

                var stream = await service.GetFileAsync(folderName, "theFileName");

                Assert.Null(stream);
            }
        }
    }
}

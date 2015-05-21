// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
using Xunit.Sdk;

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

            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
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

        public class TheCreateDownloadPackageResultMethod
        {
            [Theory]
            [FolderNamesData]
            public async Task WillGetTheBlobFromTheCorrectFolderContainer(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var httpContext = GetContext();
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.CreateDownloadFileActionResultAsync(HttpRequestUrl, folderName, "theFileName");

                fakeBlobContainer.Verify(x => x.GetBlobReference("theFileName"));
            }

            [Theory]
            [InlineData(HttpRequestUrlString, "http://")]
            [InlineData(HttpsRequestUrlString, "https://")]
            public async Task WillReturnARedirectResultToTheBlobUri(string requestUrl, string scheme)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                fakeBlobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var result = await service.CreateDownloadFileActionResultAsync(new Uri(requestUrl), Constants.PackagesFolderName, "theFileName") as RedirectResult;

                Assert.NotNull(result);
                Assert.Equal(scheme + "theuri/", result.Url);
            }
        }

        public class TheCtor
        {
            [Theory]
            [FolderNamesData]
            public async Task WillCreateABlobContainerForDemandedFoldersIfTheyDoNotExist(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0)).Verifiable();
                fakeBlobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                var simpleCloudBlob = new Mock<ISimpleCloudBlob>();
                simpleCloudBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>())).Returns(Task.FromResult(0));
                fakeBlobContainer.Setup(x => x.GetBlobReference("x.txt")).Returns(simpleCloudBlob.Object);

                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);

                var service = CreateService(fakeBlobClient);
                await service.GetFileAsync(folderName, "x.txt");

                fakeBlobClient.Verify(x => x.GetContainerReference(folderName));
                fakeBlobContainer.Verify();
            }

            [Theory]
            [FolderNamesData(includePermissions: true)]
            public async Task WillSetPermissionsForDemandedFolderInBlobContainers(string folderName, bool isPublic)
            {
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>()))
                    .Returns(Task.FromResult(0))
                    .Verifiable();

                var simpleCloudBlob = new Mock<ISimpleCloudBlob>();
                simpleCloudBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>())).Returns(Task.FromResult(0));

                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                fakeBlobContainer.Setup(x => x.GetBlobReference("x.txt")).Returns(simpleCloudBlob.Object);

                var fakeBlobClient = new Mock<ICloudBlobClient>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);

                var service = CreateService(fakeBlobClient);
                await service.GetFileAsync(folderName, "x.txt");

                fakeBlobClient.Verify(x => x.GetContainerReference(folderName));
                fakeBlobContainer.Verify();
            }
        }

        public class TheDeletePackageFileMethod
        {
            [Theory]
            [FolderNamesData]
            public async Task WillGetTheBlobFromTheCorrectFolderContainer(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.DeleteFileAsync(folderName, "theFileName");

                fakeBlobContainer.Verify(x => x.GetBlobReference("theFileName"));
            }

            [Fact]
            public async Task WillDeleteTheBlobIfItExists()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0)).Verifiable();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                fakeBlobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.DeleteFileAsync(Constants.PackagesFolderName, "theFileName");

                fakeBlob.Verify();
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

                var ex = TaskAssert.ThrowsAsync<ArgumentNullException>(() => service.GetFileAsync("theFolderName", fileName));
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

                var ex = TaskAssert.ThrowsAsync<ArgumentNullException>(() => service.GetFileAsync(folderName, "theFileName"));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [FolderNamesData]
            public async Task WillDownloadTheFile(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();

                var fakeBlob = new Mock<ISimpleCloudBlob>();

                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> containerMock;
                                if (container == folderName)
                                {
                                    containerMock = fakeBlobContainer;
                                }
                                else
                                {
                                    containerMock = new Mock<ICloudBlobContainer>();
                                }

                                containerMock.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                containerMock.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return containerMock.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>())).Returns(Task.FromResult(0)).Verifiable();
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.GetFileAsync(folderName, "theFileName");

                fakeBlob.Verify();
            }

            [Theory]
            [FolderNamesData]
            public async Task WillReturnTheStreamWhenTheFileExists(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()))
                    .Callback<Stream, AccessCondition>((x, _) => { x.WriteByte(42); })
                    .Returns(Task.FromResult(0));

                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var stream = await service.GetFileAsync(folderName, "theFileName");

                Assert.Equal(42, ((MemoryStream)stream).ToArray()[0]);
            }

            [Theory]
            [FolderNamesData]
            public async Task WillReturnNullIfFileDoesNotExist(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);

                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>())).Throws(
                    new TestableStorageClientException { ErrorCode = BlobErrorCodeStrings.BlobNotFound });
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var stream = await service.GetFileAsync(folderName, "theFileName");

                Assert.Null(stream);
            }

            [Theory]
            [FolderNamesData]
            public async Task WillSetTheStreamPositionToZero(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()))
                        .Callback<Stream, AccessCondition>((x, _) => { x.WriteByte(42); })
                        .Returns(Task.FromResult(0));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var stream = await service.GetFileAsync(folderName, "theFileName");

                Assert.Equal(0, stream.Position);
            }
        }

        public class TheSaveFileMethod
        {
            [Theory]
            [FolderNamesData]
            public async Task WillGetTheBlobFromTheCorrectFolderContainer(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));

                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.SaveFileAsync(folderName, "theFileName", new MemoryStream());

                fakeBlobContainer.Verify(x => x.GetBlobReference("theFileName"));
            }

            [Fact]
            public async Task WillDeleteTheBlobIfItExists()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0)).Verifiable();
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0)).Verifiable();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.SaveFileAsync(Constants.PackagesFolderName, "theFileName", new MemoryStream());

                fakeBlob.Verify();
            }

            [Fact]
            public async Task WillUploadThePackageFileToTheBlob()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                fakeBlobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                var service = CreateService(fakeBlobClient: fakeBlobClient);
                var fakePackageFile = new MemoryStream();
                fakeBlob.Setup(x => x.UploadFromStreamAsync(fakePackageFile)).Returns(Task.FromResult(0)).Verifiable();

                await service.SaveFileAsync(Constants.PackagesFolderName, "theFileName", fakePackageFile);

                fakeBlob.Verify();
            }

            [Theory]
            [FolderNamesData]
            public async Task WillSetTheBlobContentType(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.SaveFileAsync(folderName, "theFileName", new MemoryStream());

                Assert.Equal(Constants.PackageContentType, fakeBlob.Object.Properties.ContentType);
                fakeBlob.Verify(x => x.SetPropertiesAsync());
            }
        }

        private static HttpContextBase GetContext(string protocol = "http://")
        {
            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.SetupGet(r => r.Url).Returns(new Uri(protocol + "nuget.org"));

            var httpContext = new Mock<HttpContextBase>();
            httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

            return httpContext.Object;
        }
    }
}

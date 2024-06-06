// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using Xunit;
using Xunit.Sdk;

namespace NuGetGallery
{
    public class CloudBlobFileStorageServiceFacts
    {
        private const string HttpRequestUrlString = "http://nuget.org/api/v2/something";
        private const string HttpsRequestUrlString = "https://nuget.org/api/v2/something";

        private static readonly Uri HttpRequestUrl = new Uri(HttpRequestUrlString);

        private static CloudBlobFileStorageService CreateService(
            Mock<ICloudBlobClient> fakeBlobClient = null,
            Mock<ISourceDestinationRedirectPolicy> redirectPolicy = null,
            Mock<ICloudBlobContainerInformationProvider> folderInformationProvider = null)
        {
            if (fakeBlobClient == null)
            {
                fakeBlobClient = new Mock<ICloudBlobClient>();
            }

            if (redirectPolicy == null)
            {
                redirectPolicy = new Mock<ISourceDestinationRedirectPolicy>();
                redirectPolicy.Setup(p => p.IsAllowed(It.IsAny<Uri>(), It.IsAny<Uri>())).Returns(true);
            }

            if (folderInformationProvider == null)
            {
                folderInformationProvider = new Mock<ICloudBlobContainerInformationProvider>();
                folderInformationProvider
                    .Setup(fip => fip.IsPublicContainer(It.IsAny<string>()))
                    .Returns(false);
            }

            return new CloudBlobFileStorageService(
                fakeBlobClient.Object,
                Mock.Of<IAppConfiguration>(),
                redirectPolicy.Object,
                Mock.Of<IDiagnosticsService>(),
                folderInformationProvider.Object);
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
                        new object[] { CoreConstants.Folders.PackagesFolderName, true },
                        new object[] { CoreConstants.Folders.UploadsFolderName, false }
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
                                blobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
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
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                var requestUri = new Uri(requestUrl);
                fakeBlob.Setup(x => x.Uri).Returns(new Uri(requestUri.Scheme + "://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var result = await service.CreateDownloadFileActionResultAsync(requestUri, CoreConstants.Folders.PackagesFolderName, "theFileName") as RedirectResult;

                Assert.NotNull(result);
                Assert.Equal(scheme + "theuri/", result.Url);
            }

            [Theory]
            [InlineData(HttpsRequestUrlString, "https://theUri:20943", 20943)]
            [InlineData(HttpRequestUrlString, "http://theUri", 80)]
            [InlineData(HttpsRequestUrlString, "https://theUri", 443)]
            public async Task WillUseBlobUriPort(string requestUrl, string blobUrl, int expectedPort)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.Uri).Returns(new Uri(blobUrl));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var result = await service.CreateDownloadFileActionResultAsync(new Uri(requestUrl), CoreConstants.Folders.PackagesFolderName, "theFileName") as RedirectResult;
                var redirectUrl = new Uri(result.Url);
                Assert.Equal(expectedPort, redirectUrl.Port);
            }

            [Fact]
            public async Task WillUseISourceDestinationRedirectPolicy()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                var fakePolicy = new Mock<ISourceDestinationRedirectPolicy>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakePolicy.Setup(x => x.IsAllowed(It.IsAny<Uri>(), It.IsAny<Uri>())).Returns(true).Verifiable();
                var service = CreateService(fakeBlobClient: fakeBlobClient, redirectPolicy: fakePolicy);

                var result = await service.CreateDownloadFileActionResultAsync(
                    new Uri(HttpsRequestUrlString), 
                    CoreConstants.Folders.PackagesFolderName, 
                    "theFileName") as RedirectResult;
                fakePolicy.Verify();
            }

            [Fact]
            public async Task WillThrowIfRedirectIsNotAllowed()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                var fakePolicy = new Mock<ISourceDestinationRedirectPolicy>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakePolicy.Setup(x => x.IsAllowed(It.IsAny<Uri>(), It.IsAny<Uri>())).Returns(false);
                var service = CreateService(fakeBlobClient: fakeBlobClient, redirectPolicy: fakePolicy);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.CreateDownloadFileActionResultAsync(
                        new Uri(HttpsRequestUrlString), 
                        CoreConstants.Folders.PackagesFolderName, "theFileName")
                    );
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

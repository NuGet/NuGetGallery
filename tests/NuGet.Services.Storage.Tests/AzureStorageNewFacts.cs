﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace NuGet.Services.Storage.Tests
{
    public class AzureStorageNewFacts
    {
        private Mock<BlockBlobClient> _blobClientMock = new Mock<BlockBlobClient>();
        private Mock<BlobServiceClient> _blobServiceClientMock = new Mock<BlobServiceClient>();
        private Mock<BlobContainerClient> _blobContainerClientMock = new Mock<BlobContainerClient>();
        private Mock<ILogger<AzureStorage>> _loggerMock = new Mock<ILogger<AzureStorage>>();

        public AzureStorageNewFacts() 
        {
            _blobContainerClientMock
                .Setup(x => x.GetProperties(
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Mock.Of<Response<BlobContainerProperties>>());
            _blobServiceClientMock
                .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
                .Returns(_blobContainerClientMock.Object);
        }

        [Fact]
        public void Constructor_DoNotInitialize()
        {
            var azureStorage = new AzureStorage(
                _blobServiceClientMock.Object,
                "containerName",
                "path",
                new Uri("http://baseAddress"),
                initializeContainer: false,
                enablePublicAccess: false,
                _loggerMock.Object);

            _blobContainerClientMock.Verify(x => x.GetProperties(
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()), Times.Never);
            _blobContainerClientMock.Verify(x => x.CreateIfNotExists(
                It.IsAny<PublicAccessType>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<BlobContainerEncryptionScopeOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
            _blobContainerClientMock.Verify(x => x.SetAccessPolicy(
                It.IsAny<PublicAccessType>(),
                It.IsAny<IEnumerable<BlobSignedIdentifier>>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(true, PublicAccessType.Blob)]
        [InlineData(false, PublicAccessType.None)]
        public void Constructor_Initialize_ContainerDoesNotExist(bool enablePublicAccess, PublicAccessType publicAccess)
        {
            var getPropertiesSetup = _blobContainerClientMock
                .Setup(x => x.GetProperties(
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new RequestFailedException(404, "Not found"));

            var azureStorage = new AzureStorage(
                _blobServiceClientMock.Object,
                "containerName",
                "path",
                new Uri("http://baseAddress"),
                initializeContainer: true,
                enablePublicAccess,
                _loggerMock.Object);

            _blobContainerClientMock.Verify(x => x.CreateIfNotExists(
                publicAccess,
                default,
                default,
                default), Times.Once);
            _blobContainerClientMock.Verify(x => x.SetAccessPolicy(
                It.IsAny<PublicAccessType>(),
                It.IsAny<IEnumerable<BlobSignedIdentifier>>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(true, PublicAccessType.Blob)]
        [InlineData(false, PublicAccessType.None)]
        public void Constructor_Initialize_ContainerExists_AccessMatches(bool enablePublicAccess, PublicAccessType publicAccess)
        {
            var getPropertiesSetup = _blobContainerClientMock
                .Setup(x => x.GetProperties(
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Response.FromValue(
                    BlobsModelFactory.BlobContainerProperties(default, default, publicAccess: publicAccess),
                    Mock.Of<Response>()));

            var azureStorage = new AzureStorage(
                _blobServiceClientMock.Object,
                "containerName",
                "path",
                new Uri("http://baseAddress"),
                initializeContainer: true,
                enablePublicAccess,
                _loggerMock.Object);

            _blobContainerClientMock.Verify(x => x.CreateIfNotExists(
                It.IsAny<PublicAccessType>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<BlobContainerEncryptionScopeOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
            _blobContainerClientMock.Verify(x => x.SetAccessPolicy(
                It.IsAny<PublicAccessType>(),
                It.IsAny<IEnumerable<BlobSignedIdentifier>>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(true, PublicAccessType.Blob)]
        [InlineData(false, PublicAccessType.None)]
        public void Constructor_Initialize_ContainerExists_AccessDoesNotMatch(bool enablePublicAccess, PublicAccessType publicAccess)
        {
            var getPropertiesSetup = _blobContainerClientMock
                .Setup(x => x.GetProperties(
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Response.FromValue(
                    BlobsModelFactory.BlobContainerProperties(default, default, publicAccess: ~publicAccess),
                    Mock.Of<Response>()));

            var azureStorage = new AzureStorage(
                _blobServiceClientMock.Object,
                "containerName",
                "path",
                new Uri("http://baseAddress"),
                initializeContainer: true,
                enablePublicAccess,
                _loggerMock.Object);

            _blobContainerClientMock.Verify(x => x.CreateIfNotExists(
                It.IsAny<PublicAccessType>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<BlobContainerEncryptionScopeOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
            _blobContainerClientMock.Verify(x => x.SetAccessPolicy(
                publicAccess,
                default,
                default,
                default), Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Exists(bool expected)
        {
            var azureResponse = new Mock<Response>();
            _blobClientMock.Setup(c => c.Exists(It.IsAny<CancellationToken>())).Returns(Response.FromValue(expected, azureResponse.Object));
            _blobContainerClientMock.Protected().Setup<BlockBlobClient>("GetBlockBlobClientCore",ItExpr.IsAny<string>())
                .Returns(_blobClientMock.Object);
            var azureStorage = new AzureStorage(_blobServiceClientMock.Object, "containerName", "path", new Uri("http://baseAddress"), initializeContainer: true, enablePublicAccess: false, _loggerMock.Object);

            Assert.Equal(azureStorage.Exists("file"), expected);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExistsAsync(bool expected)
        {
            var azureResponse = new Mock<Response>();
            _blobClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(Response.FromValue(expected, azureResponse.Object)));
            _blobContainerClientMock.Protected().Setup<BlockBlobClient>("GetBlockBlobClientCore", ItExpr.IsAny<string>())
                .Returns(_blobClientMock.Object);
            var azureStorage = new AzureStorage(_blobServiceClientMock.Object, "containerName", "path", new Uri("http://baseAddress"), initializeContainer: true, enablePublicAccess: false, _loggerMock.Object);

            Assert.Equal(await azureStorage.ExistsAsync("file", CancellationToken.None), expected);
        }

        [Theory]
        [InlineData(true, true, 1)]
        [InlineData(true, false, 1)]
        [InlineData(false, true, 0)]
        [InlineData(false, false, 1)]
        public async Task Save(bool overwrite, bool exists, int calledTimes)
        {
            var azureResponse = new Mock<Response>();
            var blobInfoMock = new Mock<BlobInfo>();
            var blobContentInfoMock = new Mock<BlobContentInfo>();

            _blobClientMock.Setup(c => c.SetHttpHeadersAsync(It.IsAny<BlobHttpHeaders>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Response.FromValue(blobInfoMock.Object, azureResponse.Object)));
            _blobClientMock.Setup(c => c.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Response.FromValue(blobContentInfoMock.Object, azureResponse.Object)));
            _blobClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Response.FromValue(exists, azureResponse.Object)));

            _blobContainerClientMock.Protected().Setup<BlockBlobClient>("GetBlockBlobClientCore", ItExpr.IsAny<string>())
                .Returns(_blobClientMock.Object);
            var azureStorage = new AzureStorage(_blobServiceClientMock.Object, "containerName", "path", new Uri("http://baseAddress"), initializeContainer: true, enablePublicAccess: false, _loggerMock.Object);

            await azureStorage.Save(new Uri("http://testUri.com/blob.json"), new StringStorageContent("content"), overwrite: overwrite, CancellationToken.None);

            _blobClientMock.Verify(bcm => bcm.SetHttpHeadersAsync(It.IsAny<BlobHttpHeaders>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Exactly(calledTimes));
            _blobClientMock.Verify(bcm => bcm.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(calledTimes));
        }
    }
}

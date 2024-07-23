// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.DataMovement.Blobs;
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
        private Mock<BlobsStorageResourceProvider> _blobStorageResourceProviderMock = new Mock<BlobsStorageResourceProvider>();
        private Mock<ILogger<AzureStorage>> _loggerMock = new Mock<ILogger<AzureStorage>>();

        public AzureStorageNewFacts() 
        {   }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Exists(bool expected)
        {
            var azureResponse = new Mock<Azure.Response>();
            _blobClientMock.Setup(c => c.Exists(It.IsAny<CancellationToken>())).Returns(Azure.Response.FromValue(expected, azureResponse.Object));
            _blobContainerClientMock.Protected().Setup<BlockBlobClient>("GetBlockBlobClientCore",ItExpr.IsAny<string>())
                .Returns(_blobClientMock.Object);
            _blobServiceClientMock.Setup(bsc => bsc.GetBlobContainerClient(It.IsAny<string>())).Returns(_blobContainerClientMock.Object);
            var azureStorage = new AzureStorage(_blobServiceClientMock.Object, _blobStorageResourceProviderMock.Object, "containerName", "path", new Uri("http://baseAddress"), useServerSideCopy: true, initializeContainer: true, _loggerMock.Object);

            Assert.Equal(azureStorage.Exists("file"), expected);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExistsAsync(bool expected)
        {
            var azureResponse = new Mock<Azure.Response>();
            _blobClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(Azure.Response.FromValue(expected, azureResponse.Object)));
            _blobContainerClientMock.Protected().Setup<BlockBlobClient>("GetBlockBlobClientCore", ItExpr.IsAny<string>())
                .Returns(_blobClientMock.Object);
            _blobServiceClientMock.Setup(bsc => bsc.GetBlobContainerClient(It.IsAny<string>())).Returns(_blobContainerClientMock.Object);
            var azureStorage = new AzureStorage(_blobServiceClientMock.Object, _blobStorageResourceProviderMock.Object, "containerName", "path", new Uri("http://baseAddress"), useServerSideCopy: true, initializeContainer: true, _loggerMock.Object);

            Assert.Equal(await azureStorage.ExistsAsync("file", CancellationToken.None), expected);
        }

        [Theory]
        [InlineData(true, true, 1)]
        [InlineData(true, false, 1)]
        [InlineData(false, true, 0)]
        [InlineData(false, false, 1)]
        public async Task Save(bool overwrite, bool exists, int calledTimes)
        {
            var azureResponse = new Mock<Azure.Response>();
            var blobInfoMock = new Mock<BlobInfo>();
            var blobContentInfoMock = new Mock<BlobContentInfo>();

            _blobClientMock.Setup(c => c.SetHttpHeadersAsync(It.IsAny<BlobHttpHeaders>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Azure.Response.FromValue(blobInfoMock.Object, azureResponse.Object)));
            _blobClientMock.Setup(c => c.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Azure.Response.FromValue(blobContentInfoMock.Object, azureResponse.Object)));
            _blobClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Azure.Response.FromValue(exists, azureResponse.Object)));

            _blobContainerClientMock.Protected().Setup<BlockBlobClient>("GetBlockBlobClientCore", ItExpr.IsAny<string>())
                .Returns(_blobClientMock.Object);
            _blobServiceClientMock.Setup(bsc => bsc.GetBlobContainerClient(It.IsAny<string>())).Returns(_blobContainerClientMock.Object);
            var azureStorage = new AzureStorage(_blobServiceClientMock.Object, _blobStorageResourceProviderMock.Object, "containerName", "path", new Uri("http://baseAddress"), useServerSideCopy: true, initializeContainer: true, _loggerMock.Object);

            await azureStorage.Save(new Uri("http://testUri.com/blob.json"), new StringStorageContent("content"), overwrite: overwrite, CancellationToken.None);

            _blobClientMock.Verify(bcm => bcm.SetHttpHeadersAsync(It.IsAny<BlobHttpHeaders>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Exactly(calledTimes));
            _blobClientMock.Verify(bcm => bcm.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(calledTimes));
        }
    }
}

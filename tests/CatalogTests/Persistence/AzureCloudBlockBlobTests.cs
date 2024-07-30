// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Moq;
using NuGet.Protocol;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Persistence
{
    public class AzureCloudBlockBlobTests
    {
        public class OnLoadAsync : FactBase
        {
            [Fact]
            public void Constructor_WhenBlobIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => new AzureCloudBlockBlob(blockBlobClient: null));

                Assert.Equal("blockBlobClient", exception.ParamName);
            }

            [Fact]
            public async Task ExistsAsync_CallsUnderlyingMethod()
            {
                _blockBlobMock.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Response.FromValue(true, null));

                var blob = new AzureCloudBlockBlob(_blockBlobMock.Object);

                Assert.True(await blob.ExistsAsync(CancellationToken.None));

                _blockBlobMock.Verify(bc => bc.ExistsAsync(It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task FetchAttributesAsync_CallsUnderlyingMethod()
            {
                _blockBlobMock
                    .Setup(x => x.GetPropertiesAsync(null, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobProperties(), null));

                var blob = new AzureCloudBlockBlob(_blockBlobMock.Object);

                await blob.FetchAttributesAsync(CancellationToken.None);

                _blockBlobMock.Verify(bc => bc.GetPropertiesAsync(null, It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task GetMetadataAsync_ReturnsReadOnlyDictionary()
            {
                var metadata = new Dictionary<string, string> { { "key", "value" } };
                _blockBlobMock
                    .Setup(x => x.GetPropertiesAsync(null, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobProperties(metadata: metadata), null));

                var blob = new AzureCloudBlockBlob(_blockBlobMock.Object);

                var actualMetadata = await blob.GetMetadataAsync(CancellationToken.None);

                Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(actualMetadata);

                _blockBlobMock.Verify(bc => bc.GetPropertiesAsync(null, It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task GetStreamAsync_CallsUnderlyingMethod()
            {
                var expectedStream = new MemoryStream();

                _blockBlobMock.Setup(x => x.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Response.FromValue(expectedStream, null));

                var blob = new AzureCloudBlockBlob(_blockBlobMock.Object);

                var actualStream = await blob.GetStreamAsync(CancellationToken.None);

                Assert.Same(expectedStream, actualStream);

                _blockBlobMock.Verify(bc => bc.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task GetETagAsync_CallsUnderlyingMethod()
            {
                var metadata = new Dictionary<string, string> { { "key", "value" } };
                var blob = new AzureCloudBlockBlob(_blockBlobMock.Object);

                var headers = new BlobHttpHeaders();
                var conditions = new BlobRequestConditions();
                var eTag = new ETag("etag_value"); // Provide a valid ETag value
                var lastModified = DateTimeOffset.UtcNow; // Provide a valid DateTimeOffset value
                var blobInfo = BlobsModelFactory.BlobInfo(eTag, lastModified); // Create an instance of BlobInfo with required parameters

                _blockBlobMock
                    .Setup(x => x.GetPropertiesAsync(null, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobProperties(metadata: metadata), null));

                await blob.GetETagAsync(It.IsAny<CancellationToken>());

                _blockBlobMock.Verify(bc => bc.GetPropertiesAsync(null, It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        public class FactBase
        {
            protected readonly AzureStorageBlobs _uncompressedStorage;
            protected readonly Mock<IBlobContainerClientWrapper> _blobContainerMock;
            protected readonly Mock<BlockBlobClient> _blockBlobMock = new Mock<BlockBlobClient>();

            protected readonly Uri _baseAddress;
            protected readonly string _fileName;
            protected readonly Uri _blobUri;

            protected readonly string _contentString;
            protected readonly string _contentType;
            protected readonly string _cacheControl;
            protected readonly StringStorageContent _content;

            public FactBase()
            {
                _baseAddress = new Uri("https://test");
                _fileName = "test.json";
                _blobUri = new Uri(_baseAddress, _fileName);
                _contentType = "application/json";
                _cacheControl = "no-store";
                _contentString = JsonSerializer.Serialize(new { value = "1234" });
                var contentBytes = Encoding.Default.GetBytes(_contentString);
                _content = new StringStorageContent(_contentString, _contentType, _cacheControl);

                _blockBlobMock.Setup(bb => bb.Uri).Returns(_blobUri);
                var response = Response.FromValue(new BlobProperties(), Mock.Of<Response>());
                _blockBlobMock.Setup(bb => bb.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

                _blobContainerMock = new Mock<IBlobContainerClientWrapper>();
                _blobContainerMock.Setup(bc => bc.GetUri()).Returns(_baseAddress);
                _blobContainerMock.Setup(bc => bc.GetBlockBlobClient(_fileName)).Returns(_blockBlobMock.Object);

                _uncompressedStorage = new AzureStorageBlobs(_blobContainerMock.Object, compressContent: false, throttle: NullThrottle.Instance);
            }
        }
    }
}
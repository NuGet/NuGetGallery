// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Moq;
using Xunit;

namespace CatalogTests.Persistence
{
    public class AzureCloudBlockBlobTests
    {
        private static readonly Uri _uri = new Uri("https://nuget.test/blob");

        private readonly Mock<BlockBlobClient> _underlyingBlob;

        public AzureCloudBlockBlobTests()
        {
            _underlyingBlob = new Mock<BlockBlobClient>(MockBehavior.Strict, _uri);
        }

        [Fact]
        public void Constructor_WhenBlobIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new AzureBlockBlob(blob: null));

            Assert.Equal("blob", exception.ParamName);
        }

        [Fact]
        public async Task ExistsAsync_CallsUnderlyingMethod()
        {
            _underlyingBlob.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, null));

            var blob = new AzureBlockBlob(_underlyingBlob.Object);

            Assert.True(await blob.ExistsAsync(CancellationToken.None));

            _underlyingBlob.VerifyAll();
        }

        [Fact]
        public async Task FetchAttributesAsync_CallsUnderlyingMethod()
        {
            _underlyingBlob
                .Setup(x => x.GetPropertiesAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobProperties(), null));

            var blob = new AzureBlockBlob(_underlyingBlob.Object);

            await blob.FetchAttributesAsync(CancellationToken.None);

            _underlyingBlob.VerifyAll();
        }

        [Fact]
        public async Task GetMetadataAsync_ReturnsReadOnlyDictionary()
        {
            var metadata = new Dictionary<string, string> { { "key", "value" } };
            _underlyingBlob
                .Setup(x => x.GetPropertiesAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobProperties(metadata: metadata), null));

            var blob = new AzureBlockBlob(_underlyingBlob.Object);

            var actualMetadata = await blob.GetMetadataAsync(CancellationToken.None);

            Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(actualMetadata);

            _underlyingBlob.VerifyAll();
        }

        [Fact]
        public async Task GetStreamAsync_CallsUnderlyingMethod()
        {
            var expectedStream = new MemoryStream();

            _underlyingBlob.Setup(x => x.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(expectedStream, null));

            var blob = new AzureBlockBlob(_underlyingBlob.Object);

            var actualStream = await blob.GetStreamAsync(CancellationToken.None);

            Assert.Same(expectedStream, actualStream);

            _underlyingBlob.VerifyAll();
        }

        [Fact]
        public async Task SetPropertiesAsync_CallsUnderlyingMethod()
        {
            var blob = new AzureBlockBlob(_underlyingBlob.Object);

            var headers = new BlobHttpHeaders();
            var conditions = new BlobRequestConditions();
            var eTag = new ETag("etag_value"); // Provide a valid ETag value
            var lastModified = DateTimeOffset.UtcNow; // Provide a valid DateTimeOffset value
            var blobInfo = BlobsModelFactory.BlobInfo(eTag, lastModified); // Create an instance of BlobInfo with required parameters

            _underlyingBlob
                .Setup(b => b.SetHttpHeadersAsync(headers, conditions, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(blobInfo, null)); // Return the instance

            await blob.SetPropertiesAsync(headers, conditions);

            _underlyingBlob.VerifyAll();
        }
    }

    public class AzureBlockBlob
    {
        private readonly BlockBlobClient _blob;

        public AzureBlockBlob(BlockBlobClient blob)
        {
            _blob = blob ?? throw new ArgumentNullException(nameof(blob));
        }

        public async Task<bool> ExistsAsync(CancellationToken cancellationToken)
        {
            var response = await _blob.ExistsAsync(cancellationToken);
            return response.Value;
        }

        public async Task FetchAttributesAsync(CancellationToken cancellationToken)
        {
            await _blob.GetPropertiesAsync(null, cancellationToken);
        }

        public async Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken cancellationToken)
        {
            var properties = await _blob.GetPropertiesAsync(null, cancellationToken);
            return new ReadOnlyDictionary<string, string>(properties.Value.Metadata);
        }

        public async Task<Stream> GetStreamAsync(CancellationToken cancellationToken)
        {
            var response = await _blob.OpenReadAsync(new BlobOpenReadOptions(false), cancellationToken);
            return response; // Directly return the response as it is already a Stream
        }

        public async Task SetPropertiesAsync(BlobHttpHeaders headers, BlobRequestConditions conditions)
        {
            await _blob.SetHttpHeadersAsync(headers, conditions);
        }
    }
}
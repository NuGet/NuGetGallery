// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Moq;
using Xunit;

namespace NuGetGallery.Services
{
    public class CloudBlobWrapperFacts
    {
        [Fact]
        public void UriHasNoQuery()
        {
            var client = new CloudBlobClientWrapper("DefaultEndpointsProtocol=https;AccountName=example;SharedAccessSignature=something=somethingelse&sig=somesignature");
            var container = client.GetContainerReference("testcontainer");
            var blob = container.GetBlobReference("testblob");

            Assert.Empty(blob.Uri.Query);
        }

        [Fact]
        public async Task DownloadTextReturnsNullIfBlobDoesntExist()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object, container: null);
            _cloudBlobMock
                .Setup(cb => cb.DownloadContentAsync())
                .ThrowsAsync(new CloudBlobNotFoundException(null))
                .Verifiable();

            var result = await target.DownloadTextIfExistsAsync();
            Assert.Null(result);
        }

        [Fact]
        public async Task DownloadTextPassesThroughExceptions()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object, container: null);
            var exception = new TestException();
            _cloudBlobMock
                .Setup(cb => cb.DownloadContentAsync())
                .ThrowsAsync(exception)
                .Verifiable();

            var thrownException = await Assert.ThrowsAsync<TestException>(() => target.DownloadTextIfExistsAsync());
            Assert.Same(exception, thrownException);
        }
        [Fact]
        public async Task FetchAttributesIfExistsAsyncReturnsTrueOnSuccess()
        {
            var blobProperties = new BlobProperties();
            var response = Response.FromValue(blobProperties, response: null);
            var target = new CloudBlobWrapper(_cloudBlobMock.Object, container: null);
            _cloudBlobMock
                .Setup(cb => cb.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .Verifiable();

            var result = await target.FetchAttributesIfExistsAsync();
            _cloudBlobMock.VerifyAll();
            Assert.True(result);
        }

        [Fact]
        public async Task FetchAttributesIfExistsAsyncReturnsFalseOnNoBlob()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object, container: null);
            _cloudBlobMock
                .Setup(cb => cb.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CloudBlobNotFoundException(null))
                .Verifiable();

            var result = await target.FetchAttributesIfExistsAsync();
            _cloudBlobMock.VerifyAll();
            Assert.False(result);
        }

        [Fact]
        public async Task FetchAttributesIfExistsAsyncPassesThroughExceptions()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object, container: null);
            var exception = new TestException();
            _cloudBlobMock
                .Setup(cb => cb.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception)
                .Verifiable();

            var thrownException = await Assert.ThrowsAsync<TestException>(() => target.FetchAttributesIfExistsAsync());
            _cloudBlobMock.VerifyAll();
            Assert.Same(exception, thrownException);
        }

        [Fact]
        public async Task OpenReadIfExistsAsyncReturnsNullOnNoBlob()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object, container: null);
            _cloudBlobMock
                .Setup(cb => cb.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CloudBlobNotFoundException(null))
                .Verifiable();

            var returnedStream = await target.OpenReadIfExistsAsync();
            _cloudBlobMock.VerifyAll();
            Assert.Null(returnedStream);
        }

        [Fact]
        public async Task OpenReadIfExistsAsyncPassesThroughExceptions()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object, container: null);
            var exception = new TestException();
            _cloudBlobMock
                .Setup(cb => cb.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception)
                .Verifiable();

            var thrownException = await Assert.ThrowsAsync<TestException>(() => target.OpenReadIfExistsAsync());
            _cloudBlobMock.VerifyAll();
            Assert.Same(exception, thrownException);
        }

        private class TestException : Exception
        {
        }

        private Mock<BlockBlobClient> _cloudBlobMock;

        public CloudBlobWrapperFacts()
        {
            _cloudBlobMock = new Mock<BlockBlobClient>();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using Xunit;

namespace NuGetGallery.Services
{
    public class CloudBlobWrapperFacts
    {
        [Fact]
        public async Task DownloadsText()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object);
            const string text = "sometext";
            _cloudBlobMock
                .Setup(cb => cb.DownloadTextAsync())
                .ReturnsAsync(text)
                .Verifiable();

            var result = await target.DownloadTextIfExistsAsync();
            _cloudBlobMock.VerifyAll();
            Assert.Equal(text, result);
        }

        [Fact]
        public async Task DownloadTextReturnsNullIfBlobDoesntExist()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object);
            _cloudBlobMock
                .Setup(cb => cb.DownloadTextAsync())
                .ThrowsAsync(CreateBlobNotFoundException())
                .Verifiable();

            var result = await target.DownloadTextIfExistsAsync();
            Assert.Null(result);
        }

        [Fact]
        public async Task DownloadTextPassesThroughExceptions()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object);
            var exception = new TestException();
            _cloudBlobMock
                .Setup(cb => cb.DownloadTextAsync())
                .ThrowsAsync(exception)
                .Verifiable();

            var thrownException = await Assert.ThrowsAsync<TestException>(() => target.DownloadTextIfExistsAsync());
            Assert.Same(exception, thrownException);
        }

        [Fact]
        public async Task FetchAttributesIfExistsAsyncReturnsTrueOnSuccess()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object);
            _cloudBlobMock
                .Setup(cb => cb.FetchAttributesAsync())
                .Returns(Task.CompletedTask)
                .Verifiable();

            var result = await target.FetchAttributesIfExistsAsync();
            _cloudBlobMock.VerifyAll();
            Assert.True(result);
        }

        [Fact]
        public async Task FetchAttributesIfExistsAsyncReturnsFalseOnNoBlob()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object);
            _cloudBlobMock
                .Setup(cb => cb.FetchAttributesAsync())
                .ThrowsAsync(CreateBlobNotFoundException())
                .Verifiable();

            var result = await target.FetchAttributesIfExistsAsync();
            _cloudBlobMock.VerifyAll();
            Assert.False(result);
        }

        [Fact]
        public async Task FetchAttributesIfExistsAsyncPassesThroughExceptions()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object);
            var exception = new TestException();
            _cloudBlobMock
                .Setup(cb => cb.FetchAttributesAsync())
                .ThrowsAsync(exception)
                .Verifiable();

            var thrownException = await Assert.ThrowsAsync<TestException>(() => target.FetchAttributesIfExistsAsync());
            _cloudBlobMock.VerifyAll();
            Assert.Same(exception, thrownException);
        }

        [Fact]
        public async Task OpenReadIfExistsAsyncReturnsStream()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object);
            using (var stream = new MemoryStream())
            {
                _cloudBlobMock
                    .Setup(cb => cb.OpenReadAsync(It.IsAny<AccessCondition>(), It.IsAny<BlobRequestOptions>(), It.IsAny<OperationContext>()))
                    .ReturnsAsync(stream)
                    .Verifiable();

                var returnedStream = await target.OpenReadIfExistsAsync();
                _cloudBlobMock.VerifyAll();
                Assert.Same(stream, returnedStream);
            }
        }

        [Fact]
        public async Task OpenReadIfExistsAsyncReturnsNullOnNoBlob()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object);
            _cloudBlobMock
                .Setup(cb => cb.OpenReadAsync(It.IsAny<AccessCondition>(), It.IsAny<BlobRequestOptions>(), It.IsAny<OperationContext>()))
                .ThrowsAsync(CreateBlobNotFoundException())
                .Verifiable();

            var returnedStream = await target.OpenReadIfExistsAsync();
            _cloudBlobMock.VerifyAll();
            Assert.Null(returnedStream);
        }

        [Fact]
        public async Task OpenReadIfExistsAsyncPassesThroughExceptions()
        {
            var target = new CloudBlobWrapper(_cloudBlobMock.Object);
            var exception = new TestException();
            _cloudBlobMock
                .Setup(cb => cb.OpenReadAsync(It.IsAny<AccessCondition>(), It.IsAny<BlobRequestOptions>(), It.IsAny<OperationContext>()))
                .ThrowsAsync(exception)
                .Verifiable();

            var thrownException = await Assert.ThrowsAsync<TestException>(() => target.OpenReadIfExistsAsync());
            _cloudBlobMock.VerifyAll();
            Assert.Same(exception, thrownException);
        }

        private class TestException : Exception
        {

        }

        private static StorageException CreateBlobNotFoundException()
        {
            var responseMock = new Mock<HttpWebResponse>();
            responseMock
                .SetupGet(r => r.StatusCode)
                .Returns(HttpStatusCode.NotFound);
            var innerException = new WebException("inner", null, WebExceptionStatus.Success, responseMock.Object);
            var exception = new StorageException("nope", innerException);
            return exception;
        }

        private Mock<CloudBlockBlob> _cloudBlobMock;

        public CloudBlobWrapperFacts()
        {
            _cloudBlobMock = new Mock<CloudBlockBlob>(new Uri("https://example.com/blob"));
        }
    }
}

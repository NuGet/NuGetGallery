// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Persistence
{
    public class AzureCloudBlockBlobTests
    {
        private static readonly Uri _uri = new Uri("https://nuget.test/blob");

        private readonly Mock<CloudBlockBlob> _underlyingBlob;

        public AzureCloudBlockBlobTests()
        {
            _underlyingBlob = new Mock<CloudBlockBlob>(MockBehavior.Strict, _uri);
        }

        [Fact]
        public void Constructor_WhenBlobIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new AzureCloudBlockBlob(blob: null));

            Assert.Equal("blob", exception.ParamName);
        }

        [Fact]
        public async Task ExistsAsync_CallsUnderlyingMethod()
        {
            _underlyingBlob.Setup(x => x.ExistsAsync())
                .ReturnsAsync(true);

            var blob = new AzureCloudBlockBlob(_underlyingBlob.Object);

            Assert.True(await blob.ExistsAsync(CancellationToken.None));

            _underlyingBlob.VerifyAll();
        }

        [Fact]
        public async Task FetchAttributesAsync_CallsUnderlyingMethod()
        {
            _underlyingBlob.Setup(x => x.FetchAttributesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(0));

            var blob = new AzureCloudBlockBlob(_underlyingBlob.Object);

            await blob.FetchAttributesAsync(CancellationToken.None);

            _underlyingBlob.VerifyAll();
        }

        // CloudBlockBlob.Metadata is non-virtual, which blocks more thorough testing.
        [Fact]
        public async Task GetMetadataAsync_ReturnsReadOnlyDictionary()
        {
            var blob = new AzureCloudBlockBlob(_underlyingBlob.Object);

            var actualMetadata = await blob.GetMetadataAsync(CancellationToken.None);

            Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(actualMetadata);

            _underlyingBlob.VerifyAll();
        }

        [Fact]
        public async Task GetStreamAsync_CallsUnderlyingMethod()
        {
            var expectedStream = new MemoryStream();

            _underlyingBlob.Setup(x => x.OpenReadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedStream);

            var blob = new AzureCloudBlockBlob(_underlyingBlob.Object);

            var actualStream = await blob.GetStreamAsync(CancellationToken.None);

            Assert.Same(expectedStream, actualStream);

            _underlyingBlob.VerifyAll();
        }

        [Fact]
        public async Task SetPropertiesAsync_CallsUnderlyingMethod()
        {
            // Arrange
            var blob = new AzureCloudBlockBlob(_underlyingBlob.Object);

            var accessCondition = AccessCondition.GenerateEmptyCondition();
            var options = new BlobRequestOptions();
            var operationContext = new OperationContext();

            _underlyingBlob
                .Setup(b => b.SetPropertiesAsync(accessCondition, options, operationContext))
                .Returns(Task.CompletedTask);

            // Act
            await blob.SetPropertiesAsync(accessCondition, options, operationContext);

            // Assert
            _underlyingBlob.VerifyAll();
        }
    }
}
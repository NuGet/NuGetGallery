// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using NuGet.Services.Metadata.Catalog.Persistence;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using NuGet.Services.Storage;
using AzureStorage = NuGet.Services.Metadata.Catalog.Persistence.AzureStorage;

namespace CatalogMetadataTests
{
    public class AzureStorageFacts : AzureStorageBaseFacts
    {
        public AzureStorageFacts() : base()
        {
        }

        [Theory]
        [InlineData(true, true, "SHA512Value1", 10, true, true, "SHA512Value1", 10, true)]
        [InlineData(true, true, "SHA512Value1", 10, true, true, "SHA512Value2", 10, false)]
        [InlineData(false, false, null, 10, true, true, "SHA512Value1", 10, true)]
        [InlineData(true, true, "SHA512Value1", 10, false, false, null, 10, false)]
        [InlineData(false, false, null, 10, false, false, null, 10, true)]
        [InlineData(true, false, null, 10, true, true, "SHA512Value1", 10, false)]
        [InlineData(true, true, "SHA512Value1", 10, true, false, null, 10, false)]
        [InlineData(true, false, null, 10, true, false, null, 10, false)]
        [InlineData(true, true, "SHA512Value1", 10, true, true, "SHA512Value1", 11, false)]
        public async Task ValidateAreSynchronizedmethod(
            bool sourceBlobExists,
            bool hasSourceBlobSHA512Value,
            string sourceBlobSHA512Value,
            long sourceSize,
            bool destinationBlobExists,
            bool hasDestinationBlobSHA512Value,
            string destinationBlobSHA512Value,
            long destinationSize,
            bool expected)
        {
            // Arrange
            var sourceBlob = GetMockedBlockBlob(sourceBlobExists, hasSourceBlobSHA512Value, sourceBlobSHA512Value, sourceSize, new Uri("https://blockBlob1"));
            var destinationBlob = GetMockedBlockBlob(destinationBlobExists, hasDestinationBlobSHA512Value, destinationBlobSHA512Value, destinationSize, new Uri("https://blockBlob2"));

            // Act
            var isSynchronized = await _storage.AreSynchronized(sourceBlob.Object, destinationBlob.Object);

            // Assert
            sourceBlob.Verify(x => x.ExistsAsync(CancellationToken.None), Times.Once);
            destinationBlob.Verify(x => x.ExistsAsync(CancellationToken.None), Times.Once);

            if (sourceBlobExists && destinationBlobExists)
            {
                sourceBlob.Verify(x => x.FetchAttributesAsync(CancellationToken.None), Times.Once);
                destinationBlob.Verify(x => x.FetchAttributesAsync(CancellationToken.None), Times.Once);

                if (!hasSourceBlobSHA512Value)
                {
                    sourceBlob.Verify(x => x.Uri, Times.Once);
                }
                if (!hasDestinationBlobSHA512Value)
                {
                    destinationBlob.Verify(x => x.Uri, Times.Once);
                }
                if (hasSourceBlobSHA512Value && hasDestinationBlobSHA512Value)
                {
                    sourceBlob.Verify(x => x.Uri, Times.Once);
                    destinationBlob.Verify(x => x.Uri, Times.Once);
                }
            }

            Assert.Equal(expected, isSynchronized);
        }

        private Mock<ICloudBlockBlob> GetMockedBlockBlob(bool isExisted, bool hasSHA512Value, string SHA512Value, long size, Uri blockBlobUri)
        {
            var mockBlob = new Mock<ICloudBlockBlob>();

            mockBlob.Setup(x => x.ExistsAsync(CancellationToken.None))
                .ReturnsAsync(isExisted);

            if (isExisted)
            {
                mockBlob.Setup(x => x.Uri).Returns(blockBlobUri);

                if (hasSHA512Value)
                {
                    mockBlob.Setup(x => x.FetchAttributesAsync(CancellationToken.None))
                        .ReturnsAsync(BlobsModelFactory.BlobProperties(contentLength: size, metadata: new Dictionary<string, string>()
                        {
                            { "SHA512", SHA512Value }
                        }));
                }
                else
                {
                    mockBlob.Setup(x => x.FetchAttributesAsync(CancellationToken.None))
                        .ReturnsAsync(BlobsModelFactory.BlobProperties(contentLength: size, metadata: new Dictionary<string, string>()));
                }
            }

            return mockBlob;
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task ValidateAreSynchronizedmethodWithNullMetadata(bool isSourceBlobMetadataExisted, bool isDestinationBlobMetadataExists)
        {
            // Arrange
            var sourceBlob = GetMockedBlockBlobWithNullMetadata(isSourceBlobMetadataExisted);
            var destinationBlob = GetMockedBlockBlobWithNullMetadata(isDestinationBlobMetadataExists);

            // Act and Assert
            Assert.False(await _storage.AreSynchronized(sourceBlob, destinationBlob));
        }

        private ICloudBlockBlob GetMockedBlockBlobWithNullMetadata(bool isBlobMetadataExisted)
        {
            if (isBlobMetadataExisted)
            {
                var properties = BlobsModelFactory.BlobProperties(metadata: new Dictionary<string, string>());
                return Mock.Of<ICloudBlockBlob>(
                    x => x.ExistsAsync(CancellationToken.None) == Task.FromResult(true) &&
                         x.FetchAttributesAsync(CancellationToken.None) == Task.FromResult(properties));
            }
            else
            {
                var properties = BlobsModelFactory.BlobProperties(metadata: null);
                return Mock.Of<ICloudBlockBlob>(
                    x => x.ExistsAsync(CancellationToken.None) == Task.FromResult(true) &&
                         x.FetchAttributesAsync(CancellationToken.None) == Task.FromResult(properties));
            }
        }
    }

    public abstract class AzureStorageBaseFacts
    {
        protected readonly string _baseAddressString = "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=fake";
        protected readonly Uri _baseAddress = new Uri("https://test");
        protected readonly AzureStorage _storage;

        public AzureStorageBaseFacts()
        {

            // Mock the BlobContainerClient
            string containerName = "azuresearch";
            // The UseDevelopmentStorage=true part is a connection string setting used to configure Azure Storage SDK to connect to the Azure Storage Emulator instead of an actual Azure Storage account. 
            var mockBlobContainerClient = new Mock<BlobContainerClient>(MockBehavior.Strict, "UseDevelopmentStorage=true", containerName);

            // Setup the necessary methods and properties
            mockBlobContainerClient.Setup(client => client.Uri).Returns(new Uri($"{_baseAddress}/{containerName}"));
            mockBlobContainerClient.Setup(client => client.GetBlobClient(It.IsAny<string>()))
                .Returns((string blobName) => new BlobClient(new Uri($"{_baseAddress}/{containerName}/{blobName}")));

            mockBlobContainerClient.Setup(client => client.Name)
                .Returns(containerName);
            
            // Mock the BlobServiceClient
            var mockBlobServiceClient = new Mock<BlobServiceClient>(_baseAddressString);
            mockBlobServiceClient.Setup(x => x.Uri).Returns(_baseAddress);
            mockBlobServiceClient.Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
                .Returns(mockBlobContainerClient.Object);

            // Mock the BlobServiceClientFactory
            var mockBlobServiceFactoryClient = new Mock<BlobServiceClientFactory>(_baseAddressString);
            mockBlobServiceFactoryClient.Setup(x => x.Uri).Returns(_baseAddress);
            mockBlobServiceFactoryClient.Setup(x => x.GetBlobServiceClient(It.IsAny<BlobClientOptions>()))
                .Returns(mockBlobServiceClient.Object);

            // Mock ICloudBlobDirectory
            var directory = new Mock<ICloudBlobDirectory>();

            // Setup the ServiceClient to return the mocked BlobServiceClient
            directory.Setup(x => x.ServiceClient).Returns(mockBlobServiceFactoryClient.Object);
            directory.Setup(x => x.DirectoryPrefix).Returns("");
            directory.Setup(x => x.ContainerClientWrapper).Returns(new BlobContainerClientWrapper(mockBlobContainerClient.Object));

            _storage = new AzureStorage(directory.Object,
                _baseAddress,
                AzureStorage.DefaultMaxExecutionTime,
                AzureStorage.DefaultServerTimeout,
                false);
        }
    }
}

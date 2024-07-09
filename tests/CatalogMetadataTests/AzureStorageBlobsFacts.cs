// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
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

namespace CatalogMetadataTests
{
    public class AzureStorageBlobsFacts
    {
        public class OnLoadAsync : FactBase
        {
            [Fact]
            public async Task ValidUriReturnsContent()
            {
                var content = (StringStorageContent)await _uncompressedStorage.LoadAsync(_blobUri, CancellationToken.None);

                _blobContainerMock.Verify(bc => bc.GetBlockBlobClient(_fileName));
                Assert.Equal(_contentString, content.Content);
            }

            [Fact]
            public async Task InvalidUriReturnsNull()
            {
                var responseMock = new Mock<Response>();
                responseMock.SetupGet(r => r.Status).Returns((int)HttpStatusCode.NotFound);
                _blockBlobMock
                    .Setup(bb => bb.DownloadToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .Throws(new RequestFailedException(responseMock.Object));

                var content = (StringStorageContent)await _uncompressedStorage.LoadAsync(_blobUri, CancellationToken.None);

                Assert.Null(content);
            }
        }

        public class OnSaveAsync : FactBase
        {
            [Fact]
            public async Task WhenCompressedUploadsBlobWithGzipContentEncoding()
            {
                var headers = new BlobHttpHeaders();
                var stream = new MemoryStream();
                _blockBlobMock.Setup(bb => bb.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
                    .Callback<Stream, BlobUploadOptions, CancellationToken>((s, o, c) =>
                    {
                        s.CopyTo(stream);
                        headers = o.HttpHeaders;
                    });

                var compressedStorage = new AzureStorageBlobs(_blobContainerMock.Object, compressContent: true, throttle: NullThrottle.Instance);
                await compressedStorage.SaveAsync(_blobUri, _content, CancellationToken.None);

                var content = await LoadContentAsync(stream, isCompressed: true);
                _blobContainerMock.Verify(bc => bc.GetBlockBlobClient(_fileName));
                Assert.Equal(_contentType, headers.ContentType);
                Assert.Equal(_cacheControl, headers.CacheControl);
                Assert.Equal("gzip", headers.ContentEncoding);
                Assert.Equal(_content.Content, content);
            }

            [Fact]
            public async Task WhenUncompressedUploadsBlobWithNoContentEncoding()
            {
                var headers = new BlobHttpHeaders();
                var stream = new MemoryStream();
                _blockBlobMock.Setup(bb => bb.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
                    .Callback<Stream, BlobUploadOptions, CancellationToken>((s, o, c) =>
                    {
                        s.CopyTo(stream);
                        headers = o.HttpHeaders;
                    });

                await _uncompressedStorage.SaveAsync(_blobUri, _content, CancellationToken.None);

                var content = await LoadContentAsync(stream, isCompressed: false);
                _blobContainerMock.Verify(bc => bc.GetBlockBlobClient(_fileName));
                Assert.Equal(_contentType, headers.ContentType);
                Assert.Equal(_cacheControl, headers.CacheControl);
                Assert.Null(headers.ContentEncoding);
                Assert.Equal(_content.Content, content);
            }

            [Fact]
            public async Task CreateBlobSnapshotIfNonCreated()
            {
                _blobContainerMock.Setup(bc => bc.HasOnlyOriginalSnapshot(_fileName)).Returns(true);

                await _uncompressedStorage.SaveAsync(_blobUri, _content, CancellationToken.None);

                _blockBlobMock.Verify(bb => bb.CreateSnapshotAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()));
            }

            [Fact]
            public async Task DontCreateBlobSnapshotAlreadyCreated()
            {
                _blobContainerMock.Setup(bc => bc.HasOnlyOriginalSnapshot(_fileName)).Returns(false);

                await _uncompressedStorage.SaveAsync(_blobUri, _content, CancellationToken.None);

                _blockBlobMock.Verify(bb => bb.CreateSnapshotAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Never);
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
                _blockBlobMock.Setup(bb => bb.Name).Returns(_fileName);
                _blockBlobMock.Setup(bb => bb.DownloadToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .Callback<Stream, CancellationToken>((s, c) => s.Write(contentBytes, 0, contentBytes.Length));
                var response = Response.FromValue(new BlobProperties(), Mock.Of<Response>());
                _blockBlobMock.Setup(bb => bb.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

                _blobContainerMock = new Mock<IBlobContainerClientWrapper>();
                _blobContainerMock.Setup(bc => bc.GetUri()).Returns(_baseAddress);
                _blobContainerMock.Setup(bc => bc.GetBlockBlobClient(_fileName)).Returns(_blockBlobMock.Object);

                _uncompressedStorage = new AzureStorageBlobs(_blobContainerMock.Object, compressContent: false, throttle: NullThrottle.Instance);
            }

            public async Task<string> LoadContentAsync(Stream stream, bool isCompressed)
            {
                string content;

                stream.Seek(0, SeekOrigin.Begin);

                if (isCompressed)
                {
                    using (var uncompressedStream = new GZipStream(stream, CompressionMode.Decompress))
                    {
                        using (var reader = new StreamReader(uncompressedStream))
                        {
                            content = await reader.ReadToEndAsync();
                        }
                    }
                }
                else
                {
                    using (var reader = new StreamReader(stream))
                    {
                        content = await reader.ReadToEndAsync();
                    }
                }

                return content;
            }
        }
    }
}

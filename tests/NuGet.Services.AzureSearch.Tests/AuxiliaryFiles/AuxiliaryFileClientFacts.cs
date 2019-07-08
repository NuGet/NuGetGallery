// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using NuGet.Services.AzureSearch.SearchService;
using NuGet.Services.AzureSearch.Support;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class AuxiliaryFileClientFacts
    {
        public class LoadDownloadDataAsync : BaseFacts
        {
            public LoadDownloadDataAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ReadsContent()
            {
                var json = @"
[
    [
        ""NuGet.Frameworks"",
        [ ""1.0.0"", 406],
        [ ""2.0.0-ALPHA"", 137]
    ],
    [
        ""NuGet.Versioning"",
        [""3.0.0"", 138]
    ]
]
";
                _blob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var actual = await _target.LoadDownloadDataAsync();

                Assert.NotNull(actual);
                Assert.Equal(406, actual["NuGet.Frameworks"]["1.0.0"]);
                Assert.Equal(137, actual["NuGet.Frameworks"]["2.0.0-alpha"]);
                Assert.Equal(138, actual["nuget.versioning"]["3.0.0"]);
                Assert.Equal(0, actual["nuget.versioning"].GetDownloadCount("4.0.0"));
                Assert.Equal(0, actual.GetDownloadCount("something.else"));
                _blobClient.Verify(x => x.GetContainerReference("my-container"), Times.Once);
                _blobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Once);
                _container.Verify(x => x.GetBlobReference("my-downloads.json"), Times.Once);
                _container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(null), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.IsAny<AccessCondition>()), Times.Once);
            }
        }

        public class LoadDownloadsAsync : BaseFacts
        {
            public LoadDownloadsAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ReadsContent()
            {
                var json = @"
[
    [
        ""NuGet.Frameworks"",
        [ ""1.0.0"", 406],
        [ ""2.0.0-ALPHA"", 137]
    ],
    [
        ""NuGet.Versioning"",
        [""3.0.0"", 138]
    ]
]
";
                _blob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var actual = await _target.LoadDownloadsAsync(etag: null);

                Assert.False(actual.NotModified);
                Assert.Equal(406, actual.Data["NuGet.Frameworks"]["1.0.0"]);
                Assert.Equal(137, actual.Data["NuGet.Frameworks"]["2.0.0-alpha"]);
                Assert.Equal(138, actual.Data["nuget.versioning"]["3.0.0"]);
                Assert.Equal(0, actual.Data["nuget.versioning"]["4.0.0"]);
                Assert.Null(actual.Data["something.else"]);
                Assert.Equal(_etag, actual.Metadata.ETag);
                Assert.NotEqual(TimeSpan.Zero, actual.Metadata.LoadDuration);
                Assert.NotEqual(default(DateTimeOffset), actual.Metadata.Loaded);
                Assert.Equal(DateTimeOffset.MinValue, actual.Metadata.LastModified);
                _blobClient.Verify(x => x.GetContainerReference("my-container"), Times.Once);
                _blobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Once);
                _container.Verify(x => x.GetBlobReference("my-downloads.json"), Times.Once);
                _container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(null), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.IsAny<AccessCondition>()), Times.Once);
            }

            [Fact]
            public async Task HandlesNotModified()
            {
                _blob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ThrowsAsync(new StorageException(
                        res: new RequestResult()
                        {
                            HttpStatusCode = (int)HttpStatusCode.NotModified,
                        },
                        message: "Not so fast, buddy!",
                        inner: null));

                var actual = await _target.LoadDownloadsAsync(etag: "old-etag");

                Assert.True(actual.NotModified);
                Assert.Null(actual.Data);
                Assert.Null(actual.Metadata);
                _blobClient.Verify(x => x.GetContainerReference("my-container"), Times.Once);
                _blobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Once);
                _container.Verify(x => x.GetBlobReference("my-downloads.json"), Times.Once);
                _container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.Is<AccessCondition>(a => a.IfNoneMatchETag == "old-etag")), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.IsAny<AccessCondition>()), Times.Once);
            }
        }

        public class LoadVerifiedPackagesAsync : BaseFacts
        {
            public LoadVerifiedPackagesAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ReadsContent()
            {
                var json = @"
[
    ""NuGet.Frameworks"",
    ""NuGet.Versioning""
]
";
                _blob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var actual = await _target.LoadVerifiedPackagesAsync(etag: null);

                Assert.False(actual.NotModified);
                Assert.Contains("NuGet.Frameworks", actual.Data);
                Assert.Contains("nuget.versioning", actual.Data);
                Assert.DoesNotContain("something.else", actual.Data);
                Assert.Equal(_etag, actual.Metadata.ETag);
                Assert.NotEqual(TimeSpan.Zero, actual.Metadata.LoadDuration);
                Assert.NotEqual(default(DateTimeOffset), actual.Metadata.Loaded);
                Assert.Equal(DateTimeOffset.MinValue, actual.Metadata.LastModified);
                _blobClient.Verify(x => x.GetContainerReference("my-container"), Times.Once);
                _blobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Once);
                _container.Verify(x => x.GetBlobReference("my-verified-packages.json"), Times.Once);
                _container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(null), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.IsAny<AccessCondition>()), Times.Once);
            }

            [Fact]
            public async Task HandlesNotModified()
            {
                _blob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ThrowsAsync(new StorageException(
                        res: new RequestResult()
                        {
                            HttpStatusCode = (int)HttpStatusCode.NotModified,
                        },
                        message: "Not so fast, buddy!",
                        inner: null));

                var downloads = await _target.LoadVerifiedPackagesAsync(etag: "old-etag");

                Assert.True(downloads.NotModified);
                Assert.Null(downloads.Data);
                Assert.Null(downloads.Metadata);
                _blobClient.Verify(x => x.GetContainerReference("my-container"), Times.Once);
                _blobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Once);
                _container.Verify(x => x.GetBlobReference("my-verified-packages.json"), Times.Once);
                _container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.Is<AccessCondition>(a => a.IfNoneMatchETag == "old-etag")), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.IsAny<AccessCondition>()), Times.Once);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<ICloudBlobClient> _blobClient;
            protected readonly SearchServiceConfiguration _config;
            protected readonly Mock<IOptionsSnapshot<SearchServiceConfiguration>> _options;
            protected readonly Mock<IAzureSearchTelemetryService> _telemetryService;
            protected readonly RecordingLogger<AuxiliaryFileClient> _logger;
            protected readonly Mock<ICloudBlobContainer> _container;
            protected readonly Mock<ISimpleCloudBlob> _blob;
            protected readonly string _etag;
            protected readonly AuxiliaryFileClient _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _blobClient = new Mock<ICloudBlobClient>();
                _config = new SearchServiceConfiguration();
                _options = new Mock<IOptionsSnapshot<SearchServiceConfiguration>>();
                _telemetryService = new Mock<IAzureSearchTelemetryService>();
                _logger = output.GetLogger<AuxiliaryFileClient>();
                _container = new Mock<ICloudBlobContainer>();
                _blob = new Mock<ISimpleCloudBlob>();
                _etag = "\"something\"";

                _config.AuxiliaryDataStorageContainer = "my-container";
                _config.AuxiliaryDataStorageDownloadsPath = "my-downloads.json";
                _config.AuxiliaryDataStorageVerifiedPackagesPath = "my-verified-packages.json";
                _options.Setup(x => x.Value).Returns(() => _config);
                _blobClient
                    .Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns(() => _container.Object);
                _container
                    .Setup(x => x.GetBlobReference(It.IsAny<string>()))
                    .Returns(() => _blob.Object);
                _blob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("[]")));
                _blob
                    .Setup(x => x.ETag)
                    .Returns(() => _etag);
                _blob
                    .Setup(x => x.Properties)
                    .Returns(new BlobProperties());

                _target = new AuxiliaryFileClient(
                    _blobClient.Object,
                    _options.Object,
                    _telemetryService.Object,
                    _logger);
            }
        }
    }
}

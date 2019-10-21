// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using NuGet.Services.AzureSearch.Db2AzureSearch;
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
                _blob.Verify(x => x.OpenReadAsync(It.Is<AccessCondition>(a => a.IfMatchETag == null && a.IfNoneMatchETag == null)), Times.Once);
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

                var actual = await _target.LoadVerifiedPackagesAsync();

                Assert.NotNull(actual);
                Assert.Contains("NuGet.Frameworks", actual);
                Assert.Contains("nuget.versioning", actual);
                Assert.DoesNotContain("something.else", actual);
                _blobClient.Verify(x => x.GetContainerReference("my-container"), Times.Once);
                _blobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Once);
                _container.Verify(x => x.GetBlobReference("my-verified-packages.json"), Times.Once);
                _container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.Is<AccessCondition>(a => a.IfMatchETag == null && a.IfNoneMatchETag == null)), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.IsAny<AccessCondition>()), Times.Once);
            }
        }

        public class LoadExcludedPackagesAsync : BaseFacts
        {
            public LoadExcludedPackagesAsync(ITestOutputHelper output) : base(output)
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

                var actual = await _target.LoadExcludedPackagesAsync();

                Assert.NotNull(actual);
                Assert.Contains("NuGet.Frameworks", actual);
                Assert.Contains("nuget.versioning", actual);
                Assert.DoesNotContain("something.else", actual);
                _blobClient.Verify(x => x.GetContainerReference("my-container"), Times.Once);
                _blobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Once);
                _container.Verify(x => x.GetBlobReference("my-excluded-packages.json"), Times.Once);
                _container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.Is<AccessCondition>(a => a.IfMatchETag == null && a.IfNoneMatchETag == null)), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.IsAny<AccessCondition>()), Times.Once);
            }

            [Fact]
            public async Task ThrowsStorageExceptionWhenNotFound()
            {
                _blob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ThrowsAsync(new StorageException(
                        res: new RequestResult()
                        {
                            HttpStatusCode = (int)HttpStatusCode.NotFound,
                        },
                        message: "Not so fast, buddy!",
                        inner: null));

                var exception = await Assert.ThrowsAsync<StorageException>(async () => await _target.LoadExcludedPackagesAsync());
                Assert.True(exception.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotFound);
            }
        }

        public class LoadDownloadOverridesAsync : BaseFacts
        {
            public LoadDownloadOverridesAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ReadsContent()
            {
                var json = @"
{
    ""A"": 10,
    ""B"": 1
}
";
                _blob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var actual = await _target.LoadDownloadOverridesAsync();

                Assert.NotNull(actual);
                Assert.Equal(10, actual["A"]);
                Assert.Equal(1, actual["B"]);
                Assert.Equal(1, actual["b"]);
                _blobClient.Verify(x => x.GetContainerReference("my-container"), Times.Once);
                _blobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Once);
                _container.Verify(x => x.GetBlobReference("my-download-overrides.json"), Times.Once);
                _container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.Is<AccessCondition>(a => a.IfMatchETag == null && a.IfNoneMatchETag == null)), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.IsAny<AccessCondition>()), Times.Once);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<ICloudBlobClient> _blobClient;
            protected readonly Db2AzureSearchConfiguration _config;
            protected readonly AuxiliaryDataStorageConfiguration _configStorage;
            protected readonly Mock<IOptionsSnapshot<Db2AzureSearchConfiguration>> _options;
            protected readonly Mock<IOptionsSnapshot<AuxiliaryDataStorageConfiguration>> _optionsStorage;
            protected readonly Mock<IAzureSearchTelemetryService> _telemetryService;
            protected readonly RecordingLogger<AuxiliaryFileClient> _logger;
            protected readonly Mock<ICloudBlobContainer> _container;
            protected readonly Mock<ISimpleCloudBlob> _blob;
            protected readonly string _etag;
            protected readonly AuxiliaryFileClient _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _blobClient = new Mock<ICloudBlobClient>();
                _config = new Db2AzureSearchConfiguration();
                _configStorage = new AuxiliaryDataStorageConfiguration();
                _options = new Mock<IOptionsSnapshot<Db2AzureSearchConfiguration>>();
                _optionsStorage = new Mock<IOptionsSnapshot<AuxiliaryDataStorageConfiguration>>();
                _telemetryService = new Mock<IAzureSearchTelemetryService>();
                _logger = output.GetLogger<AuxiliaryFileClient>();
                _container = new Mock<ICloudBlobContainer>();
                _blob = new Mock<ISimpleCloudBlob>();
                _etag = "\"something\"";

                _config.AuxiliaryDataStorageContainer = "my-container";
                _config.AuxiliaryDataStorageDownloadsPath = "my-downloads.json";
                _config.AuxiliaryDataStorageDownloadOverridesPath = "my-download-overrides.json";
                _config.AuxiliaryDataStorageVerifiedPackagesPath = "my-verified-packages.json";
                _config.AuxiliaryDataStorageExcludedPackagesPath = "my-excluded-packages.json";
                _options.Setup(x => x.Value).Returns(() => _config);

                _configStorage.AuxiliaryDataStorageContainer = "my-container";
                _configStorage.AuxiliaryDataStorageDownloadsPath = "my-downloads.json";
                _configStorage.AuxiliaryDataStorageDownloadOverridesPath = "my-download-overrides.json";
                _configStorage.AuxiliaryDataStorageVerifiedPackagesPath = "my-verified-packages.json";
                _configStorage.AuxiliaryDataStorageExcludedPackagesPath = "my-excluded-packages.json";
                _optionsStorage.Setup(x => x.Value).Returns(() => _configStorage);

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
                    _optionsStorage.Object,
                    _telemetryService.Object,
                    _logger);
            }
        }
    }
}

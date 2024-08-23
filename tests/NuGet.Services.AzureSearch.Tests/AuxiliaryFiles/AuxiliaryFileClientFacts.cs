﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.Db2AzureSearch;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class AuxiliaryFileClientFacts
    {
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
                    .Setup(x => x.OpenReadAsync(It.IsAny<IAccessCondition>()))
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
                _blob.Verify(x => x.OpenReadAsync(It.Is<IAccessCondition>(a => a.IfMatchETag == null && a.IfNoneMatchETag == null)), Times.Once);
                _blob.Verify(x => x.OpenReadAsync(It.IsAny<IAccessCondition>()), Times.Once);
            }

            [Fact]
            public async Task ThrowsStorageExceptionWhenNotFound()
            {
                _blob
                    .Setup(x => x.OpenReadAsync(It.IsAny<IAccessCondition>()))
                    .ThrowsAsync(new CloudBlobNotFoundException(null));

                var exception = await Assert.ThrowsAsync<CloudBlobNotFoundException>(async () => await _target.LoadExcludedPackagesAsync());
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
                _config.AuxiliaryDataStorageExcludedPackagesPath = "my-excluded-packages.json";
                _options.Setup(x => x.Value).Returns(() => _config);

                _configStorage.AuxiliaryDataStorageContainer = "my-container";
                _configStorage.AuxiliaryDataStorageExcludedPackagesPath = "my-excluded-packages.json";
                _optionsStorage.Setup(x => x.Value).Returns(() => _configStorage);

                _blobClient
                    .Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns(() => _container.Object);
                _container
                    .Setup(x => x.GetBlobReference(It.IsAny<string>()))
                    .Returns(() => _blob.Object);
                _blob
                    .Setup(x => x.OpenReadAsync(It.IsAny<IAccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("[]")));
                _blob
                    .Setup(x => x.ETag)
                    .Returns(() => _etag);
                _blob
                    .Setup(x => x.Properties)
                    .Returns(Mock.Of<ICloudBlobProperties>());

                _target = new AuxiliaryFileClient(
                    _blobClient.Object,
                    _optionsStorage.Object,
                    _telemetryService.Object,
                    _logger);
            }
        }
    }
}

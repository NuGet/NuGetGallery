// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using NuGet.Services.AzureSearch.Support;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch
{
    public class VersionListDataClientFacts
    {
        public class ReadVersionListAsync : Facts
        {
            public ReadVersionListAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ReturnsEmptyListWhenFileDoesNotExist()
            {
                var output = await _target.ReadAsync(_id);

                Assert.NotNull(output);
                Assert.NotNull(output.Result);
                Assert.NotNull(output.AccessCondition);
                Assert.NotNull(output.Result.VersionProperties);
                Assert.Empty(output.Result.VersionProperties);
                Assert.Equal("*", output.AccessCondition.IfNoneMatchETag);
                Assert.Null(output.AccessCondition.IfMatchETag);
            }

            [Theory]
            [MemberData(nameof(ExpectedPaths))]
            public async Task UsesExpectedContainerAndFileName(string path, string expected)
            {
                _config.StoragePath = path;

                await _target.ReadAsync(_id);

                _cloudBlobClient.Verify(
                    x => x.GetContainerReference(_config.StorageContainer),
                    Times.Once);
                _cloudBlobContainer.Verify(
                    x => x.GetBlobReference(expected),
                    Times.Once);
            }

            [Fact]
            public async Task DeserializesJson()
            {
                var version2 = "2.0.0-beta.2";
                var version10 = "10.0.0";
                var versionList = Encoding.UTF8.GetBytes(@"{
  ""VersionProperties"": {
    """ + version10 + @""": {},
    """ + version2 + @""": {
      ""Listed"": true,
      ""SemVer2"": true
    }
  }
}");
                var etag = "\"some-etag\"";
                _cloudBlob
                    .Setup(x => x.ETag)
                    .Returns(etag);
                _cloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(versionList));

                var output = await _target.ReadAsync(_id);

                Assert.NotNull(output);
                Assert.NotNull(output.Result);
                Assert.NotNull(output.AccessCondition);
                Assert.NotNull(output.Result.VersionProperties);
                Assert.Equal(etag, output.AccessCondition.IfMatchETag);
                Assert.Null(output.AccessCondition.IfNoneMatchETag);
                var versions = output.Result.VersionProperties;
                Assert.Equal(2, versions.Count);
                Assert.Equal(new[] { version10, version2 }, versions.Keys.OrderBy(x => x).ToArray());
                Assert.True(versions[version2].Listed);
                Assert.True(versions[version2].SemVer2);
                Assert.False(versions[version10].Listed);
                Assert.False(versions[version10].SemVer2);
            }
        }

        public class ReplaceVersionListAsync : Facts
        {
            public ReplaceVersionListAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [MemberData(nameof(ExpectedPaths))]
            public async Task UsesExpectedStorageParameters(string path, string expected)
            {
                _config.StoragePath = path;

                await _target.ReplaceAsync(_id, _versionList, _accessCondition.Object);

                _cloudBlobClient.Verify(
                    x => x.GetContainerReference(_config.StorageContainer),
                    Times.Once);
                _cloudBlobContainer.Verify(
                    x => x.GetBlobReference(expected),
                    Times.Once);
                _cloudBlob.Verify(
                    x => x.UploadFromStreamAsync(
                        It.IsAny<Stream>(),
                        It.IsAny<AccessCondition>()),
                    Times.Once);
            }

            [Fact]
            public async Task SerializesWithoutBOM()
            {
                await _target.ReplaceAsync(_id, _versionList, _accessCondition.Object);

                var bytes = Assert.Single(_savedBytes);
                Assert.Equal((byte)'{', bytes[0]);
            }

            [Fact]
            public async Task SetsContentType()
            {
                await _target.ReplaceAsync(_id, _versionList, _accessCondition.Object);

                Assert.Equal("application/json", _cloudBlob.Object.Properties.ContentType);
            }

            [Fact]
            public async Task SerializesWithIndentation()
            {
                await _target.ReplaceAsync(_id, _versionList, _accessCondition.Object);

                var json = Assert.Single(_savedStrings);
                Assert.Contains("\n", json);
            }

            [Fact]
            public async Task SerializesVersionsInSemVerOrder()
            {
                var versionList = new VersionListData(new Dictionary<string, VersionPropertiesData>
                {
                    { "2.0.0", new VersionPropertiesData(listed: true, semVer2: false) },
                    { "1.0.0-beta.2", new VersionPropertiesData(listed: true, semVer2: true) },
                    { "10.0.0", new VersionPropertiesData(listed: false, semVer2: false) },
                    { "1.0.0-beta.10", new VersionPropertiesData(listed: true, semVer2: true) },
                });

                await _target.ReplaceAsync(_id, versionList, _accessCondition.Object);

                var json = Assert.Single(_savedStrings);
                Assert.Equal(@"{
  ""VersionProperties"": {
    ""1.0.0-beta.2"": {
      ""Listed"": true,
      ""SemVer2"": true
    },
    ""1.0.0-beta.10"": {
      ""Listed"": true,
      ""SemVer2"": true
    },
    ""2.0.0"": {
      ""Listed"": true
    },
    ""10.0.0"": {}
  }
}", json);
            }
        }

        public abstract class Facts
        {
            protected readonly string _id;
            protected readonly Mock<IAccessCondition> _accessCondition;
            protected readonly VersionListData _versionList;
            protected readonly Mock<ICloudBlobClient> _cloudBlobClient;
            protected readonly Mock<ICloudBlobContainer> _cloudBlobContainer;
            protected readonly Mock<ISimpleCloudBlob> _cloudBlob;
            protected readonly Mock<IOptionsSnapshot<AzureSearchJobConfiguration>> _options;
            protected readonly RecordingLogger<VersionListDataClient> _logger;
            protected readonly AzureSearchJobConfiguration _config;
            protected readonly VersionListDataClient _target;

            protected readonly List<byte[]> _savedBytes = new List<byte[]>();
            protected readonly List<string> _savedStrings = new List<string>();

            public static IEnumerable<object[]> ExpectedPaths => new[]
            {
                new object[] { "/", "version-lists/nuget.versioning.json" },
                new object[] { "", "version-lists/nuget.versioning.json" },
                new object[] { null, "version-lists/nuget.versioning.json" },
                new object[] { "/search/", "search/version-lists/nuget.versioning.json" },
                new object[] { "/search", "search/version-lists/nuget.versioning.json" },
                new object[] { "search/", "search/version-lists/nuget.versioning.json" },
                new object[] { "search", "search/version-lists/nuget.versioning.json" },
                new object[] { "/search/1/", "search/1/version-lists/nuget.versioning.json" },
                new object[] { "/search/1", "search/1/version-lists/nuget.versioning.json" },
                new object[] { "search/1", "search/1/version-lists/nuget.versioning.json" },
                new object[] { "search/1/", "search/1/version-lists/nuget.versioning.json" },
            };

            public Facts(ITestOutputHelper output)
            {
                _id = "NuGet.Versioning";
                _accessCondition = new Mock<IAccessCondition>();
                _versionList = new VersionListData(new Dictionary<string, VersionPropertiesData>
                {
                    { "2.0.0", new VersionPropertiesData(listed: true, semVer2: false) },
                });

                _cloudBlobClient = new Mock<ICloudBlobClient>();
                _cloudBlobContainer = new Mock<ICloudBlobContainer>();
                _cloudBlob = new Mock<ISimpleCloudBlob>();
                _options = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                _logger = output.GetLogger<VersionListDataClient>();
                _config = new AzureSearchJobConfiguration
                {
                    StorageContainer = "unit-test-container",
                };

                _options
                    .Setup(x => x.Value)
                    .Returns(() => _config);
                _cloudBlobClient
                    .Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns(() => _cloudBlobContainer.Object);
                _cloudBlobContainer
                    .Setup(x => x.GetBlobReference(It.IsAny<string>()))
                    .Returns(() => _cloudBlob.Object);
                _cloudBlob
                    .Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()))
                    .Returns(Task.CompletedTask)
                    .Callback<Stream, AccessCondition>((s, _) =>
                    {
                        using (s)
                        using (var buffer = new MemoryStream())
                        {
                            s.CopyTo(buffer);
                            var bytes = buffer.ToArray();
                            _savedBytes.Add(bytes);
                            _savedStrings.Add(Encoding.UTF8.GetString(bytes));
                        }
                    });
                _cloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ThrowsAsync(new StorageException(
                        new RequestResult
                        {
                            HttpStatusCode = (int)HttpStatusCode.NotFound,
                        },
                        message: "Not found.",
                        inner: null));
                _cloudBlob
                    .Setup(x => x.Properties)
                    .Returns(new CloudBlockBlob(new Uri("https://example/blob")).Properties);

                _target = new VersionListDataClient(
                    _cloudBlobClient.Object,
                    _options.Object,
                    _logger);
            }
        }
    }
}

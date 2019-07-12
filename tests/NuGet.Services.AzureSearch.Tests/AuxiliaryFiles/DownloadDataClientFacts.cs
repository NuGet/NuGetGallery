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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.AzureSearch.Support;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class DownloadDataClientFacts
    {
        public class ReadLatestIndexedAsync : Facts
        {
            public ReadLatestIndexedAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task AllowsEmptyObject()
            {
                var json = JsonConvert.SerializeObject(new Dictionary<string, string[]>());
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestIndexedAsync();

                Assert.Empty(output.Result);
                Assert.Equal(ETag, output.AccessCondition.IfMatchETag);
            }

            [Fact]
            public async Task AllowsMissingBlob()
            {
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ThrowsAsync(new StorageException(
                        new RequestResult
                        {
                            HttpStatusCode = (int)HttpStatusCode.NotFound,
                        },
                        message: "Not found.",
                        inner: null));

                var output = await Target.ReadLatestIndexedAsync();

                Assert.Empty(output.Result);
                Assert.Equal("*", output.AccessCondition.IfNoneMatchETag);
            }

            [Fact]
            public async Task RejectsInvalidJson()
            {
                var json = JsonConvert.SerializeObject(new object[]
                {
                    new object[]
                    {
                        "nuget.versioning",
                        new object[]
                        {
                            new object[] { "1.0.0", 5 },
                        },
                    },
                    new object[]
                    {
                        "EntityFramework",
                        new object[]
                        {
                            new object[] { "2.0.0", 10 },
                        },
                    }
                });
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.ReadLatestIndexedAsync());
                Assert.Equal("The first token should be the start of an object.", ex.Message);
            }

            [Fact]
            public async Task ReadsDownloads()
            {
                var json = JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, long>>
                {
                    {
                        "nuget.versioning",
                        new Dictionary<string, long>
                        {
                            { "1.0.0", 1 },
                            { "2.0.0-alpha", 5 },
                        }
                    },
                    {
                        "NuGet.Core",
                        new Dictionary<string, long>()
                    },
                    {
                        "EntityFramework",
                        new Dictionary<string, long>
                        {
                            { "2.0.0", 10 },
                        }
                    },
                });
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestIndexedAsync();

                Assert.Equal(new[] { "EntityFramework", "nuget.versioning" }, output.Result.Select(x => x.Key).ToArray());
                Assert.Equal(6, output.Result.GetDownloadCount("NuGet.Versioning"));
                Assert.Equal(1, output.Result.GetDownloadCount("NuGet.Versioning", "1.0.0"));
                Assert.Equal(5, output.Result.GetDownloadCount("NuGet.Versioning", "2.0.0-ALPHA"));
                Assert.Equal(10, output.Result.GetDownloadCount("EntityFramework"));
                Assert.Equal(ETag, output.AccessCondition.IfMatchETag);

                CloudBlobContainer.Verify(x => x.GetBlobReference("downloads.v2.json"), Times.Once);
            }
        }

        public class ReplaceLatestIndexedAsync : Facts
        {
            public ReplaceLatestIndexedAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SerializesWithoutBOM()
            {
                var newData = new DownloadData();

                await Target.ReplaceLatestIndexedAsync(newData, AccessCondition.Object);

                var bytes = Assert.Single(SavedBytes);
                Assert.Equal((byte)'{', bytes[0]);
            }

            [Fact]
            public async Task SetsContentType()
            {
                var newData = new DownloadData();

                await Target.ReplaceLatestIndexedAsync(newData, AccessCondition.Object);

                Assert.Equal("application/json", CloudBlob.Object.Properties.ContentType);
            }

            [Fact]
            public async Task SerializedWithoutIndentation()
            {
                var newData = new DownloadData();
                newData.SetDownloadCount("nuget.versioning", "1.0.0", 1);
                newData.SetDownloadCount("NuGet.Versioning", "2.0.0", 5);
                newData.SetDownloadCount("EntityFramework", "3.0.0", 10);

                await Target.ReplaceLatestIndexedAsync(newData, AccessCondition.Object);

                var json = Assert.Single(SavedStrings);
                Assert.DoesNotContain("\n", json);
            }

            [Fact]
            public async Task SerializesVersionsSortedOrder()
            {
                var newData = new DownloadData();
                newData.SetDownloadCount("ZZZ", "9.0.0", 23);
                newData.SetDownloadCount("YYY", "9.0.0", 0);
                newData.SetDownloadCount("nuget.versioning", "1.0.0", 1);
                newData.SetDownloadCount("NuGet.Versioning", "2.0.0", 5);
                newData.SetDownloadCount("EntityFramework", "3.0.0", 10);
                newData.SetDownloadCount("EntityFramework", "1.0.0", 0);

                await Target.ReplaceLatestIndexedAsync(newData, AccessCondition.Object);

                // Pretty-ify the JSON to make the assertion clearer.
                var json = Assert.Single(SavedStrings);
                json = JsonConvert.DeserializeObject<JObject>(json).ToString();

                Assert.Equal(@"{
  ""EntityFramework"": {
    ""3.0.0"": 10
  },
  ""NuGet.Versioning"": {
    ""1.0.0"": 1,
    ""2.0.0"": 5
  },
  ""ZZZ"": {
    ""9.0.0"": 23
  }
}", json);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                CloudBlobClient = new Mock<ICloudBlobClient>();
                CloudBlobContainer = new Mock<ICloudBlobContainer>();
                CloudBlob = new Mock<ISimpleCloudBlob>();
                Options = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                TelemetryService = new Mock<IAzureSearchTelemetryService>();
                Logger = output.GetLogger<DownloadDataClient>();
                Config = new AzureSearchJobConfiguration
                {
                    StorageContainer = "unit-test-container",
                };

                ETag = "\"some-etag\"";
                AccessCondition = new Mock<IAccessCondition>();

                Options
                    .Setup(x => x.Value)
                    .Returns(() => Config);
                CloudBlobClient
                    .Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns(() => CloudBlobContainer.Object);
                CloudBlobContainer
                    .Setup(x => x.GetBlobReference(It.IsAny<string>()))
                    .Returns(() => CloudBlob.Object)
                    .Callback<string>(x => BlobNames.Add(x));
                CloudBlob
                    .Setup(x => x.ETag)
                    .Returns(ETag);
                CloudBlob
                    .Setup(x => x.OpenWriteAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new RecordingStream(bytes =>
                    {
                        SavedBytes.Add(bytes);
                        SavedStrings.Add(Encoding.UTF8.GetString(bytes));
                    }));
                CloudBlob
                    .Setup(x => x.Properties)
                    .Returns(new CloudBlockBlob(new Uri("https://example/blob")).Properties);

                Target = new DownloadDataClient(
                    CloudBlobClient.Object,
                    Options.Object,
                    TelemetryService.Object,
                    Logger);
            }

            public Mock<ICloudBlobClient> CloudBlobClient { get; }
            public Mock<ICloudBlobContainer> CloudBlobContainer { get; }
            public Mock<ISimpleCloudBlob> CloudBlob { get; }
            public Mock<IOptionsSnapshot<AzureSearchJobConfiguration>> Options { get; }
            public Mock<IAzureSearchTelemetryService> TelemetryService { get; }
            public RecordingLogger<DownloadDataClient> Logger { get; }
            public AzureSearchJobConfiguration Config { get; }
            public string ETag { get; }
            public Mock<IAccessCondition> AccessCondition { get; }
            public DownloadDataClient Target { get; }

            public List<string> BlobNames { get; } = new List<string>();
            public List<byte[]> SavedBytes { get; } = new List<byte[]>();
            public List<string> SavedStrings { get; } = new List<string>();
        }

        private class RecordingStream : MemoryStream
        {
            private readonly object _lock = new object();
            private Action<byte[]> _onDispose;

            public RecordingStream(Action<byte[]> onDispose)
            {
                _onDispose = onDispose;
            }

            protected override void Dispose(bool disposing)
            {
                lock (_lock)
                {
                    if (_onDispose != null)
                    {
                        _onDispose(ToArray());
                        _onDispose = null;
                    }
                }

                base.Dispose(disposing);
            }
        }
    }
}

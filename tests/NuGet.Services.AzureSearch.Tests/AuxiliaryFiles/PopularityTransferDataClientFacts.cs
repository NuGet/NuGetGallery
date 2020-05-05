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
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class PopularityTransferDataClientFacts
    {
        public class ReadLatestIndexed : Facts
        {
            public ReadLatestIndexed(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task AllowsEmptyObject()
            {
                var json = JsonConvert.SerializeObject(new Dictionary<string, string[]>());
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestIndexedAsync(AccessCondition.Object, StringCache);

                Assert.Empty(output.Data);
                Assert.Equal(ETag, output.Metadata.ETag);

                TelemetryService.Verify(
                    x => x.TrackReadLatestIndexedPopularityTransfers(
                        /*outgoingTransfers: */ 0,
                        /*modified: */ true,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public async Task AllowsNotModifiedBlob()
            {
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ThrowsAsync(new StorageException(
                        new RequestResult
                        {
                            HttpStatusCode = (int)HttpStatusCode.NotModified,
                        },
                        message: "Not modified.",
                        inner: null));

                var output = await Target.ReadLatestIndexedAsync(AccessCondition.Object, StringCache);

                Assert.False(output.Modified);
                Assert.Null(output.Data);
                Assert.Null(output.Metadata);
            }

            [Fact]
            public async Task ThrowsIfBlobIsMissing()
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

                var ex = await Assert.ThrowsAsync<StorageException>(
                    () => Target.ReadLatestIndexedAsync(AccessCondition.Object, StringCache));

                Assert.Equal((int)HttpStatusCode.NotFound, ex.RequestInformation.HttpStatusCode);
            }

            [Fact]
            public async Task RejectsInvalidJson()
            {
                var json = JsonConvert.SerializeObject(new object[]
                {
                    new object[]
                    {
                        "WindowsAzure.ServiceBus",
                        new[] { "Azure.Messaging.ServiceBus" }
                    },
                    new object[]
                    {
                        "WindowsAzure.Storage",
                        new[] { "Azure.Storage.Blobs", "Azure.Storage.Queues" }
                    }
                });
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.ReadLatestIndexedAsync(AccessCondition.Object, StringCache));
                Assert.Equal("The first token should be the start of an object.", ex.Message);
            }

            [Fact]
            public async Task ReadsPopularityTransfers()
            {
                var json = JsonConvert.SerializeObject(new Dictionary<string, string[]>
                {
                    {
                        "windowsazure.servicebus",
                        new[] { "Azure.Messaging.ServiceBus" }
                    },
                    {
                        "WindowsAzure.Storage",
                        new[] { "Azure.Storage.Blobs", "Azure.Storage.Queues" }
                    },
                    {
                        "ZDuplicate",
                        new[] { "packageA", "packagea", "PACKAGEA", "packageB" }
                    },
                });
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestIndexedAsync(AccessCondition.Object, StringCache);

                Assert.Equal(3, output.Data.Count);
                Assert.Equal(new[] { "windowsazure.servicebus", "WindowsAzure.Storage", "ZDuplicate" }, output.Data.Keys.ToArray());
                Assert.Equal(new[] { "Azure.Messaging.ServiceBus" }, output.Data["windowsazure.servicebus"].ToArray());
                Assert.Equal(new[] { "Azure.Storage.Blobs", "Azure.Storage.Queues" }, output.Data["WindowsAzure.Storage"].ToArray());
                Assert.Equal(new[] { "packageA", "packageB" }, output.Data["ZDuplicate"].ToArray());
                Assert.Equal(ETag, output.Metadata.ETag);

                CloudBlobContainer.Verify(x => x.GetBlobReference("popularity-transfers/popularity-transfers.v1.json"), Times.Once);
                TelemetryService.Verify(
                    x => x.TrackReadLatestIndexedPopularityTransfers(
                        /*outgoingTransfers: */ 3,
                        /*modified: */ true,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public async Task IgnoresEmptyTransferList()
            {
                var json = JsonConvert.SerializeObject(new Dictionary<string, string[]>
                {
                    {
                        "NoTransfers",
                        new string[0]
                    },
                    {
                        "PackageA",
                        new[] { "PackageB" }
                    },
                });
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestIndexedAsync(AccessCondition.Object, StringCache);

                Assert.Single(output.Data);
                Assert.Equal(new[] { "PackageA" }, output.Data.Keys.ToArray());
                Assert.Equal(new[] { "PackageB" }, output.Data["PackageA"].ToArray());
                Assert.Equal(ETag, output.Metadata.ETag);

                TelemetryService.Verify(
                    x => x.TrackReadLatestIndexedPopularityTransfers(
                        /*outgoingTransfers: */ 1,
                        /*modified: */ true,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public async Task AllowsDuplicateIds()
            {
                var json = @"
{
  ""PackageA"": [ ""packageB"" ],
  ""PackageA"": [ ""packageC"" ],
  ""packagea"": [ ""packageD"" ]
}";

                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestIndexedAsync(AccessCondition.Object, StringCache);

                Assert.Single(output.Data);
                Assert.Equal(new[] { "PackageA" }, output.Data.Keys.ToArray());
                Assert.Equal(new[] { "packageB", "packageC", "packageD" }, output.Data["packageA"].ToArray());
                Assert.Equal(ETag, output.Metadata.ETag);

                TelemetryService.Verify(
                    x => x.TrackReadLatestIndexedPopularityTransfers(
                        /*outgoingTransfers: */ 1,
                        /*modified: */ true,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public async Task DedupesStrings()
            {
                var json = @"
{
  ""PackageA"": [ ""PackageB"" ],
  ""PackageB"": [ ""PackageA"" ]
}";

                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestIndexedAsync(AccessCondition.Object, StringCache);

                var transfers = output.Data.ToList();
                var transferA = transfers[0];
                var transferB = transfers[1];

                Assert.Equal(2, StringCache.StringCount);
                Assert.Same(transferA.Key, transferB.Value.First());
                Assert.Same(transferB.Key, transferA.Value.First());
            }
        }

        public class ReplaceLatestIndexed : Facts
        {
            public ReplaceLatestIndexed(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SerializesWithoutBOM()
            {
                var newData = new PopularityTransferData();

                await Target.ReplaceLatestIndexedAsync(newData, AccessCondition.Object);

                var bytes = Assert.Single(SavedBytes);
                Assert.Equal((byte)'{', bytes[0]);
            }

            [Fact]
            public async Task SetsContentType()
            {
                var newData = new PopularityTransferData();

                await Target.ReplaceLatestIndexedAsync(newData, AccessCondition.Object);

                Assert.Equal("application/json", CloudBlob.Object.Properties.ContentType);
            }

            [Fact]
            public async Task SerializedWithoutIndentation()
            {
                var newData = new PopularityTransferData();

                newData.AddTransfer("PackageA", "packageB");
                newData.AddTransfer("PackageA", "packageC");

                await Target.ReplaceLatestIndexedAsync(newData, AccessCondition.Object);

                var json = Assert.Single(SavedStrings);
                Assert.DoesNotContain("\n", json);
            }

            [Fact]
            public async Task SerializesVersionsSortedOrder()
            {
                var newData = new PopularityTransferData();

                newData.AddTransfer("PackageB", "PackageA");
                newData.AddTransfer("PackageB", "PackageB");

                newData.AddTransfer("PackageA", "PackageC");
                newData.AddTransfer("PackageA", "packagec");
                newData.AddTransfer("PackageA", "packageC");
                newData.AddTransfer("PackageA", "PackageB");

                newData.AddTransfer("PackageC", "PackageZ");

                await Target.ReplaceLatestIndexedAsync(newData, AccessCondition.Object);

                // Pretty-ify the JSON to make the assertion clearer.
                var json = Assert.Single(SavedStrings);
                json = JsonConvert.DeserializeObject<JObject>(json).ToString();

                Assert.Equal(@"{
  ""PackageA"": [
    ""PackageB"",
    ""PackageC""
  ],
  ""PackageB"": [
    ""PackageA"",
    ""PackageB""
  ],
  ""PackageC"": [
    ""PackageZ""
  ]
}", json);
                TelemetryService.Verify(
                    x => x.TrackReplaceLatestIndexedPopularityTransfers(
                        /*outgoingTransfers: */ 3),
                    Times.Once);
                ReplaceLatestIndexedPopularityTransfersDurationMetric.Verify(x => x.Dispose(), Times.Once);
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
                Logger = output.GetLogger<PopularityTransferDataClient>();
                Config = new AzureSearchJobConfiguration
                {
                    StorageContainer = "unit-test-container",
                };

                ETag = "\"some-etag\"";
                AccessCondition = new Mock<IAccessCondition>();
                StringCache = new StringCache();
                ReplaceLatestIndexedPopularityTransfersDurationMetric = new Mock<IDisposable>();

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

                TelemetryService
                    .Setup(x => x.TrackReplaceLatestIndexedPopularityTransfers(It.IsAny<int>()))
                    .Returns(ReplaceLatestIndexedPopularityTransfersDurationMetric.Object);

                Target = new PopularityTransferDataClient(
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
            public RecordingLogger<PopularityTransferDataClient> Logger { get; }
            public AzureSearchJobConfiguration Config { get; }
            public string ETag { get; }
            public Mock<IAccessCondition> AccessCondition { get; }
            public StringCache StringCache { get; }
            public Mock<IDisposable> ReplaceLatestIndexedPopularityTransfersDurationMetric { get; }
            public PopularityTransferDataClient Target { get; }


            public List<string> BlobNames { get; } = new List<string>();
            public List<byte[]> SavedBytes { get; } = new List<byte[]>();
            public List<string> SavedStrings { get; } = new List<string>();
        }
    }
}

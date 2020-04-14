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

                var output = await Target.ReadLatestIndexedAsync();

                Assert.Empty(output.Result);
                Assert.Equal(ETag, output.AccessCondition.IfMatchETag);

                TelemetryService.Verify(
                    x => x.TrackReadLatestIndexedPopularityTransfers(
                        /*outgoingTransfers: */ 0,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
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

                TelemetryService.Verify(
                    x => x.TrackReadLatestIndexedPopularityTransfers(
                        /*outgoingTransfers: */ 0,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
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
                    () => Target.ReadLatestIndexedAsync());
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

                var output = await Target.ReadLatestIndexedAsync();

                Assert.Equal(3, output.Result.Count);
                Assert.Equal(new[] { "windowsazure.servicebus", "WindowsAzure.Storage", "ZDuplicate" }, output.Result.Keys.ToArray());
                Assert.Equal(new[] { "Azure.Messaging.ServiceBus" }, output.Result["windowsazure.servicebus"].ToArray());
                Assert.Equal(new[] { "Azure.Storage.Blobs", "Azure.Storage.Queues" }, output.Result["WindowsAzure.Storage"].ToArray());
                Assert.Equal(new[] { "packageA", "packageB" }, output.Result["ZDuplicate"].ToArray());
                Assert.Equal(StringComparer.OrdinalIgnoreCase, output.Result.Comparer);
                Assert.Equal(StringComparer.OrdinalIgnoreCase, output.Result["windowsazure.servicebus"].Comparer);
                Assert.Equal(StringComparer.OrdinalIgnoreCase, output.Result["WindowsAzure.Storage"].Comparer);
                Assert.Equal(StringComparer.OrdinalIgnoreCase, output.Result["ZDuplicate"].Comparer);
                Assert.Equal(ETag, output.AccessCondition.IfMatchETag);

                CloudBlobContainer.Verify(x => x.GetBlobReference("popularity-transfers/popularity-transfers.v1.json"), Times.Once);
                TelemetryService.Verify(
                    x => x.TrackReadLatestIndexedPopularityTransfers(
                        /*outgoingTransfers: */ 3,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public async Task IgnoresEmptyOwnerLists()
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

                var output = await Target.ReadLatestIndexedAsync();

                Assert.Single(output.Result);
                Assert.Equal(new[] { "PackageA" }, output.Result.Keys.ToArray());
                Assert.Equal(new[] { "PackageB" }, output.Result["PackageA"].ToArray());
                Assert.Equal(StringComparer.OrdinalIgnoreCase, output.Result.Comparer);
                Assert.Equal(StringComparer.OrdinalIgnoreCase, output.Result["PackageA"].Comparer);
                Assert.Equal(ETag, output.AccessCondition.IfMatchETag);

                TelemetryService.Verify(
                    x => x.TrackReadLatestIndexedPopularityTransfers(
                        /*outgoingTransfers: */ 1,
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

                var output = await Target.ReadLatestIndexedAsync();

                Assert.Single(output.Result);
                Assert.Equal(new[] { "PackageA" }, output.Result.Keys.ToArray());
                Assert.Equal(new[] { "packageB", "packageC", "packageD" }, output.Result["packageA"].ToArray());
                Assert.Equal(StringComparer.OrdinalIgnoreCase, output.Result.Comparer);
                Assert.Equal(StringComparer.OrdinalIgnoreCase, output.Result["packageA"].Comparer);
                Assert.Equal(ETag, output.AccessCondition.IfMatchETag);

                TelemetryService.Verify(
                    x => x.TrackReadLatestIndexedPopularityTransfers(
                        /*outgoingTransfers: */ 1,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
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
                var newData = new SortedDictionary<string, SortedSet<string>>();

                await Target.ReplaceLatestIndexedAsync(newData, AccessCondition.Object);

                var bytes = Assert.Single(SavedBytes);
                Assert.Equal((byte)'{', bytes[0]);
            }

            [Fact]
            public async Task SetsContentType()
            {
                var newData = new SortedDictionary<string, SortedSet<string>>();

                await Target.ReplaceLatestIndexedAsync(newData, AccessCondition.Object);

                Assert.Equal("application/json", CloudBlob.Object.Properties.ContentType);
            }

            [Fact]
            public async Task SerializedWithoutIndentation()
            {
                var newData = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "PackageA",
                        new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "packageB", "packageC" }
                    }
                };

                await Target.ReplaceLatestIndexedAsync(newData, AccessCondition.Object);

                var json = Assert.Single(SavedStrings);
                Assert.DoesNotContain("\n", json);
            }

            [Fact]
            public async Task SerializesVersionsSortedOrder()
            {
                var newData = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "PackageB",
                        new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "PackageA", "PackageB" }
                    },
                    {
                        "PackageA",
                        new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "PackageC", "packagec", "packageC", "PackageB" }
                    },
                    {
                        "PackageC",
                        new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "PackageZ" }
                    }
                };

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
            public Mock<IDisposable> ReplaceLatestIndexedPopularityTransfersDurationMetric { get; }
            public PopularityTransferDataClient Target { get; }

            public List<string> BlobNames { get; } = new List<string>();
            public List<byte[]> SavedBytes { get; } = new List<byte[]>();
            public List<string> SavedStrings { get; } = new List<string>();
        }
    }
}

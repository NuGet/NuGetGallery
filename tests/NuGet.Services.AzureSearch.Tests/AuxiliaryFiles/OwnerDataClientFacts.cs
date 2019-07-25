// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
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
    public class OwnerDataClientFacts
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
                        new[] { "nuget", "Microsoft" }
                    },
                    new object[]
                    {
                        "EntityFramework",
                        new[] { "Microsoft", "aspnet", "EntityFramework" }
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
            public async Task ReadsOwners()
            {
                var json = JsonConvert.SerializeObject(new Dictionary<string, string[]>
                {
                    {
                        "nuget.versioning",
                        new[] { "nuget", "Microsoft" }
                    },
                    {
                        "EntityFramework",
                        new[] { "Microsoft", "aspnet", "EntityFramework" }
                    },
                    {
                        "NuGet.Core",
                        new[] { "nuget" }
                    },
                    {
                        "ZDuplicate",
                        new[] { "ownerA", "ownera", "OWNERA", "ownerB" }
                    },
                });
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestIndexedAsync();

                Assert.Equal(4, output.Result.Count);
                Assert.Equal(new[] { "EntityFramework", "NuGet.Core", "nuget.versioning", "ZDuplicate" }, output.Result.Keys.ToArray());
                Assert.Equal(new[] { "aspnet", "EntityFramework", "Microsoft" }, output.Result["EntityFramework"].ToArray());
                Assert.Equal(new[] { "nuget" }, output.Result["NuGet.Core"].ToArray());
                Assert.Equal(new[] { "Microsoft", "nuget" }, output.Result["nuget.versioning"].ToArray());
                Assert.Equal(new[] { "ownerA", "ownerB" }, output.Result["ZDuplicate"].ToArray());
                Assert.Equal(ETag, output.AccessCondition.IfMatchETag);

                CloudBlobContainer.Verify(x => x.GetBlobReference("owners/owners.v2.json"), Times.Once);
            }

            [Fact]
            public async Task IgnoresEmptyOwnerLists()
            {
                var json = JsonConvert.SerializeObject(new Dictionary<string, string[]>
                {
                    {
                        "NoOwners",
                        new string[0]
                    },
                    {
                        "NuGet.Core",
                        new[] { "nuget" }
                    },
                });
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestIndexedAsync();

                Assert.Single(output.Result);
                Assert.Equal(new[] { "NuGet.Core" }, output.Result.Keys.ToArray());
                Assert.Equal(new[] { "nuget" }, output.Result["NuGet.Core"].ToArray());
                Assert.Equal(ETag, output.AccessCondition.IfMatchETag);
            }

            [Fact]
            public async Task AllowsDuplicateIdsWithDifferentCase()
            {
                var json = JsonConvert.SerializeObject(new SortedDictionary<string, string[]>(StringComparer.Ordinal)
                {
                    {
                        "NuGet.Core",
                        new[] { "nuget" }
                    },
                    {
                        "nuget.core",
                        new[] { "microsoft" }
                    },
                });
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestIndexedAsync();

                Assert.Single(output.Result);
                Assert.Equal(new[] { "NuGet.Core" }, output.Result.Keys.ToArray());
                Assert.Equal(new[] { "microsoft", "nuget" }, output.Result["NuGet.Core"].ToArray());
                Assert.Equal(ETag, output.AccessCondition.IfMatchETag);
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
                        "nuget.versioning",
                        new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "nuget", "Microsoft" }
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
                        "nuget.versioning",
                        new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "nuget", "Microsoft" }
                    },
                    {
                        "ZDuplicate",
                        new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "ownerA", "ownera", "OWNERA", "ownerB" }
                    },
                    {
                        "EntityFramework",
                        new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "Microsoft", "aspnet", "EntityFramework" }
                    },
                    {
                        "NuGet.Core",
                        new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "nuget" }
                    },
                };

                await Target.ReplaceLatestIndexedAsync(newData, AccessCondition.Object);

                // Pretty-ify the JSON to make the assertion clearer.
                var json = Assert.Single(SavedStrings);
                json = JsonConvert.DeserializeObject<JObject>(json).ToString();

                Assert.Equal(@"{
  ""EntityFramework"": [
    ""aspnet"",
    ""EntityFramework"",
    ""Microsoft""
  ],
  ""NuGet.Core"": [
    ""nuget""
  ],
  ""nuget.versioning"": [
    ""Microsoft"",
    ""nuget""
  ],
  ""ZDuplicate"": [
    ""ownerA"",
    ""ownerB""
  ]
}", json);
            }
        }

        public class UploadChangeHistoryAsync : Facts
        {
            public UploadChangeHistoryAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task RejectsEmptyList()
            {
                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => Target.UploadChangeHistoryAsync(new string[0]));
                Assert.Contains("The list of package IDs must have at least one element.", ex.Message);
            }

            [Fact]
            public async Task SerializesWithoutBOM()
            {
                await Target.UploadChangeHistoryAsync(new[] { "nuget" });

                var bytes = Assert.Single(SavedBytes);
                Assert.Equal((byte)'[', bytes[0]);
            }

            [Fact]
            public async Task SetsContentType()
            {
                await Target.UploadChangeHistoryAsync(new[] { "nuget" });

                Assert.Equal("application/json", CloudBlob.Object.Properties.ContentType);
            }

            [Fact]
            public async Task UsesTimestampAsBlobName()
            {
                var before = DateTimeOffset.UtcNow;
                await Target.UploadChangeHistoryAsync(new[] { "nuget" });
                var after = DateTimeOffset.UtcNow;

                var blobName = Assert.Single(BlobNames);
                var slashIndex = blobName.LastIndexOf('/');
                Assert.True(slashIndex >= 0, "The index of the last slash must not be negative.");

                var directoryName = blobName.Substring(0, slashIndex);
                Assert.Equal("owners/changes", directoryName);

                var fileName = blobName.Substring(slashIndex + 1);
                Assert.EndsWith(".json", fileName);
                var timestamp = DateTimeOffset.ParseExact(
                    fileName,
                    "yyyy-MM-dd-HH-mm-ss-FFFFFFF\\.\\j\\s\\o\\n",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal);
                Assert.InRange(timestamp, before, after);
            }

            [Fact]
            public async Task SerializedWithoutIndentation()
            {
                var input = new[] { "nuget", "Microsoft" };

                await Target.UploadChangeHistoryAsync(input);

                var json = Assert.Single(SavedStrings);
                Assert.DoesNotContain("\n", json);
            }

            [Fact]
            public async Task SerializesInProvidedOrder()
            {
                var input = new[]
                {
                    "ZZZ",
                    "AAA",
                    "B",
                    "B",
                    "z",
                    "00"
                };

                await Target.UploadChangeHistoryAsync(input);

                // Pretty-ify the JSON to make the assertion clearer.
                var json = Assert.Single(SavedStrings);
                json = JsonConvert.DeserializeObject<JArray>(json).ToString();
                Assert.Equal(@"[
  ""ZZZ"",
  ""AAA"",
  ""B"",
  ""B"",
  ""z"",
  ""00""
]", json);
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
                Logger = output.GetLogger<OwnerDataClient>();
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

                Target = new OwnerDataClient(
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
            public RecordingLogger<OwnerDataClient> Logger { get; }
            public AzureSearchJobConfiguration Config { get; }
            public string ETag { get; }
            public Mock<IAccessCondition> AccessCondition { get; }
            public OwnerDataClient Target { get; }

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

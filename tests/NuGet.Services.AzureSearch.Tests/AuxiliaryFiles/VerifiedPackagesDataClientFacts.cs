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
using NuGet.Services.AzureSearch.Support;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class VerifiedPackagesDataClientFacts
    {
        public class ReadLatestAsync : Facts
        {
            public ReadLatestAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task AllowsEmptyObject()
            {
                var json = JsonConvert.SerializeObject(new HashSet<string>());
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestAsync(AccessCondition.Object, StringCache);

                Assert.True(output.Modified);
                Assert.Empty(output.Data);
                Assert.Equal(ETag, output.Metadata.ETag);
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

                var output = await Target.ReadLatestAsync(AccessCondition.Object, StringCache);

                Assert.False(output.Modified);
                Assert.Null(output.Data);
                Assert.Null(output.Metadata);
            }

            [Fact]
            public async Task RejectsMissingBlob()
            {
                var expected = new StorageException(
                    new RequestResult
                    {
                        HttpStatusCode = (int)HttpStatusCode.NotFound,
                    },
                    message: "Not found.",
                    inner: null);
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ThrowsAsync(expected);

                var actual = await Assert.ThrowsAsync<StorageException>(
                    () => Target.ReadLatestAsync(AccessCondition.Object, StringCache));
                Assert.Same(actual, expected);
            }

            [Fact]
            public async Task RejectsInvalidJson()
            {
                var json = JsonConvert.SerializeObject(new
                {
                    Version = 7,
                    Ids = new[] { "nuget.versioning", "EntityFramework" },
                });
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));


                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.ReadLatestAsync(AccessCondition.Object, StringCache));
                Assert.Equal("The first token should be the start of an array.", ex.Message);
            }

            [Fact]
            public async Task ReadsVerifiedPackages()
            {
                var json = JsonConvert.SerializeObject(new[]
                {
                    "nuget.versioning",
                    "EntityFramework",
                    "NuGet.Core",
                });
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestAsync(AccessCondition.Object, StringCache);

                Assert.True(output.Modified);
                Assert.Equal(new[] { "EntityFramework", "NuGet.Core", "nuget.versioning" }, output.Data.OrderBy(x => x).ToArray());
                Assert.Equal(ETag, output.Metadata.ETag);
                CloudBlobContainer.Verify(x => x.GetBlobReference("verified-packages/verified-packages.v1.json"), Times.Once);
            }

            [Fact]
            public async Task AllowsDuplicateIdsWithDifferentCase()
            {
                var json = JsonConvert.SerializeObject(new[]
                {
                    "NuGet.Core",
                    "nuget.core",
                });
                CloudBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(json)));

                var output = await Target.ReadLatestAsync(AccessCondition.Object, StringCache);

                Assert.True(output.Modified);
                Assert.Single(output.Data);
                Assert.Equal(new[] { "NuGet.Core" }, output.Data.ToArray());
                Assert.Equal(ETag, output.Metadata.ETag);
            }
        }

        public class ReplaceLatestAsync : Facts
        {
            public ReplaceLatestAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SerializesWithoutBOM()
            {
                var newData = new HashSet<string>();

                await Target.ReplaceLatestAsync(newData, AccessCondition.Object);

                var bytes = Assert.Single(SavedBytes);
                Assert.Equal((byte)'[', bytes[0]);
            }

            [Fact]
            public async Task SetsContentType()
            {
                var newData = new HashSet<string>();

                await Target.ReplaceLatestAsync(newData, AccessCondition.Object);

                Assert.Equal("application/json", CloudBlob.Object.Properties.ContentType);
            }

            [Fact]
            public async Task SerializedWithoutIndentation()
            {
                var newData = new HashSet<string>
                {
                    "nuget.versioning",
                    "NuGet.Core",
                };

                await Target.ReplaceLatestAsync(newData, AccessCondition.Object);

                var json = Assert.Single(SavedStrings);
                Assert.DoesNotContain("\n", json);
            }

            [Fact]
            public async Task SerializesPackageIds()
            {
                var newData = new HashSet<string>
                {
                    "nuget.versioning",
                    "EntityFramework",
                    "NuGet.Core",
                };

                await Target.ReplaceLatestAsync(newData, AccessCondition.Object);

                // Pretty-ify and sort the JSON to make the assertion clearer.
                var json = Assert.Single(SavedStrings);
                var array = JsonConvert
                    .DeserializeObject<string[]>(json)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                json = JsonConvert.SerializeObject(array, Formatting.Indented);

                Assert.Equal(@"[
  ""EntityFramework"",
  ""NuGet.Core"",
  ""nuget.versioning""
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
                Options = new Mock<IOptionsSnapshot<AzureSearchConfiguration>>();
                TelemetryService = new Mock<IAzureSearchTelemetryService>();
                Logger = output.GetLogger<VerifiedPackagesDataClient>();
                Config = new AzureSearchConfiguration
                {
                    StorageContainer = "unit-test-container",
                };

                ETag = "\"some-etag\"";
                AccessCondition = new Mock<IAccessCondition>();
                StringCache = new StringCache();

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

                Target = new VerifiedPackagesDataClient(
                    CloudBlobClient.Object,
                    Options.Object,
                    TelemetryService.Object,
                    Logger);
            }

            public Mock<ICloudBlobClient> CloudBlobClient { get; }
            public Mock<ICloudBlobContainer> CloudBlobContainer { get; }
            public Mock<ISimpleCloudBlob> CloudBlob { get; }
            public Mock<IOptionsSnapshot<AzureSearchConfiguration>> Options { get; }
            public Mock<IAzureSearchTelemetryService> TelemetryService { get; }
            public RecordingLogger<VerifiedPackagesDataClient> Logger { get; }
            public AzureSearchConfiguration Config { get; }
            public string ETag { get; }
            public Mock<IAccessCondition> AccessCondition { get; }
            public StringCache StringCache { get; }
            public VerifiedPackagesDataClient Target { get; }

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

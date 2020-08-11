// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using Newtonsoft.Json;
using NuGet.Protocol;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Services;
using NuGet.Versioning;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Jobs.Catalog2Registration
{
    public class HiveStorageFacts
    {
        public class ReadIndexOrNullAsync : Facts
        {
            public ReadIndexOrNullAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ReturnsNullFor404()
            {
                LegacyBlob
                    .Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>()))
                    .Throws(new StorageException(new RequestResult { HttpStatusCode = (int)HttpStatusCode.NotFound }, "Missing.", inner: null));

                var index = await Target.ReadIndexOrNullAsync(HiveType.Legacy, "NuGet.Versioning");

                Assert.Null(index);
            }

            [Fact]
            public async Task DeserializesGzipped()
            {
                GzippedBlob.Object.Properties.ContentEncoding = "gzip";
                var commitId = "6c6330d8-6d49-4c51-9a82-398a04f9e448";
                GzippedStream = SerializeToStream(new { commitId }, gzipped: true);

                var index = await Target.ReadIndexOrNullAsync(HiveType.Gzipped, "NuGet.Versioning");

                Assert.Equal(commitId, index.CommitId);
            }

            [Fact]
            public async Task ProvidesUnencodedStringToStorage()
            {
                var index = await Target.ReadIndexOrNullAsync(HiveType.Legacy, "测试更新包");

                LegacyContainer.Verify(x => x.GetBlobReference("测试更新包/index.json"), Times.Once);
            }

            [Theory]
            [MemberData(nameof(AllHivesTestData))]
            public async Task DeserializesAllHiveTypes(HiveType hive)
            {
                var index = await Target.ReadIndexOrNullAsync(hive, "NuGet.Versioning");

                Assert.NotNull(index);
                CloudBlobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Once);
                CloudBlobClient.Verify(x => x.GetContainerReference(GetContainerName(hive)), Times.Once);
                var container = GetContainer(hive);
                container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                container.Verify(x => x.GetBlobReference("nuget.versioning/index.json"), Times.Once);
                var blob = GetBlob(hive);
                blob.Verify(x => x.OpenReadAsync(It.IsAny<AccessCondition>()), Times.Once);
                blob.Verify(x => x.OpenReadAsync(It.Is<AccessCondition>(a => a.IfMatchETag == null && a.IfNoneMatchETag == null)), Times.Once);
                Assert.Equal(blob.Object.Uri.AbsoluteUri, index.Url);
            }
        }

        public class ReadPageAsync : Facts
        {
            public ReadPageAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ThrowsFor404()
            {
                var expected = new StorageException(
                    new RequestResult { HttpStatusCode = (int)HttpStatusCode.NotFound },
                    "Missing.",
                    inner: null);
                LegacyBlob.Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>())).Throws(expected);
                var url = "https://example/reg/nuget.versioning/0.0.1/0.0.2.json";

                var actual = await Assert.ThrowsAsync<StorageException>(
                    () => Target.ReadPageAsync(HiveType.Legacy, url));
                Assert.Same(expected, actual);
            }

            [Fact]
            public async Task DeserializesGzipped()
            {
                GzippedBlob.Object.Properties.ContentEncoding = "gzip";
                var commitId = "6c6330d8-6d49-4c51-9a82-398a04f9e448";
                GzippedStream = SerializeToStream(new { commitId }, gzipped: true);

                var index = await Target.ReadPageAsync(HiveType.Gzipped, "https://example/reg-gz/nuget.versioning/0.0.1/0.0.2.json");

                Assert.Equal(commitId, index.CommitId);
            }

            [Fact]
            public async Task ProvidesUnencodedStringToStorage()
            {
                var url = "https://example/reg/测试更新包/0.0.1/0.0.2.json";

                var index = await Target.ReadPageAsync(HiveType.Legacy, url);

                LegacyContainer.Verify(x => x.GetBlobReference("测试更新包/0.0.1/0.0.2.json"), Times.Once);
            }

            [Fact]
            public async Task RejectsMismatchingBaseUrl()
            {
                var url = "https://example/reg-gz/nuget.versioning/0.0.1/0.0.2.json";

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.ReadPageAsync(HiveType.Legacy, url));
                Assert.Equal($"URL '{url}' does not start with expected base URL 'https://example/reg/'.", ex.Message);
            }

            [Theory]
            [MemberData(nameof(AllHivesTestData))]
            public async Task DeserializesAllHiveTypes(HiveType hive)
            {
                var index = await Target.ReadPageAsync(hive, GetBaseUrl(hive) + "nuget.versioning/0.0.1/0.0.2.json");

                Assert.NotNull(index);
                CloudBlobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Once);
                CloudBlobClient.Verify(x => x.GetContainerReference(GetContainerName(hive)), Times.Once);
                var container = GetContainer(hive);
                container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                container.Verify(x => x.GetBlobReference("nuget.versioning/0.0.1/0.0.2.json"), Times.Once);
                var blob = GetBlob(hive);
                blob.Verify(x => x.OpenReadAsync(It.IsAny<AccessCondition>()), Times.Once);
                blob.Verify(x => x.OpenReadAsync(It.Is<AccessCondition>(a => a.IfMatchETag == null && a.IfNoneMatchETag == null)), Times.Once);
                Assert.Equal(blob.Object.Uri.AbsoluteUri, index.Url);
            }
        }

        public class WriteIndexAsync : Facts
        {
            public WriteIndexAsync(ITestOutputHelper output) : base(output)
            {
                LegacyStream = new MemoryStream();
                GzippedStream = new MemoryStream();
                SemVer2Stream = new MemoryStream();
            }

            [Fact]
            public async Task SerializesIndex()
            {
                await Target.WriteIndexAsync(Hive, ReplicaHives, Id, Index);

                var json = Encoding.UTF8.GetString(LegacyStream.ToArray());
                Assert.Equal("{\"commitTimeStamp\":\"0001-01-01T00:00:00+00:00\",\"count\":0}", json);
                LegacyContainer.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                LegacyContainer.Verify(x => x.GetBlobReference("nuget.versioning/index.json"), Times.Once);
                LegacyBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                LegacyBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.Is<AccessCondition>(a => a.IfMatchETag == null && a.IfNoneMatchETag == null)), Times.Once);
                GzippedContainer.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Never);
                SemVer2Container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task WritesToReplicaHives()
            {
                ReplicaHives.Add(HiveType.Gzipped);
                ReplicaHives.Add(HiveType.SemVer2);

                await Target.WriteIndexAsync(Hive, ReplicaHives, Id, Index);
                LegacyBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                GzippedBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                SemVer2Blob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                EntityBuilder.Verify(x => x.UpdateIndexUrls(It.IsAny<RegistrationIndex>(), It.IsAny<HiveType>(), It.IsAny<HiveType>()), Times.Exactly(3));
                EntityBuilder.Verify(x => x.UpdateIndexUrls(Index, HiveType.Legacy, HiveType.Gzipped), Times.Once);
                EntityBuilder.Verify(x => x.UpdateIndexUrls(Index, HiveType.Gzipped, HiveType.SemVer2), Times.Once);
                EntityBuilder.Verify(x => x.UpdateIndexUrls(Index, HiveType.SemVer2, HiveType.Legacy), Times.Once);
            }

            [Theory]
            [InlineData(HiveType.Legacy, false)]
            [InlineData(HiveType.Gzipped, true)]
            [InlineData(HiveType.SemVer2, true)]
            public async Task CompressesContent(HiveType hive, bool isGzipped)
            {
                await Target.WriteIndexAsync(hive, ReplicaHives, Id, Index);

                var stream = GetStream(hive);
                var bytes = stream.ToArray();
                if (isGzipped)
                {
                    var output = new MemoryStream();
                    using (var content = new MemoryStream(bytes))
                    using (var gzipStream = new GZipStream(content, CompressionMode.Decompress))
                    {
                        gzipStream.CopyTo(output);
                    }

                    bytes = output.ToArray();
                }
                var json = Encoding.UTF8.GetString(bytes);
                Assert.Equal("{\"commitTimeStamp\":\"0001-01-01T00:00:00+00:00\",\"count\":0}", json);
            }

            [Theory]
            [InlineData(HiveType.Legacy, null)]
            [InlineData(HiveType.Gzipped, "gzip")]
            [InlineData(HiveType.SemVer2, "gzip")]
            public async Task SetsProperties(HiveType hive, string contentEncoding)
            {
                await Target.WriteIndexAsync(hive, ReplicaHives, Id, Index);

                var blob = GetBlob(hive);
                Assert.Equal("application/json", blob.Object.Properties.ContentType);
                Assert.Equal("no-store", blob.Object.Properties.CacheControl);
                Assert.Equal(contentEncoding, blob.Object.Properties.ContentEncoding);
            }

            [Fact]
            public async Task SnapshotsBlobWhenConfiguredAndMissingSnapshot()
            {
                Config.EnsureSingleSnapshot = true;
                LegacySegment.Setup(x => x.Results).Returns(() => new[] { LegacyBlob.Object });

                await Target.WriteIndexAsync(Hive, ReplicaHives, Id, Index);
                LegacyBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                LegacyContainer.Verify(
                    x => x.ListBlobsSegmentedAsync(
                        "nuget.versioning/index.json",
                        true,
                        BlobListingDetails.Snapshots,
                        2,
                        null,
                        null,
                        null,
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                LegacyBlob.Verify(x => x.SnapshotAsync(It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task DoesNotSnapshotBlobWhenConfiguredAndAlreadyHasSnapshot()
            {
                Config.EnsureSingleSnapshot = true;
                LegacySegment.Setup(x => x.Results).Returns(() => new[] { LegacyBlob.Object, LegacyBlob.Object });

                await Target.WriteIndexAsync(Hive, ReplicaHives, Id, Index);
                LegacyBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                LegacyContainer.Verify(
                    x => x.ListBlobsSegmentedAsync(
                        "nuget.versioning/index.json",
                        true,
                        BlobListingDetails.Snapshots,
                        2,
                        null,
                        null,
                        null,
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                LegacyBlob.Verify(x => x.SnapshotAsync(It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task DoesNotListOrSnapshotBlobWhenNotConfiguredToSnapshot()
            {
                Config.EnsureSingleSnapshot = false;

                await Target.WriteIndexAsync(Hive, ReplicaHives, Id, Index);
                LegacyBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                LegacyContainer.Verify(
                    x => x.ListBlobsSegmentedAsync(
                        It.IsAny<string>(),
                        It.IsAny<bool>(),
                        It.IsAny<BlobListingDetails>(),
                        It.IsAny<int?>(),
                        It.IsAny<BlobContinuationToken>(),
                        It.IsAny<BlobRequestOptions>(),
                        It.IsAny<OperationContext>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                LegacyBlob.Verify(x => x.SnapshotAsync(It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        public class WritePageAsync : Facts
        {
            public WritePageAsync(ITestOutputHelper output) : base(output)
            {
                LegacyStream = new MemoryStream();
                GzippedStream = new MemoryStream();
                SemVer2Stream = new MemoryStream();
                Lower = NuGetVersion.Parse("1.0.0");
                Upper = NuGetVersion.Parse("2.0.0");
            }

            public NuGetVersion Lower { get; }
            public NuGetVersion Upper { get; }

            [Fact]
            public async Task SerializesPage()
            {
                await Target.WritePageAsync(Hive, ReplicaHives, Id, Lower, Upper, Page);

                var json = Encoding.UTF8.GetString(LegacyStream.ToArray());
                Assert.Equal("{\"commitTimeStamp\":\"0001-01-01T00:00:00+00:00\",\"count\":0}", json);
                LegacyContainer.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                LegacyContainer.Verify(x => x.GetBlobReference("nuget.versioning/page/1.0.0/2.0.0.json"), Times.Once);
                LegacyBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                LegacyBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.Is<AccessCondition>(a => a.IfMatchETag == null && a.IfNoneMatchETag == null)), Times.Once);
                GzippedContainer.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Never);
                SemVer2Container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task WritesToReplicaHives()
            {
                ReplicaHives.Add(HiveType.Gzipped);
                ReplicaHives.Add(HiveType.SemVer2);

                await Target.WritePageAsync(Hive, ReplicaHives, Id, Lower, Upper, Page);
                LegacyBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                GzippedBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                SemVer2Blob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                EntityBuilder.Verify(x => x.UpdatePageUrls(It.IsAny<RegistrationPage>(), It.IsAny<HiveType>(), It.IsAny<HiveType>()), Times.Exactly(3));
                EntityBuilder.Verify(x => x.UpdatePageUrls(Page, HiveType.Legacy, HiveType.Gzipped), Times.Once);
                EntityBuilder.Verify(x => x.UpdatePageUrls(Page, HiveType.Gzipped, HiveType.SemVer2), Times.Once);
                EntityBuilder.Verify(x => x.UpdatePageUrls(Page, HiveType.SemVer2, HiveType.Legacy), Times.Once);
            }
        }

        public class WriteLeafAsync : Facts
        {
            public WriteLeafAsync(ITestOutputHelper output) : base(output)
            {
                LegacyStream = new MemoryStream();
                GzippedStream = new MemoryStream();
                SemVer2Stream = new MemoryStream();
                Version = NuGetVersion.Parse("1.0.0");
            }

            public NuGetVersion Version { get; }

            [Fact]
            public async Task SerializesPage()
            {
                await Target.WriteLeafAsync(Hive, ReplicaHives, Id, Version, Leaf);

                var json = Encoding.UTF8.GetString(LegacyStream.ToArray());
                Assert.Equal("{}", json);
                LegacyContainer.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Once);
                LegacyContainer.Verify(x => x.GetBlobReference("nuget.versioning/1.0.0.json"), Times.Once);
                LegacyBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                LegacyBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.Is<AccessCondition>(a => a.IfMatchETag == null && a.IfNoneMatchETag == null)), Times.Once);
                GzippedContainer.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Never);
                SemVer2Container.Verify(x => x.GetBlobReference(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task WritesToReplicaHives()
            {
                ReplicaHives.Add(HiveType.Gzipped);
                ReplicaHives.Add(HiveType.SemVer2);

                await Target.WriteLeafAsync(Hive, ReplicaHives, Id, Version, Leaf);

                LegacyBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                GzippedBlob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                SemVer2Blob.Verify(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()), Times.Once);
                EntityBuilder.Verify(x => x.UpdateLeafUrls(It.IsAny<RegistrationLeaf>(), It.IsAny<HiveType>(), It.IsAny<HiveType>()), Times.Exactly(3));
                EntityBuilder.Verify(x => x.UpdateLeafUrls(Leaf, HiveType.Legacy, HiveType.Gzipped), Times.Once);
                EntityBuilder.Verify(x => x.UpdateLeafUrls(Leaf, HiveType.Gzipped, HiveType.SemVer2), Times.Once);
                EntityBuilder.Verify(x => x.UpdateLeafUrls(Leaf, HiveType.SemVer2, HiveType.Legacy), Times.Once);
            }
        }

        public class DeleteIndexAsync : Facts
        {
            public DeleteIndexAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task DeletesBlobFromHivesAndReplicaHives()
            {
                ReplicaHives.Add(HiveType.Gzipped);
                ReplicaHives.Add(HiveType.SemVer2);

                await Target.DeleteIndexAsync(Hive, ReplicaHives, Id);

                LegacyContainer.Verify(x => x.GetBlobReference("nuget.versioning/index.json"), Times.Once);
                GzippedContainer.Verify(x => x.GetBlobReference("nuget.versioning/index.json"), Times.Once);
                SemVer2Container.Verify(x => x.GetBlobReference("nuget.versioning/index.json"), Times.Once);
                LegacyBlob.Verify(x => x.DeleteIfExistsAsync(), Times.Once);
                GzippedBlob.Verify(x => x.DeleteIfExistsAsync(), Times.Once);
                SemVer2Blob.Verify(x => x.DeleteIfExistsAsync(), Times.Once);
            }

            [Fact]
            public async Task DoesNotDeleteNonExistentBlob()
            {
                LegacyBlob.Setup(x => x.ExistsAsync()).ReturnsAsync(false);

                await Target.DeleteIndexAsync(Hive, ReplicaHives, Id);

                LegacyContainer.Verify(x => x.GetBlobReference("nuget.versioning/index.json"), Times.Once);
                LegacyBlob.Verify(x => x.ExistsAsync(), Times.Once);
                LegacyBlob.Verify(x => x.DeleteIfExistsAsync(), Times.Never);
            }
        }

        public class DeleteUrlAsync : Facts
        {
            public DeleteUrlAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task DeletesBlobFromHivesAndReplicaHives()
            {
                ReplicaHives.Add(HiveType.Gzipped);
                ReplicaHives.Add(HiveType.SemVer2);

                await Target.DeleteUrlAsync(Hive, ReplicaHives, "https://example/reg/nuget.versioning/1.0.0.json");

                LegacyContainer.Verify(x => x.GetBlobReference("nuget.versioning/1.0.0.json"), Times.Once);
                GzippedContainer.Verify(x => x.GetBlobReference("nuget.versioning/1.0.0.json"), Times.Once);
                SemVer2Container.Verify(x => x.GetBlobReference("nuget.versioning/1.0.0.json"), Times.Once);
                LegacyBlob.Verify(x => x.DeleteIfExistsAsync(), Times.Once);
                GzippedBlob.Verify(x => x.DeleteIfExistsAsync(), Times.Once);
                SemVer2Blob.Verify(x => x.DeleteIfExistsAsync(), Times.Once);
            }

            [Fact]
            public async Task DoesNotDeleteNonExistentBlob()
            {
                LegacyBlob.Setup(x => x.ExistsAsync()).ReturnsAsync(false);

                await Target.DeleteUrlAsync(Hive, ReplicaHives, "https://example/reg/nuget.versioning/1.0.0.json");

                LegacyContainer.Verify(x => x.GetBlobReference("nuget.versioning/1.0.0.json"), Times.Once);
                LegacyBlob.Verify(x => x.ExistsAsync(), Times.Once);
                LegacyBlob.Verify(x => x.DeleteIfExistsAsync(), Times.Never);
            }

            [Fact]
            public async Task RejectsMismatchingBaseUrl()
            {
                var url = "https://example/reg-gz/nuget.versioning/0.0.1/0.0.2.json";

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.DeleteUrlAsync(Hive, ReplicaHives, url));
                Assert.Equal($"URL '{url}' does not start with expected base URL 'https://example/reg/'.", ex.Message);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                CloudBlobClient = new Mock<ICloudBlobClient>();
                EntityBuilder = new Mock<IEntityBuilder>();
                Throttle = new Mock<IThrottle>();
                Options = new Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();
                Logger = output.GetLogger<HiveStorage>();

                Config = new Catalog2RegistrationConfiguration
                {
                    LegacyBaseUrl = "https://example/reg/",
                    LegacyStorageContainer = "reg",
                    GzippedBaseUrl = "https://example/reg-gz/",
                    GzippedStorageContainer = "reg-gz",
                    SemVer2BaseUrl = "https://example/reg-gz-semver2/",
                    SemVer2StorageContainer = "reg-gz-semver2",
                    EnsureSingleSnapshot = false,
                };
                LegacyContainer = new Mock<ICloudBlobContainer>();
                GzippedContainer = new Mock<ICloudBlobContainer>();
                SemVer2Container = new Mock<ICloudBlobContainer>();
                LegacyBlob = new Mock<ISimpleCloudBlob>();
                GzippedBlob = new Mock<ISimpleCloudBlob>();
                SemVer2Blob = new Mock<ISimpleCloudBlob>();
                LegacyStream = new MemoryStream();
                GzippedStream = new MemoryStream();
                SemVer2Stream = new MemoryStream();
                LegacySegment = new Mock<ISimpleBlobResultSegment>();
                Hive = HiveType.Legacy;
                ReplicaHives = new List<HiveType>();
                Id = "NuGet.Versioning";
                Index = new RegistrationIndex();
                Page = new RegistrationPage();
                Leaf = new RegistrationLeaf();

                Options.Setup(x => x.Value).Returns(() => Config);
                CloudBlobClient.Setup(x => x.GetContainerReference(Config.LegacyStorageContainer)).Returns(() => LegacyContainer.Object);
                CloudBlobClient.Setup(x => x.GetContainerReference(Config.GzippedStorageContainer)).Returns(() => GzippedContainer.Object);
                CloudBlobClient.Setup(x => x.GetContainerReference(Config.SemVer2StorageContainer)).Returns(() => SemVer2Container.Object);
                LegacyContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(() => LegacyBlob.Object);
                GzippedContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(() => GzippedBlob.Object);
                SemVer2Container.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(() => SemVer2Blob.Object);
                LegacyBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                LegacyBlob.Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>())).ReturnsAsync(() => LegacyStream);
                LegacyBlob.Setup(x => x.Uri).Returns(new Uri("https://example/reg/something.json"));
                LegacyBlob
                    .Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()))
                    .Returns(Task.CompletedTask)
                    .Callback<Stream, AccessCondition>((s, _) => s.CopyTo(LegacyStream));
                LegacyBlob.Setup(x => x.ExistsAsync()).ReturnsAsync(true);
                LegacyContainer
                    .Setup(x => x.ListBlobsSegmentedAsync(
                        It.IsAny<string>(),
                        It.IsAny<bool>(),
                        It.IsAny<BlobListingDetails>(),
                        It.IsAny<int?>(),
                        It.IsAny<BlobContinuationToken>(),
                        It.IsAny<BlobRequestOptions>(),
                        It.IsAny<OperationContext>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(() => Task.FromResult(LegacySegment.Object));
                LegacySegment.Setup(x => x.Results).Returns(new List<ISimpleCloudBlob>());
                GzippedBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                GzippedBlob.Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>())).ReturnsAsync(() => GzippedStream);
                GzippedBlob.Setup(x => x.Uri).Returns(new Uri("https://example/reg-gz/something.json"));
                GzippedBlob
                    .Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()))
                    .Returns(Task.CompletedTask)
                    .Callback<Stream, AccessCondition>((s, _) => s.CopyTo(GzippedStream));
                GzippedBlob.Setup(x => x.ExistsAsync()).ReturnsAsync(true);
                SemVer2Blob.Setup(x => x.Properties).Returns(new BlobProperties());
                SemVer2Blob.Setup(x => x.OpenReadAsync(It.IsAny<AccessCondition>())).ReturnsAsync(() => SemVer2Stream);
                SemVer2Blob.Setup(x => x.Uri).Returns(new Uri("https://example/reg-gz-semver2/something.json"));
                SemVer2Blob
                    .Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()))
                    .Returns(Task.CompletedTask)
                    .Callback<Stream, AccessCondition>((s, _) => s.CopyTo(SemVer2Stream));
                SemVer2Blob.Setup(x => x.ExistsAsync()).ReturnsAsync(true);

                SerializeToStream(LegacyStream, new Dictionary<string, string> { { "@id", LegacyBlob.Object.Uri.AbsoluteUri } });
                SerializeToStream(GzippedStream, new Dictionary<string, string> { { "@id", GzippedBlob.Object.Uri.AbsoluteUri } });
                SerializeToStream(SemVer2Stream, new Dictionary<string, string> { { "@id", SemVer2Blob.Object.Uri.AbsoluteUri } });

                Target = new HiveStorage(
                    CloudBlobClient.Object,
                    new RegistrationUrlBuilder(Options.Object),
                    EntityBuilder.Object,
                    Throttle.Object,
                    Options.Object,
                    Logger);
            }

            public Mock<ICloudBlobClient> CloudBlobClient { get; }
            public Mock<IEntityBuilder> EntityBuilder { get; }
            public Mock<IThrottle> Throttle { get; }
            public Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>> Options { get; }
            public RecordingLogger<HiveStorage> Logger { get; }
            public Catalog2RegistrationConfiguration Config { get; }
            public Mock<ICloudBlobContainer> LegacyContainer { get; }
            public Mock<ICloudBlobContainer> GzippedContainer { get; }
            public Mock<ICloudBlobContainer> SemVer2Container { get; }
            public Mock<ISimpleCloudBlob> LegacyBlob { get; }
            public Mock<ISimpleCloudBlob> GzippedBlob { get; }
            public Mock<ISimpleCloudBlob> SemVer2Blob { get; }
            public MemoryStream LegacyStream { get; set; }
            public MemoryStream GzippedStream { get; set; }
            public MemoryStream SemVer2Stream { get; set;  }
            public Mock<ISimpleBlobResultSegment> LegacySegment { get; }
            public HiveType Hive { get; set; }
            public List<HiveType> ReplicaHives { get; }
            public string Id { get; }
            public RegistrationIndex Index { get; }
            public RegistrationPage Page { get; }
            public RegistrationLeaf Leaf { get; }
            public HiveStorage Target { get; }

            public MemoryStream SerializeToStream(object obj, bool gzipped = false)
            {
                var memoryStream = new MemoryStream();
                SerializeToStream(memoryStream, obj, gzipped);
                return memoryStream;
            }

            public void SerializeToStream(MemoryStream stream, object obj, bool gzipped = false)
            {
                var json = JsonConvert.SerializeObject(
                    obj,
                    NuGetJsonSerialization.Settings);
                var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);

                if (gzipped)
                {
                    var memoryStream = new MemoryStream();
                    using (memoryStream)
                    using (var gzip = new GZipStream(memoryStream, CompressionMode.Compress))
                    {
                        gzip.Write(bytes, 0, bytes.Length);
                    }

                    bytes = memoryStream.ToArray();
                }

                stream.Write(bytes, 0, bytes.Length);
                stream.Position -= bytes.Length;
            }

            public static IEnumerable<HiveType> AllHives => Enum
                .GetValues(typeof(HiveType))
                .Cast<HiveType>();

            public string GetBaseUrl(HiveType hive) => HiveToBaseUrl[hive](this);
            public string GetContainerName(HiveType hive) => HiveToContainerName[hive](this);
            public Mock<ICloudBlobContainer> GetContainer(HiveType hive) => HiveToContainer[hive](this);
            public Mock<ISimpleCloudBlob> GetBlob(HiveType hive) => HiveToBlob[hive](this);
            public MemoryStream GetStream(HiveType hive) => HiveToStream[hive](this);

            public static IReadOnlyDictionary<HiveType, Func<Facts, string>> HiveToBaseUrl
                = new Dictionary<HiveType, Func<Facts, string>>
                {
                    { HiveType.Legacy, c => c.Config.LegacyBaseUrl },
                    { HiveType.Gzipped, c => c.Config.GzippedBaseUrl },
                    { HiveType.SemVer2, c => c.Config.SemVer2BaseUrl },
                };

            public static IReadOnlyDictionary<HiveType, Func<Facts, string>> HiveToContainerName
                = new Dictionary<HiveType, Func<Facts, string>>
                {
                    { HiveType.Legacy, c => c.Config.LegacyStorageContainer },
                    { HiveType.Gzipped, c => c.Config.GzippedStorageContainer },
                    { HiveType.SemVer2, c => c.Config.SemVer2StorageContainer },
                };

            public static IReadOnlyDictionary<HiveType, Func<Facts, Mock<ICloudBlobContainer>>> HiveToContainer
                = new Dictionary<HiveType, Func<Facts, Mock<ICloudBlobContainer>>>
                {
                    { HiveType.Legacy, c => c.LegacyContainer },
                    { HiveType.Gzipped, c => c.GzippedContainer },
                    { HiveType.SemVer2, c => c.SemVer2Container },
                };

            public static IReadOnlyDictionary<HiveType, Func<Facts, Mock<ISimpleCloudBlob>>> HiveToBlob
                = new Dictionary<HiveType, Func<Facts, Mock<ISimpleCloudBlob>>>
                {
                    { HiveType.Legacy, c => c.LegacyBlob },
                    { HiveType.Gzipped, c => c.GzippedBlob },
                    { HiveType.SemVer2, c => c.SemVer2Blob },
                };

            public static IReadOnlyDictionary<HiveType, Func<Facts, MemoryStream>> HiveToStream
                = new Dictionary<HiveType, Func<Facts, MemoryStream>>
                {
                    { HiveType.Legacy, c => c.LegacyStream },
                    { HiveType.Gzipped, c => c.GzippedStream },
                    { HiveType.SemVer2, c => c.SemVer2Stream },
                };

            public static IEnumerable<object[]> AllHivesTestData => AllHives
                .Select(x => new object[] { x });
        }
    }
}

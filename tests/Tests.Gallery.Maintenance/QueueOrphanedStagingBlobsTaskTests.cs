// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Gallery.Maintenance;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery;
using Xunit;

namespace Tests.Gallery.Maintenance
{
    /// <summary>
    /// Verifies orphan discovery boundaries, retained references, and conditional cleanup handoff.
    /// </summary>
    public class QueueOrphanedStagingBlobsTaskTests
    {
        [Fact]
        public async Task DoesNotFailBeforeTheFirstStagingUploadCreatesTheContainer()
        {
            var context = new TestContext();
            context.Container.Setup(container => container.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

            await context.Target.ProcessAsync(context.Entities.Object, context.Container.Object, context.Now);

            context.Container.Verify(container => container.GetBlobsAsync(
                It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            context.Entities.Verify(entities => entities.SaveChangesAsync(), Times.Never);
        }

        [Theory]
        [InlineData(-1, true)]
        [InlineData(0, false)]
        public async Task QueuesOnlyFilesStrictlyOlderThan24Hours(int secondsFromCutoff, bool expected)
        {
            var context = new TestContext();
            context.SetPages(new[] { CreateBlob("orphan.snupkg", context.Now.AddHours(-24).AddSeconds(secondsFromCutoff)) });

            await context.Target.ProcessAsync(context.Entities.Object, context.Container.Object, context.Now);

            if (expected)
            {
                var request = Assert.Single(context.Requests);
                Assert.Null(request.StagedPackageIdentityKey);
                Assert.Equal("orphan.snupkg", request.BlobPath);
                Assert.Equal("captured-etag", request.BlobETag);
                Assert.Equal(context.Now.UtcDateTime, request.QueuedDate);
            }
            else
            {
                Assert.Empty(context.Requests);
                context.Entities.Verify(entities => entities.SaveChangesAsync(), Times.Never);
            }
        }

        [Fact]
        public async Task ProtectsAllReferencesAcrossPagesAndDoesNotQueueDuplicates()
        {
            var context = new TestContext();
            var old = context.Now.AddDays(-2);
            var firstPage = Enumerable.Range(1, 100).Select(key => CreateBlob($"retained-{key}.nupkg", old)).ToArray();
            context.Packages.AddRange(firstPage.Select(blob => new StagedPackage { Status = StagedPackageStatus.Superseded, UploadedBlobPath = blob.Name }));
            context.Packages.Add(new StagedPackage { Status = StagedPackageStatus.Deleted, ValidatedBlobPath = "validated.nupkg" });
            context.Symbols.Add(new StagedSymbolPackage { Status = StagedPackageStatus.Superseded, UploadedBlobPath = "shared.snupkg" });
            context.Requests.Add(new StagingBlobCleanup { Key = 1, BlobPath = "already-queued.nupkg", BlobETag = "older-etag" });
            context.SetPages(firstPage, new[]
            {
                CreateBlob("validated.nupkg", old),
                CreateBlob("shared.snupkg", old),
                CreateBlob("already-queued.nupkg", old),
                CreateBlob("orphan.nupkg", old),
                CreateBlob("recent.nupkg", context.Now.AddHours(-1)),
            });

            await context.Target.ProcessAsync(context.Entities.Object, context.Container.Object, context.Now);
            await context.Target.ProcessAsync(context.Entities.Object, context.Container.Object, context.Now);

            Assert.Equal(new[] { "already-queued.nupkg", "orphan.nupkg" }, context.Requests.Select(request => request.BlobPath));
            Assert.Equal("older-etag", context.Requests[0].BlobETag);
            context.Entities.Verify(entities => entities.SaveChangesAsync(), Times.Once);
            context.Container.Verify(container => container.GetBlobClient(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExistingConsumerDeletesQueuedOrphanUsingCapturedETag()
        {
            var context = new TestContext();
            context.SetPages(new[] { CreateBlob("orphan.nupkg", context.Now.AddDays(-2)) });
            await context.Target.ProcessAsync(context.Entities.Object, context.Container.Object, context.Now);
            var blob = new Mock<BlobClient>();
            blob.Setup(client => client.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots, It.Is<BlobRequestConditions>(conditions => conditions.IfMatch == new ETag("captured-etag")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
            context.Container.Setup(container => container.GetBlobClient("orphan.nupkg")).Returns(blob.Object);
            var deletion = new DeleteStagingBlobsTask(Mock.Of<ILogger<DeleteStagingBlobsTask>>());

            await deletion.ProcessAsync(context.Entities.Object, context.Container.Object);

            Assert.Empty(context.Requests);
            blob.Verify(client => client.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots, It.Is<BlobRequestConditions>(conditions => conditions.IfMatch == new ETag("captured-etag")), It.IsAny<CancellationToken>()), Times.Once);
        }

        private static BlobItem CreateBlob(string path, DateTimeOffset lastModified)
        {
            return BlobsModelFactory.BlobItem(path, properties: BlobsModelFactory.BlobItemProperties(false, lastModified: lastModified, eTag: new ETag("captured-etag")));
        }

        private class TestContext
        {
            private readonly List<StagingBlobCleanup> _pendingAdds = new List<StagingBlobCleanup>();
            private readonly List<StagingBlobCleanup> _pendingDeletes = new List<StagingBlobCleanup>();

            public TestContext()
            {
                var requests = new Mock<DbSet<StagingBlobCleanup>>().SetupDbSet(Requests);
                requests.Setup(set => set.Add(It.IsAny<StagingBlobCleanup>())).Callback<StagingBlobCleanup>(_pendingAdds.Add);
                requests.Setup(set => set.Remove(It.IsAny<StagingBlobCleanup>())).Callback<StagingBlobCleanup>(_pendingDeletes.Add);
                Entities.Setup(entities => entities.Set<StagingBlobCleanup>()).Returns(requests.Object);
                Entities.Setup(entities => entities.StagedPackages).Returns(new Mock<DbSet<StagedPackage>>().SetupDbSet(Packages).Object);
                Entities.Setup(entities => entities.StagedSymbolPackages).Returns(new Mock<DbSet<StagedSymbolPackage>>().SetupDbSet(Symbols).Object);
                Entities.Setup(entities => entities.SaveChangesAsync()).Callback(() =>
                {
                    foreach (var request in _pendingAdds)
                    {
                        request.Key = Requests.Count + 1;
                        Requests.Add(request);
                    }
                    _pendingAdds.Clear();
                    foreach (var request in _pendingDeletes)
                    {
                        Requests.Remove(request);
                    }
                    _pendingDeletes.Clear();
                }).ReturnsAsync(1);
                Container.Setup(container => container.ExistsAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
                Target = new QueueOrphanedStagingBlobsTask(Mock.Of<ILogger<QueueOrphanedStagingBlobsTask>>());
            }

            public DateTimeOffset Now { get; } = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(2));

            public List<StagingBlobCleanup> Requests { get; } = new List<StagingBlobCleanup>();

            public List<StagedPackage> Packages { get; } = new List<StagedPackage>();

            public List<StagedSymbolPackage> Symbols { get; } = new List<StagedSymbolPackage>();

            public Mock<IEntitiesContext> Entities { get; } = new Mock<IEntitiesContext>();

            public Mock<BlobContainerClient> Container { get; } = new Mock<BlobContainerClient>();

            public QueueOrphanedStagingBlobsTask Target { get; }

            public void SetPages(params BlobItem[][] pages)
            {
                var responses = pages.Select((items, index) => Page<BlobItem>.FromValues(items, index + 1 < pages.Length ? $"page-{index + 1}" : null, Mock.Of<Response>()));
                Container.Setup(container => container.GetBlobsAsync(
                    It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(AsyncPageable<BlobItem>.FromPages(responses));
            }
        }
    }
}

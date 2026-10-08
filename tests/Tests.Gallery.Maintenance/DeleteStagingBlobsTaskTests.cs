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
    public class DeleteStagingBlobsTaskTests
    {
        [Fact]
        public async Task ReachesLaterPagesAndRetainsChangedAndLiveFiles()
        {
            var context = new TestContext();
            for (var key = 1; key <= 101; key++)
            {
                context.Requests.Add(new StagingBlobCleanup { Key = key, StagedPackageIdentityKey = 42, BlobPath = "protected.nupkg", BlobETag = "etag" });
            }
            context.Packages.Add(new StagedPackage
            {
                Key = 1,
                StagedPackageIdentity = new StagedPackageIdentity { CurrentStagedPackageKey = 1 },
                Status = StagedPackageStatus.PromotionFailed,
                UploadedBlobPath = "protected.nupkg",
            });
            context.Requests.Add(new StagingBlobCleanup { Key = 102, StagedPackageIdentityKey = 42, BlobPath = "changed.nupkg", BlobETag = "old-etag" });
            context.Requests.Add(new StagingBlobCleanup { Key = 103, StagedPackageIdentityKey = 42, BlobPath = "eligible.nupkg", BlobETag = "eligible-etag" });
            context.Blob.Setup(blob => blob.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots, It.Is<BlobRequestConditions>(conditions => conditions.IfMatch == new ETag("old-etag")), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(412, "ETag mismatch"));

            await context.Target.ProcessAsync(context.Entities.Object, context.Container.Object);

            context.Container.Verify(container => container.GetBlobClient("protected.nupkg"), Times.Never);
            Assert.Equal(102, context.Requests.Count);
            Assert.Contains(context.Requests, request => request.Key == 102);
            context.Entities.Verify(entities => entities.SaveChangesAsync(), Times.Once);
            context.Blob.Verify(blob => blob.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots, It.Is<BlobRequestConditions>(conditions => conditions.IfMatch == new ETag("eligible-etag")), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CompletesAlreadyMissingFileWhenConditionalDeletionReturnsPreconditionFailure()
        {
            var context = new TestContext();
            context.Requests.Add(new StagingBlobCleanup { Key = 1, StagedPackageIdentityKey = 42, BlobPath = "missing.nupkg", BlobETag = "etag" });
            context.Blob.Setup(blob => blob.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots, It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(412, "Conditional deletion requires an existing blob"));
            context.Blob.Setup(blob => blob.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

            await context.Target.ProcessAsync(context.Entities.Object, context.Container.Object);

            Assert.Empty(context.Requests);
        }

        [Fact]
        public async Task PreservesRequestWhenStorageFails()
        {
            var context = new TestContext();
            context.Requests.Add(new StagingBlobCleanup { Key = 1, StagedPackageIdentityKey = 42, BlobPath = "obsolete.nupkg", BlobETag = "etag" });
            context.Blob.Setup(blob => blob.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots, It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(503, "Storage unavailable"));

            await Assert.ThrowsAsync<RequestFailedException>(() => context.Target.ProcessAsync(context.Entities.Object, context.Container.Object));

            Assert.Single(context.Requests);
            context.Entities.Verify(entities => entities.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task RetriesAlreadyDeletedBlobAfterCleanupCommitFails()
        {
            var context = new TestContext();
            context.Requests.Add(new StagingBlobCleanup { Key = 1, StagedPackageIdentityKey = 42, BlobPath = "obsolete.nupkg", BlobETag = "etag" });
            context.Entities.Setup(entities => entities.SaveChangesAsync()).ThrowsAsync(new InvalidOperationException("Database unavailable"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => context.Target.ProcessAsync(context.Entities.Object, context.Container.Object));

            Assert.Single(context.Requests);
            var retry = new TestContext();
            retry.Requests.Add(new StagingBlobCleanup { Key = 1, StagedPackageIdentityKey = 42, BlobPath = "obsolete.nupkg", BlobETag = "etag" });
            retry.Blob.Setup(blob => blob.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots, It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

            await retry.Target.ProcessAsync(retry.Entities.Object, retry.Container.Object);

            Assert.Empty(retry.Requests);
            context.Entities.Verify(entities => entities.SaveChangesAsync(), Times.Once);
            retry.Entities.Verify(entities => entities.SaveChangesAsync(), Times.Once);
        }

        private class TestContext
        {
            public TestContext()
            {
                var requests = new Mock<DbSet<StagingBlobCleanup>>().SetupDbSet(Requests);
                requests.Setup(set => set.Remove(It.IsAny<StagingBlobCleanup>())).Callback<StagingBlobCleanup>(PendingDeletes.Add);
                Entities.Setup(entities => entities.Set<StagingBlobCleanup>()).Returns(requests.Object);
                Entities.Setup(entities => entities.StagedPackages).Returns(new Mock<DbSet<StagedPackage>>().SetupDbSet(Packages).Object);
                Entities.Setup(entities => entities.StagedSymbolPackages).Returns(new Mock<DbSet<StagedSymbolPackage>>().SetupDbSet(Array.Empty<StagedSymbolPackage>()).Object);
                Entities.Setup(entities => entities.SaveChangesAsync()).Callback(CommitPendingDeletes).ReturnsAsync(1);
                Container.Setup(container => container.GetBlobClient(It.IsAny<string>())).Returns(Blob.Object);
                Blob.Setup(blob => blob.ExistsAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
                Blob.Setup(blob => blob.DeleteIfExistsAsync(
                    DeleteSnapshotsOption.IncludeSnapshots, It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
                Target = new DeleteStagingBlobsTask(Mock.Of<ILogger<DeleteStagingBlobsTask>>());
            }

            public List<StagingBlobCleanup> Requests { get; } = new List<StagingBlobCleanup>();

            public List<StagedPackage> Packages { get; } = new List<StagedPackage>();

            public List<StagingBlobCleanup> PendingDeletes { get; } = new List<StagingBlobCleanup>();

            public Mock<IEntitiesContext> Entities { get; } = new Mock<IEntitiesContext>();

            public Mock<BlobContainerClient> Container { get; } = new Mock<BlobContainerClient>();

            public Mock<BlobClient> Blob { get; } = new Mock<BlobClient>();

            public DeleteStagingBlobsTask Target { get; }

            public void CommitPendingDeletes()
            {
                foreach (var request in PendingDeletes)
                {
                    Requests.Remove(request);
                }
                PendingDeletes.Clear();
            }
        }
    }
}

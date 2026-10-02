// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class StagingBlobCleanupServiceFacts
    {
        [Fact]
        public void QueuesAllIdentityFilesWithoutCommittingOrRemovingAttempts()
        {
            var context = new TestContext();
            context.Packages.Add(new StagedPackage
            {
                StagedPackageIdentityKey = 42,
                Status = StagedPackageStatus.Superseded,
                UploadedBlobPath = "old.nupkg",
                UploadedBlobETag = "old-etag",
                ValidatedBlobPath = "old-validated.nupkg",
                ValidatedBlobETag = "old-validated-etag",
            });
            context.Packages.Add(new StagedPackage
            {
                StagedPackageIdentityKey = 42,
                UploadedBlobPath = "current.nupkg",
                UploadedBlobETag = "current-etag",
            });
            context.Packages.Add(new StagedPackage { StagedPackageIdentityKey = 43 });
            context.Symbols.Add(new StagedSymbolPackage
            {
                StagedPackageIdentityKey = 42,
                UploadedBlobPath = "symbols.snupkg",
                UploadedBlobETag = "symbols-etag",
            });
            var before = DateTime.UtcNow;

            context.Target.QueuePackageFiles(42);
            context.Target.QueueSymbolFiles(42);

            Assert.Equal(new[] { "old.nupkg", "old-validated.nupkg", "current.nupkg", "symbols.snupkg" }, context.Requests.Select(request => request.BlobPath));
            Assert.Equal(new[] { "old-etag", "old-validated-etag", "current-etag", "symbols-etag" }, context.Requests.Select(request => request.BlobETag));
            Assert.All(context.Requests, request =>
            {
                Assert.Equal(42, request.StagedPackageIdentityKey);
                Assert.InRange(request.QueuedDate, before, DateTime.UtcNow);
            });
            Assert.Equal(3, context.Packages.Count);
            context.Entities.Verify(entities => entities.SaveChangesAsync(), Times.Never);
        }

        [Theory]
        [InlineData(StagedPackageStatus.Validating, true)]
        [InlineData(StagedPackageStatus.Ready, true)]
        [InlineData(StagedPackageStatus.FailedValidation, true)]
        [InlineData(StagedPackageStatus.PromotionFailed, true)]
        [InlineData(StagedPackageStatus.Promoting, true)]
        [InlineData(StagedPackageStatus.Superseded, false)]
        [InlineData(StagedPackageStatus.Deleted, false)]
        [InlineData(StagedPackageStatus.Succeeded, false)]
        public void ProtectsOnlyLiveParentContent(StagedPackageStatus status, bool expected)
        {
            var context = new TestContext();
            context.Packages.Add(new StagedPackage
            {
                Key = 1,
                Status = status,
                StagedPackageIdentity = new StagedPackageIdentity { CurrentStagedPackageKey = 1 },
                ValidatedBlobPath = "validated.nupkg",
            });

            Assert.Equal(expected, context.Target.HasLiveReference(new StagingBlobCleanup { StagedPackageIdentityKey = 42, BlobPath = "validated.nupkg", BlobETag = "etag" }));
        }

        [Theory]
        [InlineData(StagedPackageStatus.Validating, true)]
        [InlineData(StagedPackageStatus.Ready, true)]
        [InlineData(StagedPackageStatus.WaitingForParent, true)]
        [InlineData(StagedPackageStatus.PromotionFailed, true)]
        [InlineData(StagedPackageStatus.Promoting, true)]
        [InlineData(StagedPackageStatus.Superseded, false)]
        [InlineData(StagedPackageStatus.Deleted, false)]
        [InlineData(StagedPackageStatus.Succeeded, false)]
        public void ProtectsSharedSymbolContentAfterParentRevalidation(StagedPackageStatus status, bool expected)
        {
            var context = new TestContext();
            var identity = new StagedPackageIdentity { CurrentStagedSymbolPackageKey = 2 };
            context.Symbols.Add(new StagedSymbolPackage
            {
                Key = 1,
                StagedPackageIdentity = identity,
                Status = StagedPackageStatus.Superseded,
                UploadedBlobPath = "shared.snupkg",
            });
            context.Symbols.Add(new StagedSymbolPackage
            {
                Key = 2,
                StagedPackageIdentity = identity,
                Status = status,
                UploadedBlobPath = "shared.snupkg",
            });

            Assert.Equal(expected, context.Target.HasLiveReference(new StagingBlobCleanup { StagedPackageIdentityKey = 42, BlobPath = "shared.snupkg", BlobETag = "etag" }));
        }

        [Theory]
        [InlineData(StagedPackageStatus.Validating, "removed-parent.nupkg", true)]
        [InlineData(StagedPackageStatus.Validating, "removed-parent.NUPKG", true)]
        [InlineData(StagedPackageStatus.Validating, "old-symbols.snupkg", false)]
        [InlineData(StagedPackageStatus.Ready, "removed-parent.nupkg", false)]
        [InlineData(StagedPackageStatus.WaitingForParent, "removed-parent.nupkg", false)]
        public void ProtectsPublishedParentsPrivateFileOnlyWhileRetainedSymbolsAreValidating(StagedPackageStatus status, string blobPath, bool expected)
        {
            var context = new TestContext();
            context.Symbols.Add(new StagedSymbolPackage
            {
                Key = 2,
                StagedPackageIdentityKey = 42,
                StagedPackageIdentity = new StagedPackageIdentity { CurrentStagedSymbolPackageKey = 2 },
                Status = status,
                UploadedBlobPath = "symbols.snupkg",
            });

            Assert.Equal(expected, context.Target.HasLiveReference(new StagingBlobCleanup { StagedPackageIdentityKey = 42, BlobPath = blobPath, BlobETag = "etag" }));
            Assert.False(context.Target.HasLiveReference(new StagingBlobCleanup { StagedPackageIdentityKey = 43, BlobPath = "unrelated.nupkg", BlobETag = "etag" }));
        }

        private class TestContext
        {
            public TestContext()
            {
                var packages = new Mock<DbSet<StagedPackage>>().SetupDbSet(Packages);
                packages.Setup(set => set.AsNoTracking()).Returns(packages.Object);
                Entities.Setup(entities => entities.StagedPackages).Returns(packages.Object);
                var requests = new Mock<DbSet<StagingBlobCleanup>>();
                requests.Setup(set => set.Add(It.IsAny<StagingBlobCleanup>())).Callback<StagingBlobCleanup>(Requests.Add);
                Entities.Setup(entities => entities.Set<StagingBlobCleanup>()).Returns(requests.Object);
                var symbols = new Mock<DbSet<StagedSymbolPackage>>().SetupDbSet(Symbols);
                symbols.Setup(set => set.AsNoTracking()).Returns(symbols.Object);
                Entities.Setup(entities => entities.StagedSymbolPackages).Returns(symbols.Object);
                Target = new StagingBlobCleanupService(Entities.Object);
            }

            public List<StagedPackage> Packages { get; } = new List<StagedPackage>();

            public List<StagedSymbolPackage> Symbols { get; } = new List<StagedSymbolPackage>();

            public List<StagingBlobCleanup> Requests { get; } = new List<StagingBlobCleanup>();

            public Mock<IEntitiesContext> Entities { get; } = new Mock<IEntitiesContext>();

            public StagingBlobCleanupService Target { get; }
        }
    }
}

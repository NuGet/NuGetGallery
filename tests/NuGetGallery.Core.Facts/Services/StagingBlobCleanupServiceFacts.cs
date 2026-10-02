// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
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
        public async Task ProtectsOnlyLiveParentContent(StagedPackageStatus status, bool expected)
        {
            var context = new TestContext();
            context.Packages.Add(new StagedPackage
            {
                Key = 1,
                Status = status,
                StagedPackageIdentity = new StagedPackageIdentity { CurrentStagedPackageKey = 1 },
                ValidatedBlobPath = "validated.nupkg",
            });

            var livePaths = await context.Target.GetLiveReferencedPathsAsync(new[] { new StagingBlobCleanup { BlobPath = "validated.nupkg", BlobETag = "etag" } });

            Assert.Equal(expected, livePaths.Contains("validated.nupkg"));
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
        public async Task ProtectsSharedSymbolContentAfterParentRevalidation(StagedPackageStatus status, bool expected)
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

            var livePaths = await context.Target.GetLiveReferencedPathsAsync(new[] { new StagingBlobCleanup { BlobPath = "shared.snupkg", BlobETag = "etag" } });

            Assert.Equal(expected, livePaths.Contains("shared.snupkg"));
        }

        [Theory]
        [InlineData(StagedPackageStatus.Validating, "removed-parent.nupkg", true)]
        [InlineData(StagedPackageStatus.Validating, "removed-parent.NUPKG", true)]
        [InlineData(StagedPackageStatus.Validating, "old-symbols.snupkg", false)]
        [InlineData(StagedPackageStatus.Ready, "removed-parent.nupkg", false)]
        [InlineData(StagedPackageStatus.WaitingForParent, "removed-parent.nupkg", false)]
        public async Task ProtectsPublishedParentsPrivateFileOnlyWhileRetainedSymbolsAreValidating(StagedPackageStatus status, string blobPath, bool expected)
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

            var requests = new[]
            {
                new StagingBlobCleanup { StagedPackageIdentityKey = 42, BlobPath = blobPath, BlobETag = "etag" },
                new StagingBlobCleanup { StagedPackageIdentityKey = 43, BlobPath = "unrelated.nupkg", BlobETag = "etag" },
            };
            var livePaths = await context.Target.GetLiveReferencedPathsAsync(requests);

            Assert.Equal(expected, livePaths.Contains(blobPath));
            Assert.DoesNotContain("unrelated.nupkg", livePaths);
        }

        [Fact]
        public async Task ResolvesMixedPageReferencesWithoutProtectingObsoleteFiles()
        {
            var context = new TestContext();
            var identity = new StagedPackageIdentity { CurrentStagedPackageKey = 1, CurrentStagedSymbolPackageKey = 2 };
            context.Packages.Add(new StagedPackage
            {
                Key = 1,
                StagedPackageIdentity = identity,
                Status = StagedPackageStatus.Ready,
                UploadedBlobPath = "uploaded.nupkg",
                ValidatedBlobPath = "validated.nupkg",
            });
            context.Packages.Add(new StagedPackage
            {
                Key = 3,
                StagedPackageIdentity = new StagedPackageIdentity { CurrentStagedPackageKey = 4 },
                StagedPackageIdentityKey = 43,
                Status = StagedPackageStatus.Ready,
                UploadedBlobPath = "obsolete.nupkg",
            });
            context.Symbols.Add(new StagedSymbolPackage
            {
                Key = 2,
                StagedPackageIdentity = identity,
                StagedPackageIdentityKey = 42,
                Status = StagedPackageStatus.Validating,
                UploadedBlobPath = "shared.snupkg",
            });
            var requests = new[] { "uploaded.nupkg", "validated.nupkg", "shared.snupkg", "removed-parent.nupkg", "old-symbols.snupkg" }
                .Select(path => new StagingBlobCleanup { StagedPackageIdentityKey = 42, BlobPath = path, BlobETag = "etag" }).ToList();
            requests.Add(new StagingBlobCleanup { StagedPackageIdentityKey = 43, BlobPath = "obsolete.nupkg", BlobETag = "etag" });
            requests.Add(new StagingBlobCleanup { StagedPackageIdentityKey = 43, BlobPath = "unrelated.nupkg", BlobETag = "etag" });

            var livePaths = await context.Target.GetLiveReferencedPathsAsync(requests);

            Assert.Equal(new[] { "removed-parent.nupkg", "shared.snupkg", "uploaded.nupkg", "validated.nupkg" }, livePaths.OrderBy(path => path));
            Assert.Equal(1, context.PackageQueryCount);
            Assert.Equal(1, context.SymbolQueryCount);
        }

        private class TestContext
        {
            public TestContext()
            {
                var packages = new Mock<DbSet<StagedPackage>>().SetupDbSet(CountQueries(Packages, () => PackageQueryCount++));
                packages.Setup(set => set.AsNoTracking()).Returns(packages.Object);
                Entities.Setup(entities => entities.StagedPackages).Returns(packages.Object);
                var requests = new Mock<DbSet<StagingBlobCleanup>>();
                requests.Setup(set => set.Add(It.IsAny<StagingBlobCleanup>())).Callback<StagingBlobCleanup>(Requests.Add);
                Entities.Setup(entities => entities.Set<StagingBlobCleanup>()).Returns(requests.Object);
                var symbols = new Mock<DbSet<StagedSymbolPackage>>().SetupDbSet(CountQueries(Symbols, () => SymbolQueryCount++));
                symbols.Setup(set => set.AsNoTracking()).Returns(symbols.Object);
                Entities.Setup(entities => entities.StagedSymbolPackages).Returns(symbols.Object);
                Target = new StagingBlobCleanupService(Entities.Object);
            }

            public List<StagedPackage> Packages { get; } = new List<StagedPackage>();

            public List<StagedSymbolPackage> Symbols { get; } = new List<StagedSymbolPackage>();

            public List<StagingBlobCleanup> Requests { get; } = new List<StagingBlobCleanup>();

            public Mock<IEntitiesContext> Entities { get; } = new Mock<IEntitiesContext>();

            public StagingBlobCleanupService Target { get; }

            public int PackageQueryCount { get; private set; }

            public int SymbolQueryCount { get; private set; }

            private static IEnumerable<T> CountQueries<T>(IEnumerable<T> entities, Action onQuery)
            {
                onQuery();
                foreach (var entity in entities)
                {
                    yield return entity;
                }
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Gallery.Maintenance;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery;
using Xunit;

namespace Tests.Gallery.Maintenance
{
    public class ExpireStagingTaskTests
    {
        [Fact]
        public async Task ExpiredGroupDeletesPrivateMembersAndPreservesPublishedContent()
        {
            var context = new TestContext();
            var group = context.AddGroup(1, expired: true);
            var parent = context.AddPackage(1, group);
            var symbols = context.AddSymbols(parent.StagedPackageIdentity, 1);
            var published = context.AddPackage(2, group);
            published.Status = StagedPackageStatus.Succeeded;
            published.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
            var publishedSymbols = context.AddSymbols(published.StagedPackageIdentity, 2);
            publishedSymbols.Status = StagedPackageStatus.Succeeded;
            publishedSymbols.SymbolPackage.StatusKey = PackageStatus.Available;

            await context.RunAsync();

            Assert.Empty(context.Groups);
            Assert.Equal(StagedPackageStatus.Deleted, parent.Status);
            Assert.Equal(PackageStatus.Deleted, parent.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.False(parent.StagedPackageIdentity.Package.Listed);
            Assert.DoesNotContain(symbols, context.Symbols);
            Assert.DoesNotContain(symbols.SymbolPackage, context.SymbolPackages);
            Assert.Null(parent.StagedPackageIdentity.StagingGroupKey);
            Assert.Equal(PackageStatus.Available, published.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.Equal(PackageStatus.Available, publishedSymbols.SymbolPackage.StatusKey);
            Assert.Contains(publishedSymbols, context.Symbols);
            Assert.Null(published.StagedPackageIdentity.StagingGroupKey);
            context.AssertQueuedFiles(1, packages: 1, symbols: 1);
            context.AssertQueuedFiles(2, packages: 0, symbols: 0);
        }

        [Fact]
        public async Task ExpiredParentRetainsStillLiveSymbolsWithTheirOriginalDeadline()
        {
            var context = new TestContext();
            var parent = context.AddPackage(1, expired: true);
            var previous = context.AddSymbols(parent.StagedPackageIdentity, 1);
            previous.Status = StagedPackageStatus.Validating;
            var deadline = previous.ExpirationDate;

            await context.RunAsync();

            Assert.Equal(StagedPackageStatus.Deleted, parent.Status);
            Assert.Equal(PackageStatus.Deleted, parent.StagedPackageIdentity.Package.PackageStatusKey);
            var waiting = parent.StagedPackageIdentity.CurrentStagedSymbolPackage;
            Assert.NotSame(previous, waiting);
            Assert.Equal(StagedPackageStatus.WaitingForParent, waiting.Status);
            Assert.Equal(deadline, waiting.ExpirationDate);
            Assert.Contains(previous.SymbolPackage, context.SymbolPackages);
            context.AssertQueuedFiles(1, packages: 1, symbols: 0);

            await context.RunAsync();

            Assert.Same(waiting, parent.StagedPackageIdentity.CurrentStagedSymbolPackage);
            context.AssertQueuedFiles(1, packages: 1, symbols: 0);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExpiredSymbolsLeaveTheirPublicOrLiveStagedParentUntouched(bool stagedParent)
        {
            var context = new TestContext();
            var parent = context.AddPackage(1);
            if (!stagedParent)
            {
                parent.Status = StagedPackageStatus.Succeeded;
                parent.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
                parent.StagedPackageIdentity.CurrentStagedPackageKey = null;
                parent.StagedPackageIdentity.CurrentStagedPackage = null;
            }
            var symbols = context.AddSymbols(parent.StagedPackageIdentity, 1, expired: true);
            var originalParentStatus = parent.StagedPackageIdentity.Package.PackageStatusKey;

            await context.RunAsync();

            Assert.Equal(originalParentStatus, parent.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.DoesNotContain(symbols, context.Symbols);
            Assert.DoesNotContain(symbols.SymbolPackage, context.SymbolPackages);
            Assert.Null(parent.StagedPackageIdentity.CurrentStagedSymbolPackageKey);
            Assert.Equal(stagedParent, context.Identities.Contains(parent.StagedPackageIdentity));
            context.AssertQueuedFiles(1, packages: stagedParent ? 0 : 1, symbols: 1);
        }

        [Fact]
        public async Task ExpiringBothStandaloneArtifactsRemovesRetainedSymbolsInTheSameRun()
        {
            var context = new TestContext();
            var parent = context.AddPackage(1, expired: true);
            context.AddSymbols(parent.StagedPackageIdentity, 1, expired: true);

            await context.RunAsync();

            Assert.Equal(StagedPackageStatus.Deleted, parent.Status);
            Assert.Empty(context.Symbols);
            Assert.Empty(context.SymbolPackages);
            Assert.Null(parent.StagedPackageIdentity.CurrentStagedSymbolPackageKey);
            context.AssertQueuedFiles(1, packages: 1, symbols: 2);
        }

        [Theory]
        [InlineData("group")]
        [InlineData("parent")]
        [InlineData("symbols")]
        public async Task AcceptedPromotionRemainsProtectedAfterExpiration(string target)
        {
            var context = new TestContext();
            var group = target == "group" ? context.AddGroup(1, expired: true) : null;
            var parent = context.AddPackage(1, group, expired: true);
            var symbols = context.AddSymbols(parent.StagedPackageIdentity, 1, expired: true);
            if (group != null)
            {
                group.ActivePromotionId = Guid.NewGuid();
            }
            else if (target == "parent")
            {
                parent.Status = StagedPackageStatus.Promoting;
            }
            else
            {
                symbols.Status = StagedPackageStatus.Promoting;
            }

            await context.RunAsync();

            Assert.Equal(PackageStatus.Staged, parent.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.Contains(symbols, context.Symbols);
            Assert.Contains(symbols.SymbolPackage, context.SymbolPackages);
            Assert.Empty(context.Cleanups);
            Assert.All(context.Contexts, entities => entities.Verify(value => value.SaveChangesAsync(), Times.Never));
        }

        [Fact]
        public async Task LiveGroupOverridesExpiredArtifactDeadlinesAndRetiredAttemptsAreIgnored()
        {
            var context = new TestContext();
            var group = context.AddGroup(1);
            var parent = context.AddPackage(1, group, expired: true);
            var symbols = context.AddSymbols(parent.StagedPackageIdentity, 1, expired: true);
            var retired = context.AddPackage(2, expired: true);
            retired.Status = StagedPackageStatus.Superseded;
            retired.StagedPackageIdentity.CurrentStagedPackageKey = 99;

            await context.RunAsync();

            Assert.Contains(group, context.Groups);
            Assert.Equal(StagedPackageStatus.Ready, parent.Status);
            Assert.Contains(symbols, context.Symbols);
            Assert.Equal(StagedPackageStatus.Superseded, retired.Status);
            Assert.Empty(context.Cleanups);
        }

        [Fact]
        public async Task ProtectedGroupsDoNotStarveLaterPages()
        {
            var context = new TestContext();
            for (var key = 1; key <= 100; key++)
            {
                var protectedGroup = context.AddGroup(key, expired: true);
                context.AddPackage(key, protectedGroup).Status = StagedPackageStatus.Promoting;
            }
            var eligible = context.AddGroup(101, expired: true);
            var parent = context.AddPackage(101, eligible);

            await context.RunAsync();

            Assert.Equal(100, context.Groups.Count);
            Assert.DoesNotContain(eligible, context.Groups);
            Assert.Equal(StagedPackageStatus.Deleted, parent.Status);
            context.AssertQueuedFiles(101, packages: 1, symbols: 0);
            Assert.Single(context.Cleanups);
        }

        [Fact]
        public async Task RechecksPromotionAtArtifactDeletionAcceptance()
        {
            var context = new TestContext();
            var parent = context.AddPackage(1, expired: true);
            context.BeforeTransaction = () =>
            {
                context.Packages.Remove(parent);
                context.Identities.Remove(parent.StagedPackageIdentity);
                context.AddPackage(1, expired: true).Status = StagedPackageStatus.Promoting;
            };

            await context.RunAsync();

            var current = Assert.Single(context.Packages);
            Assert.NotSame(parent, current);
            Assert.Equal(StagedPackageStatus.Promoting, current.Status);
            Assert.Equal(PackageStatus.Staged, current.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.Empty(context.Cleanups);
        }

        [Fact]
        public async Task DisposesEachCandidateContextAcrossPagesAndAllThreeScans()
        {
            var context = new TestContext();
            for (var key = 1; key <= 101; key++)
            {
                context.AddGroup(key, expired: true);
            }
            var expiredParent = context.AddPackage(102, expired: true);
            var liveParent = context.AddPackage(103);
            context.AddSymbols(liveParent.StagedPackageIdentity, 103, expired: true);

            await context.RunAsync();

            Assert.Empty(context.Groups);
            Assert.Equal(StagedPackageStatus.Deleted, expiredParent.Status);
            Assert.Empty(context.Symbols);
            Assert.Equal(PackageStatus.Staged, liveParent.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.Equal(110, context.Contexts.Count);
            Assert.Equal(1, context.MaxActiveContexts);
            Assert.Equal(0, context.ActiveContexts);
            Assert.All(context.Contexts, entities => entities.Verify(value => value.Dispose(), Times.Once));
        }

        private class TestContext
        {
            public List<StagingGroup> Groups { get; } = new List<StagingGroup>();

            public List<StagedPackage> Packages { get; } = new List<StagedPackage>();

            public List<StagedSymbolPackage> Symbols { get; } = new List<StagedSymbolPackage>();

            public List<StagedPackageIdentity> Identities { get; } = new List<StagedPackageIdentity>();

            public List<SymbolPackage> SymbolPackages { get; } = new List<SymbolPackage>();

            public List<StagingBlobCleanup> Cleanups { get; } = new List<StagingBlobCleanup>();

            public List<Mock<IEntitiesContext>> Contexts { get; } = new List<Mock<IEntitiesContext>>();

            public Action BeforeTransaction { get; set; }

            public int ActiveContexts { get; private set; }

            public int MaxActiveContexts { get; private set; }

            public StagingGroup AddGroup(int key, bool expired = false)
            {
                var group = new StagingGroup { Key = key, OwnerKey = 1, ExpirationDate = DateTime.UtcNow.AddDays(expired ? -1 : 1) };
                Groups.Add(group);
                return group;
            }

            public StagedPackage AddPackage(int key, StagingGroup group = null, bool expired = false)
            {
                var identity = new StagedPackageIdentity
                {
                    Key = key,
                    OwnerKey = 1,
                    StagingGroupKey = group?.Key,
                    StagingGroup = group,
                    Package = new Package { Key = key, PackageRegistration = new PackageRegistration(), PackageStatusKey = PackageStatus.Staged, Listed = true },
                    CurrentStagedPackageKey = key,
                };
                var attempt = new StagedPackage
                {
                    Key = key,
                    StagedPackageIdentityKey = key,
                    StagedPackageIdentity = identity,
                    Status = StagedPackageStatus.Ready,
                    ExpirationDate = DateTime.UtcNow.AddDays(expired ? -1 : 1),
                    UploadedBlobPath = $"parent-{key}.nupkg",
                    UploadedBlobETag = $"parent-etag-{key}",
                };
                identity.CurrentStagedPackage = attempt;
                Identities.Add(identity);
                Packages.Add(attempt);
                return attempt;
            }

            public StagedSymbolPackage AddSymbols(StagedPackageIdentity identity, int key, bool expired = false)
            {
                var symbols = new SymbolPackage { Key = key, PackageKey = identity.Key, Package = identity.Package, StatusKey = PackageStatus.Staged };
                var attempt = new StagedSymbolPackage
                {
                    Key = key,
                    StagedPackageIdentityKey = identity.Key,
                    StagedPackageIdentity = identity,
                    SymbolPackage = symbols,
                    Status = StagedPackageStatus.Ready,
                    ExpirationDate = DateTime.UtcNow.AddDays(expired ? -1 : 1),
                    UploadedBlobPath = $"symbols-{key}.snupkg",
                    UploadedBlobETag = $"etag-{key}",
                };
                identity.CurrentStagedSymbolPackageKey = key;
                identity.CurrentStagedSymbolPackage = attempt;
                Symbols.Add(attempt);
                SymbolPackages.Add(symbols);
                return attempt;
            }

            public Task RunAsync()
            {
                return new ExpireStagingTask(Mock.Of<ILogger<ExpireStagingTask>>())
                    .ProcessAsync(CreateContextAsync);
            }

            public void AssertQueuedFiles(int identityKey, int packages, int symbols)
            {
                Assert.Equal(packages, Cleanups.Count(request => request.StagedPackageIdentityKey == identityKey && request.BlobPath.EndsWith(".nupkg", StringComparison.Ordinal)));
                Assert.Equal(symbols, Cleanups.Count(request => request.StagedPackageIdentityKey == identityKey && request.BlobPath.EndsWith(".snupkg", StringComparison.Ordinal)));
            }

            private Task<IEntitiesContext> CreateContextAsync()
            {
                var entities = new Mock<IEntitiesContext>();
                var pendingDeletes = new List<Action>();
                var groups = CreateSet(Groups, pendingDeletes);
                var packages = CreateSet(Packages, pendingDeletes);
                var symbols = CreateSet(Symbols, pendingDeletes);
                var identities = CreateSet(Identities, pendingDeletes);
                var symbolPackages = CreateSet(SymbolPackages, pendingDeletes);
                var cleanups = CreateSet(Cleanups, pendingDeletes);
                entities.Setup(value => value.StagingGroups).Returns(groups.Object);
                entities.Setup(value => value.StagedPackages).Returns(packages.Object);
                entities.Setup(value => value.StagedSymbolPackages).Returns(symbols.Object);
                entities.Setup(value => value.Set<StagingGroup>()).Returns(groups.Object);
                entities.Setup(value => value.Set<StagedPackage>()).Returns(packages.Object);
                entities.Setup(value => value.Set<StagedSymbolPackage>()).Returns(symbols.Object);
                entities.Setup(value => value.Set<StagedPackageIdentity>()).Returns(identities.Object);
                entities.Setup(value => value.Set<SymbolPackage>()).Returns(symbolPackages.Object);
                entities.Setup(value => value.Set<StagingBlobCleanup>()).Returns(cleanups.Object);
                symbols.Setup(value => value.Add(It.IsAny<StagedSymbolPackage>()))
                    .Callback<StagedSymbolPackage>(attempt =>
                    {
                        attempt.Key = Symbols.Max(symbol => symbol.Key) + 1;
                        attempt.StagedPackageIdentityKey = attempt.StagedPackageIdentity.Key;
                        Symbols.Add(attempt);
                    }).Returns<StagedSymbolPackage>(attempt => attempt);
                entities.Setup(value => value.SaveChangesAsync()).Callback(() =>
                {
                    foreach (var delete in pendingDeletes)
                    {
                        delete();
                    }
                    pendingDeletes.Clear();
                }).ReturnsAsync(1);
                var database = new Mock<IDatabase>();
                database.Setup(value => value.BeginTransaction()).Callback(() => BeforeTransaction?.Invoke())
                    .Returns(() => Mock.Of<IDbContextTransaction>());
                entities.Setup(value => value.GetDatabase()).Returns(database.Object);
                ActiveContexts++;
                MaxActiveContexts = Math.Max(MaxActiveContexts, ActiveContexts);
                entities.Setup(value => value.Dispose()).Callback(() => ActiveContexts--);
                Contexts.Add(entities);
                return Task.FromResult(entities.Object);
            }

            private static Mock<DbSet<T>> CreateSet<T>(List<T> items, List<Action> pendingDeletes) where T : class
            {
                var set = new Mock<DbSet<T>>().SetupDbSet(items);
                set.Setup(value => value.Include(It.IsAny<string>())).Returns(set.Object);
                set.Setup(value => value.AsNoTracking()).Returns(set.Object);
                set.Setup(value => value.Add(It.IsAny<T>())).Callback<T>(items.Add).Returns<T>(item => item);
                set.Setup(value => value.Remove(It.IsAny<T>())).Callback<T>(item => pendingDeletes.Add(() => items.Remove(item))).Returns<T>(item => item);
                return set;
            }
        }
    }
}

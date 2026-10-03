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
            context.Cleanup.Verify(service => service.QueuePackageFiles(1), Times.Once);
            context.Cleanup.Verify(service => service.QueueSymbolFiles(1), Times.Once);
            context.Cleanup.Verify(service => service.QueuePackageFiles(2), Times.Never);
            context.Cleanup.Verify(service => service.QueueSymbolFiles(2), Times.Never);
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
            context.Cleanup.Verify(service => service.QueuePackageFiles(1), Times.Once);
            context.Cleanup.Verify(service => service.QueueSymbolFiles(1), Times.Never);

            await context.RunAsync();

            Assert.Same(waiting, parent.StagedPackageIdentity.CurrentStagedSymbolPackage);
            context.Cleanup.Verify(service => service.QueuePackageFiles(1), Times.Once);
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
            context.Cleanup.Verify(service => service.QueueSymbolFiles(1), Times.Once);
            context.PackageService.Verify(service => service.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()), Times.Never);
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
            context.Cleanup.Verify(service => service.QueuePackageFiles(1), Times.Once);
            context.Cleanup.Verify(service => service.QueueSymbolFiles(1), Times.Once);
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
            context.Cleanup.VerifyNoOtherCalls();
            context.PackageRepository.Verify(repository => repository.CommitChangesAsync(), Times.Never);
            context.SymbolRepository.Verify(repository => repository.CommitChangesAsync(), Times.Never);
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
            context.Cleanup.VerifyNoOtherCalls();
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
            context.Cleanup.Verify(service => service.QueuePackageFiles(101), Times.Once);
            context.Cleanup.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RechecksPromotionAtArtifactDeletionAcceptance()
        {
            var context = new TestContext();
            var parent = context.AddPackage(1, expired: true);
            context.PackageRepository.Setup(repository => repository.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(action =>
                {
                    parent.Status = StagedPackageStatus.Promoting;
                    return action();
                });

            await context.RunAsync();

            Assert.Equal(StagedPackageStatus.Promoting, parent.Status);
            Assert.Equal(PackageStatus.Staged, parent.StagedPackageIdentity.Package.PackageStatusKey);
            context.Cleanup.VerifyNoOtherCalls();
        }

        private class TestContext
        {
            public TestContext()
            {
                GroupRepository = CreateRepository(Groups);
                PackageRepository = CreateRepository(Packages);
                SymbolRepository = CreateRepository(Symbols);
                var identities = CreateRepository(Identities);
                var symbolPackages = CreateRepository(SymbolPackages);
                SymbolRepository.Setup(repository => repository.InsertOnCommit(It.IsAny<StagedSymbolPackage>()))
                    .Callback<StagedSymbolPackage>(attempt =>
                    {
                        attempt.Key = Symbols.Max(symbol => symbol.Key) + 1;
                        attempt.StagedPackageIdentityKey = attempt.StagedPackageIdentity.Key;
                        Symbols.Add(attempt);
                    });
                PackageService.Setup(service => service.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), false))
                    .Callback<Package, PackageStatus, bool>((package, status, commit) => package.PackageStatusKey = status)
                    .Returns(Task.CompletedTask);
                Deletion = new StagingDeletionService(
                    PackageRepository.Object, SymbolRepository.Object, identities.Object,
                    symbolPackages.Object, GroupRepository.Object, PackageService.Object, Cleanup.Object);
            }

            public List<StagingGroup> Groups { get; } = new List<StagingGroup>();

            public List<StagedPackage> Packages { get; } = new List<StagedPackage>();

            public List<StagedSymbolPackage> Symbols { get; } = new List<StagedSymbolPackage>();

            public List<StagedPackageIdentity> Identities { get; } = new List<StagedPackageIdentity>();

            public List<SymbolPackage> SymbolPackages { get; } = new List<SymbolPackage>();

            public Mock<IEntityRepository<StagingGroup>> GroupRepository { get; }

            public Mock<IEntityRepository<StagedPackage>> PackageRepository { get; }

            public Mock<IEntityRepository<StagedSymbolPackage>> SymbolRepository { get; }

            public Mock<ICorePackageService> PackageService { get; } = new Mock<ICorePackageService>();

            public Mock<IStagingBlobCleanupService> Cleanup { get; } = new Mock<IStagingBlobCleanupService>();

            public StagingDeletionService Deletion { get; }

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
                    .ProcessAsync(GroupRepository.Object, PackageRepository.Object, SymbolRepository.Object, Deletion);
            }

            private static Mock<IEntityRepository<T>> CreateRepository<T>(List<T> items) where T : class, new()
            {
                var set = new Mock<DbSet<T>>().SetupDbSet(items);
                set.Setup(value => value.Include(It.IsAny<string>())).Returns(set.Object);
                var repository = new Mock<IEntityRepository<T>>();
                repository.Setup(value => value.GetAll()).Returns(set.Object);
                repository.Setup(value => value.ExecuteInTransactionAsync(It.IsAny<Func<Task>>())).Returns<Func<Task>>(action => action());
                repository.Setup(value => value.CommitChangesAsync()).Returns(Task.CompletedTask);
                repository.Setup(value => value.DeleteOnCommit(It.IsAny<T>())).Callback<T>(item => items.Remove(item));
                return repository;
            }
        }
    }
}

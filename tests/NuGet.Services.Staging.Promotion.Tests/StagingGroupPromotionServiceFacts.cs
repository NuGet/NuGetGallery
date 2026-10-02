// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Staging.Promotion.Tests
{
    public class StagingGroupPromotionServiceFacts
    {
        [Fact]
        public async Task RetainsSuccessfulPackageUntilRemainingMembersComplete()
        {
            var context = new TestContext();
            var remainingMember = context.AddMember(43, StagedPackageStatus.Promoting);

            context.Target.MarkPackageSucceeded(context.StagedPackage);
            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Equal(StagedPackageStatus.Succeeded, context.StagedPackage.Status);
            Assert.Equal(StagedPackageStatus.Promoting, remainingMember.Status);
            Assert.Equal(2, context.StagedPackages.Count);
            Assert.Single(context.StagingGroups);
            context.StagedPackageRepository.Verify(
                x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()),
                Times.Once);
            context.StagedPackageRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagedPackage>()), Times.Never);
            context.StagingGroupRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagingGroup>()), Times.Never);
            context.StagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            context.BlobCleanup.Verify(service => service.QueuePackageFiles(It.IsAny<int>()), Times.Never);
            context.BlobCleanup.Verify(service => service.QueueSymbolFiles(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task RetainsEmptyGroupWhenAllMembersHaveSucceeded()
        {
            var context = new TestContext();
            var completedMember = context.AddMember(43, StagedPackageStatus.Succeeded);

            context.Target.MarkPackageSucceeded(context.StagedPackage);
            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Empty(context.StagedPackages);
            Assert.Same(context.StagingGroup, Assert.Single(context.StagingGroups));
            Assert.Null(context.StagingGroup.ActivePromotionId);
            Assert.Null(context.StagedPackage.StagedPackageIdentity.CurrentStagedPackageKey);
            Assert.Null(context.StagedPackage.StagedPackageIdentity.StagingGroupKey);
            Assert.Null(completedMember.StagedPackageIdentity.CurrentStagedPackageKey);
            Assert.Null(completedMember.StagedPackageIdentity.StagingGroupKey);
            context.StagedPackageRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagedPackage>()), Times.Exactly(2));
            context.StagedPackageIdentityRepository.Verify(x => x.DeleteOnCommit(context.StagedPackage.StagedPackageIdentity), Times.Once);
            context.StagedPackageIdentityRepository.Verify(x => x.DeleteOnCommit(completedMember.StagedPackageIdentity), Times.Once);
            context.StagingGroupRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagingGroup>()), Times.Never);
            context.StagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Exactly(2));
            context.BlobCleanup.Verify(service => service.QueuePackageFiles(42), Times.Once);
            context.BlobCleanup.Verify(service => service.QueueSymbolFiles(42), Times.Once);
            context.BlobCleanup.Verify(service => service.QueuePackageFiles(43), Times.Once);
            context.BlobCleanup.Verify(service => service.QueueSymbolFiles(43), Times.Once);
        }

        [Fact]
        public async Task UnlocksGroupWhenSomeMembersFailedAndOthersSucceeded()
        {
            var context = new TestContext();
            var failedMember = context.AddMember(43, StagedPackageStatus.PromotionFailed);

            context.Target.MarkPackageSucceeded(context.StagedPackage);
            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Same(failedMember, Assert.Single(context.StagedPackages));
            Assert.Same(context.StagingGroup, Assert.Single(context.StagingGroups));
            Assert.Null(context.StagingGroup.ActivePromotionId);
            Assert.Null(context.StagedPackage.StagedPackageIdentity.CurrentStagedPackageKey);
            Assert.Null(context.StagedPackage.StagedPackageIdentity.StagingGroupKey);
            Assert.Equal(failedMember.Key, failedMember.StagedPackageIdentity.CurrentStagedPackageKey);
            Assert.Equal(context.StagingGroup.Key, failedMember.StagedPackageIdentity.StagingGroupKey);
            Assert.Null(failedMember.ActivePromotionId);
            context.StagedPackageRepository.Verify(x => x.DeleteOnCommit(context.StagedPackage), Times.Once);
            context.StagedPackageRepository.Verify(x => x.DeleteOnCommit(failedMember), Times.Never);
            context.BlobCleanup.Verify(service => service.QueuePackageFiles(failedMember.StagedPackageIdentityKey), Times.Never);
            context.BlobCleanup.Verify(service => service.QueueSymbolFiles(failedMember.StagedPackageIdentityKey), Times.Never);
            context.StagedPackageIdentityRepository.Verify(x => x.DeleteOnCommit(context.StagedPackage.StagedPackageIdentity), Times.Once);
            context.StagedPackageIdentityRepository.Verify(x => x.DeleteOnCommit(failedMember.StagedPackageIdentity), Times.Never);
            context.StagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Exactly(2));
        }

        [Fact]
        public async Task RetainsIdentityAndMembershipWhenSymbolsRemain()
        {
            var context = new TestContext();
            var identity = context.StagedPackage.StagedPackageIdentity;
            identity.CurrentStagedSymbolPackageKey = 100;
            context.Target.MarkPackageSucceeded(context.StagedPackage);

            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Empty(context.StagedPackages);
            Assert.Null(identity.CurrentStagedPackageKey);
            Assert.Null(identity.CurrentStagedPackage);
            Assert.Equal(100, identity.CurrentStagedSymbolPackageKey);
            Assert.Same(context.StagingGroup, identity.StagingGroup);
            Assert.Equal(context.StagingGroup.Key, identity.StagingGroupKey);
            Assert.Null(context.StagingGroup.ActivePromotionId);
            context.StagedPackageIdentityRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagedPackageIdentity>()), Times.Never);
        }

        [Fact]
        public async Task RetainsPublishedArtifactsUntilSymbolOrchestrationCompletes()
        {
            var context = new TestContext();
            var symbols = context.AddSymbols(StagedPackageStatus.Promoting);
            context.Target.MarkPackageSucceeded(context.StagedPackage);

            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Equal(context.PromotionId, context.StagingGroup.ActivePromotionId);
            Assert.Single(context.StagedPackages);
            Assert.Single(context.StagedSymbols);
            context.StagedPackageRepository.Verify(repository => repository.CommitChangesAsync(), Times.Never);
            symbols.Status = StagedPackageStatus.Succeeded;

            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Empty(context.StagedPackages);
            Assert.Empty(context.StagedSymbols);
            Assert.Null(context.StagingGroup.ActivePromotionId);
            Assert.Null(symbols.StagedPackageIdentity.CurrentStagedSymbolPackageKey);
            context.StagedPackageIdentityRepository.Verify(repository => repository.DeleteOnCommit(symbols.StagedPackageIdentity), Times.Once);
        }

        [Fact]
        public async Task FailedSymbolsKeepTheirIdentityWithoutKeepingTheSuccessfulPackageAttempt()
        {
            var context = new TestContext();
            var symbols = context.AddSymbols(StagedPackageStatus.PromotionFailed);
            context.Target.MarkPackageSucceeded(context.StagedPackage);

            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Empty(context.StagedPackages);
            Assert.Same(symbols, Assert.Single(context.StagedSymbols));
            Assert.Null(context.StagingGroup.ActivePromotionId);
            Assert.Null(symbols.StagedPackageIdentity.CurrentStagedPackageKey);
            Assert.Equal(symbols.Key, symbols.StagedPackageIdentity.CurrentStagedSymbolPackageKey);
            Assert.Equal(context.StagingGroup.Key, symbols.StagedPackageIdentity.StagingGroupKey);
            Assert.Equal(context.PromotionId, symbols.ActivePromotionId);
            context.StagedPackageIdentityRepository.Verify(repository => repository.DeleteOnCommit(It.IsAny<StagedPackageIdentity>()), Times.Never);
        }

        [Fact]
        public async Task FinalizesASymbolOnlyGroup()
        {
            var context = new TestContext();
            var symbols = context.AddSymbols(StagedPackageStatus.Succeeded);
            context.StagedPackages.Clear();
            symbols.StagedPackageIdentity.CurrentStagedPackageKey = null;
            symbols.StagedPackageIdentity.CurrentStagedPackage = null;

            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Empty(context.StagedSymbols);
            Assert.Null(context.StagingGroup.ActivePromotionId);
            context.StagedPackageIdentityRepository.Verify(repository => repository.DeleteOnCommit(symbols.StagedPackageIdentity), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task FinalizesWhenDeletingSuccessfulAttemptsClearsTheirIdentityNavigation(bool symbolOnly)
        {
            var context = new TestContext();
            var symbols = context.AddSymbols(StagedPackageStatus.Succeeded);
            var identity = symbols.StagedPackageIdentity;
            context.Target.MarkPackageSucceeded(context.StagedPackage);
            if (symbolOnly)
            {
                context.StagedPackages.Clear();
                identity.CurrentStagedPackageKey = null;
                identity.CurrentStagedPackage = null;
            }

            context.StagedPackageRepository
                .Setup(repository => repository.DeleteOnCommit(It.IsAny<StagedPackage>()))
                .Callback<StagedPackage>(package =>
                {
                    context.PendingStagedPackageDeletes.Add(package);
                    package.StagedPackageIdentity = null;
                });
            context.StagedSymbolPackageRepository
                .Setup(repository => repository.DeleteOnCommit(It.IsAny<StagedSymbolPackage>()))
                .Callback<StagedSymbolPackage>(attempt =>
                {
                    context.PendingStagedSymbolDeletes.Add(attempt);
                    attempt.StagedPackageIdentity = null;
                });

            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Empty(context.StagedPackages);
            Assert.Empty(context.StagedSymbols);
            Assert.Null(identity.CurrentStagedPackageKey);
            Assert.Null(identity.CurrentStagedSymbolPackageKey);
            Assert.Null(identity.StagingGroupKey);
            Assert.Null(context.StagingGroup.ActivePromotionId);
            context.StagedPackageIdentityRepository.Verify(repository => repository.DeleteOnCommit(identity), Times.Once);
        }

        [Fact]
        public async Task UnlocksGroupWhenEveryMemberFailed()
        {
            var context = new TestContext();
            context.StagedPackage.Status = StagedPackageStatus.PromotionFailed;
            var failedMember = context.AddMember(43, StagedPackageStatus.PromotionFailed);

            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Equal(2, context.StagedPackages.Count);
            Assert.Null(context.StagingGroup.ActivePromotionId);
            Assert.Null(context.StagedPackage.ActivePromotionId);
            Assert.Null(failedMember.ActivePromotionId);
            context.StagedPackageRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagedPackage>()), Times.Never);
            context.StagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task KeepsGroupFrozenWhileOtherMembersAreStillPromoting()
        {
            var context = new TestContext();
            context.StagedPackage.Status = StagedPackageStatus.PromotionFailed;
            context.AddMember(43, StagedPackageStatus.Promoting);

            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Equal(context.PromotionId, context.StagingGroup.ActivePromotionId);
            Assert.Equal(2, context.StagedPackages.Count);
            context.StagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task AcceptsConcurrencyWhenAnotherHandlerCompletedFinalization()
        {
            var context = new TestContext();
            context.Target.MarkPackageSucceeded(context.StagedPackage);
            context.SimulateConcurrentFinalization();

            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Empty(context.StagedPackages);
            Assert.Same(context.StagingGroup, Assert.Single(context.StagingGroups));
            Assert.Null(context.StagingGroup.ActivePromotionId);
        }

        [Fact]
        public async Task PropagatesConcurrencyWhenPromotionRemainsActive()
        {
            var context = new TestContext();
            context.Target.MarkPackageSucceeded(context.StagedPackage);
            context.StagedPackageRepository
                .Setup(x => x.CommitChangesAsync())
                .Returns(() =>
                {
                    context.StagingGroup.ActivePromotionId = context.PromotionId;
                    return Task.FromException(new DbUpdateConcurrencyException());
                });

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId));
        }

        private class TestContext
        {
            public TestContext()
            {
                PromotionId = Guid.NewGuid();
                StagingGroup = new StagingGroup
                {
                    Key = 7,
                    ActivePromotionId = PromotionId,
                };
                StagingGroups = new List<StagingGroup> { StagingGroup };
                StagedPackages = new List<StagedPackage>();
                StagedPackage = AddMember(42, StagedPackageStatus.Promoting);

                StagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                StagedPackageIdentityRepository = new Mock<IEntityRepository<StagedPackageIdentity>>();
                StagedPackageIdentityRepository
                    .Setup(x => x.DeleteOnCommit(It.IsAny<StagedPackageIdentity>()))
                    .Callback(() => StagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once));
                StagedPackageRepository
                    .Setup(x => x.GetAll())
                    .Returns(() => StagedPackages.AsQueryable());
                StagedSymbolPackageRepository.Setup(repository => repository.GetAll()).Returns(() => StagedSymbols.AsQueryable());
                StagedSymbolPackageRepository.Setup(repository => repository.DeleteOnCommit(It.IsAny<StagedSymbolPackage>()))
                    .Callback<StagedSymbolPackage>(symbols => PendingStagedSymbolDeletes.Add(symbols));
                StagedPackageRepository
                    .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                    .Returns((Func<Task> action) => action());
                StagedPackageRepository
                    .Setup(x => x.DeleteOnCommit(It.IsAny<StagedPackage>()))
                    .Callback<StagedPackage>(stagedPackage => PendingStagedPackageDeletes.Add(stagedPackage));
                StagedPackageRepository
                    .Setup(x => x.CommitChangesAsync())
                    .Returns(() =>
                    {
                        foreach (var stagedPackage in PendingStagedPackageDeletes)
                        {
                            StagedPackages.Remove(stagedPackage);
                        }

                        foreach (var stagingGroup in PendingStagingGroupDeletes)
                        {
                            StagingGroups.Remove(stagingGroup);
                        }

                        foreach (var symbols in PendingStagedSymbolDeletes)
                        {
                            StagedSymbols.Remove(symbols);
                        }

                        return Task.CompletedTask;
                    });
                StagingGroupRepository = new Mock<IEntityRepository<StagingGroup>>();
                StagingGroupRepository
                    .Setup(x => x.GetAll())
                    .Returns(() => StagingGroups.AsQueryable());
                StagingGroupRepository
                    .Setup(x => x.DeleteOnCommit(It.IsAny<StagingGroup>()))
                    .Callback<StagingGroup>(stagingGroup => PendingStagingGroupDeletes.Add(stagingGroup));

                Target = new StagingGroupPromotionService(
                    StagedPackageRepository.Object,
                    StagedSymbolPackageRepository.Object,
                    StagedPackageIdentityRepository.Object,
                    StagingGroupRepository.Object,
                    BlobCleanup.Object,
                    Mock.Of<ILogger<StagingGroupPromotionService>>());
            }

            public void SimulateConcurrentFinalization()
            {
                StagedPackageRepository
                    .Setup(x => x.CommitChangesAsync())
                    .Returns(() =>
                    {
                        StagedPackages.Clear();
                        StagingGroup.ActivePromotionId = null;
                        return Task.FromException(new DbUpdateConcurrencyException());
                    });
            }

            public StagedSymbolPackage AddSymbols(StagedPackageStatus status)
            {
                var identity = StagedPackage.StagedPackageIdentity;
                var symbols = new StagedSymbolPackage
                {
                    Key = 100,
                    StagedPackageIdentity = identity,
                    StagedPackageIdentityKey = identity.Key,
                    ActivePromotionId = PromotionId,
                    Status = status,
                    SymbolPackage = new SymbolPackage { StatusKey = PackageStatus.Available },
                };
                identity.CurrentStagedSymbolPackageKey = symbols.Key;
                identity.CurrentStagedSymbolPackage = symbols;
                StagedSymbols.Add(symbols);
                return symbols;
            }

            public StagedPackage AddMember(int key, StagedPackageStatus status)
            {
                var identity = new StagedPackageIdentity
                {
                    Key = key,
                    StagingGroupKey = StagingGroup.Key,
                    StagingGroup = StagingGroup,
                };
                var stagedPackage = new StagedPackage
                {
                    Key = key,
                    StagedPackageIdentityKey = identity.Key,
                    StagedPackageIdentity = identity,
                    Status = status,
                    ActivePromotionId = PromotionId,
                };
                identity.CurrentStagedPackageKey = stagedPackage.Key;
                identity.CurrentStagedPackage = stagedPackage;
                StagedPackages.Add(stagedPackage);
                return stagedPackage;
            }

            public Guid PromotionId { get; }
            public StagingGroup StagingGroup { get; }
            public StagedPackage StagedPackage { get; }
            public List<StagingGroup> StagingGroups { get; }
            public List<StagedPackage> StagedPackages { get; }

            public List<StagedSymbolPackage> StagedSymbols { get; } = new List<StagedSymbolPackage>();

            public List<StagedSymbolPackage> PendingStagedSymbolDeletes { get; } = new List<StagedSymbolPackage>();

            public List<StagingGroup> PendingStagingGroupDeletes { get; } = new List<StagingGroup>();
            public List<StagedPackage> PendingStagedPackageDeletes { get; } = new List<StagedPackage>();
            public Mock<IEntityRepository<StagedPackage>> StagedPackageRepository { get; }

            public Mock<IEntityRepository<StagedSymbolPackage>> StagedSymbolPackageRepository { get; } = new Mock<IEntityRepository<StagedSymbolPackage>>();

            public Mock<IEntityRepository<StagedPackageIdentity>> StagedPackageIdentityRepository { get; }
            public Mock<IEntityRepository<StagingGroup>> StagingGroupRepository { get; }
            public Mock<IStagingBlobCleanupService> BlobCleanup { get; } = new Mock<IStagingBlobCleanupService>();
            public StagingGroupPromotionService Target { get; }
        }
    }
}

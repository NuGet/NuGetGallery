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
        }

        [Fact]
        public async Task RemovesGroupWhenAllMembersHaveSucceeded()
        {
            var context = new TestContext();
            var completedMember = context.AddMember(43, StagedPackageStatus.Succeeded);

            context.Target.MarkPackageSucceeded(context.StagedPackage);
            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Empty(context.StagedPackages);
            Assert.Empty(context.StagingGroups);
            Assert.Null(context.StagedPackage.StagedPackageIdentity.CurrentStagedPackageKey);
            Assert.Null(context.StagedPackage.StagedPackageIdentity.StagingGroupKey);
            Assert.Null(completedMember.StagedPackageIdentity.CurrentStagedPackageKey);
            Assert.Null(completedMember.StagedPackageIdentity.StagingGroupKey);
            context.StagedPackageRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagedPackage>()), Times.Exactly(2));
            context.StagingGroupRepository.Verify(x => x.DeleteOnCommit(context.StagingGroup), Times.Once);
            context.StagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task AcceptsConcurrencyWhenAnotherHandlerCompletedFinalization()
        {
            var context = new TestContext();
            context.Target.MarkPackageSucceeded(context.StagedPackage);
            context.SimulateConcurrentFinalization();

            await context.Target.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId);

            Assert.Empty(context.StagedPackages);
            Assert.Empty(context.StagingGroups);
        }

        [Fact]
        public async Task PropagatesConcurrencyWhenPromotionRemainsActive()
        {
            var context = new TestContext();
            context.Target.MarkPackageSucceeded(context.StagedPackage);
            context.StagedPackageRepository
                .Setup(x => x.CommitChangesAsync())
                .ThrowsAsync(new DbUpdateConcurrencyException());

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
                StagedPackageRepository
                    .Setup(x => x.GetAll())
                    .Returns(() => StagedPackages.AsQueryable());
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
                    StagingGroupRepository.Object,
                    Mock.Of<ILogger<StagingGroupPromotionService>>());
            }

            public void SimulateConcurrentFinalization()
            {
                StagedPackageRepository
                    .Setup(x => x.CommitChangesAsync())
                    .Returns(() =>
                    {
                        StagedPackages.Clear();
                        StagingGroups.Clear();
                        return Task.FromException(new DbUpdateConcurrencyException());
                    });
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
            public List<StagingGroup> PendingStagingGroupDeletes { get; } = new List<StagingGroup>();
            public List<StagedPackage> PendingStagedPackageDeletes { get; } = new List<StagedPackage>();
            public Mock<IEntityRepository<StagedPackage>> StagedPackageRepository { get; }
            public Mock<IEntityRepository<StagingGroup>> StagingGroupRepository { get; }
            public StagingGroupPromotionService Target { get; }
        }
    }
}

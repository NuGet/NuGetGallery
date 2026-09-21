// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
        public void RetainsSuccessfulPackageUntilRemainingMembersComplete()
        {
            var context = new TestContext();
            var remainingMember = context.AddMember(43, StagedPackageStatus.Promoting);

            context.Target.CompletePackage(context.StagedPackage);

            Assert.Equal(StagedPackageStatus.Succeeded, context.StagedPackage.Status);
            Assert.Equal(StagedPackageStatus.Promoting, remainingMember.Status);
            Assert.Equal(2, context.StagedPackages.Count);
            Assert.Single(context.StagingGroups);
            context.StagedPackageRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagedPackage>()), Times.Never);
            context.StagingGroupRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagingGroup>()), Times.Never);
        }

        [Fact]
        public void RemovesGroupWhenAllMembersHaveSucceeded()
        {
            var context = new TestContext();
            var completedMember = context.AddMember(43, StagedPackageStatus.Succeeded);

            context.Target.CompletePackage(context.StagedPackage);

            Assert.Empty(context.StagedPackages);
            Assert.Empty(context.StagingGroups);
            Assert.Null(context.StagedPackage.StagedPackageIdentity.CurrentStagedPackageKey);
            Assert.Null(context.StagedPackage.StagedPackageIdentity.StagingGroupKey);
            Assert.Null(completedMember.StagedPackageIdentity.CurrentStagedPackageKey);
            Assert.Null(completedMember.StagedPackageIdentity.StagingGroupKey);
            context.StagedPackageRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagedPackage>()), Times.Exactly(2));
            context.StagingGroupRepository.Verify(x => x.DeleteOnCommit(context.StagingGroup), Times.Once);
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
                StagedPackageRepository.Setup(x => x.GetAll()).Returns(() => StagedPackages.AsQueryable());
                StagedPackageRepository
                    .Setup(x => x.DeleteOnCommit(It.IsAny<StagedPackage>()))
                    .Callback<StagedPackage>(stagedPackage => StagedPackages.Remove(stagedPackage));
                StagingGroupRepository = new Mock<IEntityRepository<StagingGroup>>();
                StagingGroupRepository
                    .Setup(x => x.DeleteOnCommit(It.IsAny<StagingGroup>()))
                    .Callback<StagingGroup>(stagingGroup => StagingGroups.Remove(stagingGroup));

                Target = new StagingGroupPromotionService(
                    StagedPackageRepository.Object,
                    StagingGroupRepository.Object,
                    Mock.Of<ILogger<StagingGroupPromotionService>>());
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
            public Mock<IEntityRepository<StagedPackage>> StagedPackageRepository { get; }
            public Mock<IEntityRepository<StagingGroup>> StagingGroupRepository { get; }
            public StagingGroupPromotionService Target { get; }
        }
    }
}

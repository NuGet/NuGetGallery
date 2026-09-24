// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Staging.Promotion.Tests
{
    public class StagingGroupPromotionMessageHandlerFacts
    {
        [Fact]
        public async Task FansOutCurrentActivePackageAttempts()
        {
            var context = new TestContext();
            context.AddPackage(43, StagedPackageStatus.Promoting, context.PromotionId, isCurrent: true);
            context.AddPackage(44, StagedPackageStatus.Ready, context.PromotionId, isCurrent: true);
            context.AddPackage(45, StagedPackageStatus.Promoting, Guid.NewGuid(), isCurrent: true);
            context.AddPackage(46, StagedPackageStatus.Promoting, context.PromotionId, isCurrent: false);

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            Assert.Collection(
                context.SentMessages,
                message => Assert.Equal(42, message.TargetKey),
                message => Assert.Equal(43, message.TargetKey));
            Assert.All(context.SentMessages, message =>
            {
                Assert.Equal(context.PromotionId, message.PromotionId);
                Assert.Equal(StagingPromotionTargetType.StagedPackage, message.TargetType);
            });
            context.GroupPromotionService.Verify(x => x.TryFinalizeAsync(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task ConsumesRootMessageAfterPromotionCompletes()
        {
            var context = new TestContext();
            context.Group.ActivePromotionId = null;

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            Assert.Empty(context.SentMessages);
            context.GroupPromotionService.Verify(x => x.TryFinalizeAsync(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task ConsumesMessageForDifferentActivePromotion()
        {
            var context = new TestContext();
            context.Group.ActivePromotionId = Guid.NewGuid();

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            Assert.Empty(context.SentMessages);
        }

        [Fact]
        public async Task RetriesRootWhenFanOutIsPartial()
        {
            var context = new TestContext();
            context.AddPackage(43, StagedPackageStatus.Promoting, context.PromotionId, isCurrent: true);
            var expected = new InvalidOperationException("Send failed.");
            context.MessageEnqueuer
                .Setup(x => x.SendMessageAsync(It.Is<StagingPromotionMessage>(message => message.TargetKey == 43)))
                .ThrowsAsync(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Target.HandleAsync(context.Message));

            Assert.Same(expected, actual);
            Assert.Collection(context.SentMessages, message => Assert.Equal(42, message.TargetKey));
        }

        [Fact]
        public async Task FinalizesGroupWhenNoPromotingPackagesRemain()
        {
            var context = new TestContext();
            context.Packages[0].Status = StagedPackageStatus.PromotionFailed;

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            Assert.Empty(context.SentMessages);
            context.GroupPromotionService.Verify(x => x.TryFinalizeAsync(context.Group.Key, context.PromotionId), Times.Once);
        }

        private class TestContext
        {
            public TestContext()
            {
                PromotionId = Guid.NewGuid();
                Group = new StagingGroup { Key = 7, ActivePromotionId = PromotionId };
                Groups = new List<StagingGroup> { Group };
                Packages = new List<StagedPackage>();
                SentMessages = new List<StagingPromotionMessage>();
                Message = StagingPromotionMessage.ForGroup(PromotionId, Group.Key);

                StagingGroupRepository = new Mock<IEntityRepository<StagingGroup>>();
                StagingGroupRepository.Setup(x => x.GetAll()).Returns(() => Groups.AsQueryable());
                StagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                StagedPackageRepository.Setup(x => x.GetAll()).Returns(() => Packages.AsQueryable());
                MessageEnqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
                MessageEnqueuer
                    .Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                    .Callback<StagingPromotionMessage>(message => SentMessages.Add(message))
                    .Returns(Task.CompletedTask);
                GroupPromotionService = new Mock<IStagingGroupPromotionService>();
                GroupPromotionService
                    .Setup(x => x.TryFinalizeAsync(It.IsAny<int>(), It.IsAny<Guid>()))
                    .Returns(Task.CompletedTask);

                AddPackage(42, StagedPackageStatus.Promoting, PromotionId, isCurrent: true);
                Target = new StagingGroupPromotionMessageHandler(
                    StagingGroupRepository.Object,
                    StagedPackageRepository.Object,
                    MessageEnqueuer.Object,
                    GroupPromotionService.Object,
                    Mock.Of<ILogger<StagingGroupPromotionMessageHandler>>());
            }

            public void AddPackage(int key, StagedPackageStatus status, Guid promotionId, bool isCurrent)
            {
                var identity = new StagedPackageIdentity
                {
                    Key = key,
                    StagingGroupKey = Group.Key,
                    StagingGroup = Group,
                    CurrentStagedPackageKey = isCurrent ? key : key + 1000,
                };
                var stagedPackage = new StagedPackage
                {
                    Key = key,
                    StagedPackageIdentityKey = identity.Key,
                    StagedPackageIdentity = identity,
                    Status = status,
                    ActivePromotionId = promotionId,
                };
                identity.CurrentStagedPackage = isCurrent ? stagedPackage : null;
                Packages.Add(stagedPackage);
            }

            public Guid PromotionId { get; }
            public StagingGroup Group { get; }
            public List<StagingGroup> Groups { get; }
            public List<StagedPackage> Packages { get; }
            public List<StagingPromotionMessage> SentMessages { get; }
            public StagingPromotionMessage Message { get; }
            public Mock<IEntityRepository<StagingGroup>> StagingGroupRepository { get; }
            public Mock<IEntityRepository<StagedPackage>> StagedPackageRepository { get; }
            public Mock<IStagingPromotionMessageEnqueuer> MessageEnqueuer { get; }
            public Mock<IStagingGroupPromotionService> GroupPromotionService { get; }
            public StagingGroupPromotionMessageHandler Target { get; }
        }
    }
}

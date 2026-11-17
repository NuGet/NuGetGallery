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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DispatchesSymbolsOnlyAfterTheirStagedParentFinishes(bool parentPublished)
        {
            var context = new TestContext();
            if (parentPublished)
            {
                context.Packages[0].Status = StagedPackageStatus.Succeeded;
            }

            var paired = context.AddSymbols(100, context.Packages[0]);
            var standalone = context.AddSymbols(101);
            context.AddSymbols(102).ActivePromotionId = Guid.NewGuid();
            context.AddSymbols(103).StagedPackageIdentity.CurrentStagedSymbolPackageKey = 999;

            Assert.True(await context.Target.HandleAsync(context.Message));

            var firstMessage = StagingPromotionMessage.ForPackage(context.PromotionId, context.Packages[0].Key);
            if (parentPublished)
            {
                firstMessage = StagingPromotionMessage.ForSymbolPackage(context.PromotionId, paired.Key);
            }

            Assert.Collection(context.SentMessages,
                message =>
                {
                    Assert.Equal(firstMessage.TargetKey, message.TargetKey);
                    Assert.Equal(firstMessage.TargetType, message.TargetType);
                },
                message =>
                {
                    Assert.Equal(standalone.Key, message.TargetKey);
                    Assert.Equal(StagingPromotionTargetType.StagedSymbolPackage, message.TargetType);
                });
            Assert.All(context.SentMessages, message => Assert.Equal(context.PromotionId, message.PromotionId));
            Assert.Equal(parentPublished, paired.PromotionMessageSentDate.HasValue);
            Assert.NotNull(standalone.PromotionMessageSentDate);
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RetriesRootWhenFanOutIsPartial(bool symbolFails)
        {
            var context = new TestContext();
            var failedKey = 43;
            if (symbolFails)
            {
                failedKey = context.AddSymbols(100).Key;
            }
            else
            {
                context.AddPackage(43, StagedPackageStatus.Promoting, context.PromotionId, isCurrent: true);
            }

            var expected = new InvalidOperationException("Send failed.");
            context.MessageEnqueuer
                .Setup(x => x.SendMessageAsync(It.Is<StagingPromotionMessage>(message => message.TargetKey == failedKey)))
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
                StagedSymbolPackageRepository.Setup(repository => repository.GetAll()).Returns(() => Symbols.AsQueryable());
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
                    StagedSymbolPackageRepository.Object,
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

            public StagedSymbolPackage AddSymbols(int key, StagedPackage parent = null)
            {
                var identity = parent?.StagedPackageIdentity ?? new StagedPackageIdentity
                {
                    Key = key,
                    StagingGroupKey = Group.Key,
                    StagingGroup = Group,
                };
                var symbols = new StagedSymbolPackage
                {
                    Key = key,
                    StagedPackageIdentity = identity,
                    StagedPackageIdentityKey = identity.Key,
                    Status = StagedPackageStatus.Promoting,
                    ActivePromotionId = PromotionId,
                };
                identity.CurrentStagedSymbolPackageKey = symbols.Key;
                identity.CurrentStagedSymbolPackage = symbols;
                Symbols.Add(symbols);
                return symbols;
            }

            public Guid PromotionId { get; }
            public StagingGroup Group { get; }
            public List<StagingGroup> Groups { get; }
            public List<StagedPackage> Packages { get; }

            public List<StagedSymbolPackage> Symbols { get; } = new List<StagedSymbolPackage>();

            public List<StagingPromotionMessage> SentMessages { get; }
            public StagingPromotionMessage Message { get; }
            public Mock<IEntityRepository<StagingGroup>> StagingGroupRepository { get; }
            public Mock<IEntityRepository<StagedPackage>> StagedPackageRepository { get; }

            public Mock<IEntityRepository<StagedSymbolPackage>> StagedSymbolPackageRepository { get; } = new Mock<IEntityRepository<StagedSymbolPackage>>(MockBehavior.Loose);

            public Mock<IStagingPromotionMessageEnqueuer> MessageEnqueuer { get; }
            public Mock<IStagingGroupPromotionService> GroupPromotionService { get; }
            public StagingGroupPromotionMessageHandler Target { get; }
        }
    }
}

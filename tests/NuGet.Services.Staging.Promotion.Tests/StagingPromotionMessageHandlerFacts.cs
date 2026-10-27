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
    public class StagingPromotionMessageHandlerFacts
    {
        [Fact]
        public async Task RoutesGroupPromotion()
        {
            var message = StagingPromotionMessage.ForGroup(Guid.NewGuid(), 12);
            var groupHandler = new Mock<IStagingPromotionMessageHandler<StagingGroup>>();
            groupHandler.Setup(x => x.HandleAsync(message)).ReturnsAsync(true);
            var packageHandler = new Mock<IStagingPromotionMessageHandler<StagedPackage>>();
            var target = CreateTarget(groupHandler, packageHandler);

            var result = await target.HandleAsync(message);

            Assert.True(result);
            groupHandler.Verify(x => x.HandleAsync(message), Times.Once);
            packageHandler.Verify(x => x.HandleAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task RoutesPackagePromotion()
        {
            var message = StagingPromotionMessage.ForPackage(Guid.NewGuid(), 34);
            var groupHandler = new Mock<IStagingPromotionMessageHandler<StagingGroup>>();
            var packageHandler = new Mock<IStagingPromotionMessageHandler<StagedPackage>>();
            packageHandler.Setup(x => x.HandleAsync(message)).ReturnsAsync(true);
            var target = CreateTarget(groupHandler, packageHandler);

            var result = await target.HandleAsync(message);

            Assert.True(result);
            packageHandler.Verify(x => x.HandleAsync(message), Times.Once);
            groupHandler.Verify(x => x.HandleAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task ReturnsFalseWhenSymbolDispatcherDoesNotHandleMessage()
        {
            var groupHandler = new Mock<IStagingPromotionMessageHandler<StagingGroup>>(MockBehavior.Strict);
            var packageHandler = new Mock<IStagingPromotionMessageHandler<StagedPackage>>(MockBehavior.Strict);
            var symbolHandler = new Mock<IStagingPromotionMessageHandler<StagedSymbolPackage>>(MockBehavior.Strict);
            var message = StagingPromotionMessage.ForSymbolPackage(Guid.NewGuid(), 43);
            symbolHandler.Setup(handler => handler.HandleAsync(message)).ReturnsAsync(false);
            var dispatcher = new Lazy<IStagingPromotionMessageHandler<StagedSymbolPackage>>(() => symbolHandler.Object);
            var target = CreateTarget(groupHandler, packageHandler, dispatcher);

            Assert.False(await target.HandleAsync(message));

            symbolHandler.Verify(handler => handler.HandleAsync(message), Times.Once);
        }

        [Theory]
        [InlineData(StagingPromotionTargetType.StagedSymbolPackage)]
        [InlineData(StagingPromotionTargetType.StagingGroup)]
        public async Task ContendingMessageDoesNotEnterItsHandlerUntilLeaseIsReleased(StagingPromotionTargetType targetType)
        {
            var promotionId = Guid.NewGuid();
            var firstMessage = StagingPromotionMessage.ForPackage(promotionId, 34);
            var secondMessage = StagingPromotionMessage.ForSymbolPackage(promotionId, 43);
            var groupHandler = new Mock<IStagingPromotionMessageHandler<StagingGroup>>();
            var packageHandler = new Mock<IStagingPromotionMessageHandler<StagedPackage>>();
            var symbolHandler = new Mock<IStagingPromotionMessageHandler<StagedSymbolPackage>>();
            var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (targetType == StagingPromotionTargetType.StagingGroup)
            {
                firstMessage = StagingPromotionMessage.ForGroup(promotionId, 12);
                secondMessage = firstMessage;
                groupHandler.Setup(handler => handler.HandleAsync(firstMessage)).Returns(finish.Task);
            }
            else
            {
                packageHandler.Setup(handler => handler.HandleAsync(firstMessage)).Returns(finish.Task);
                symbolHandler.Setup(handler => handler.HandleAsync(secondMessage)).ReturnsAsync(true);
            }

            var leases = new TestLeaseService();
            var first = CreateTarget(groupHandler, packageHandler, new Lazy<IStagingPromotionMessageHandler<StagedSymbolPackage>>(() => symbolHandler.Object),
                new StagingPromotionLease(leases, Mock.Of<ILogger<StagingPromotionLease>>()));
            var second = CreateTarget(groupHandler, packageHandler, new Lazy<IStagingPromotionMessageHandler<StagedSymbolPackage>>(() => symbolHandler.Object),
                new StagingPromotionLease(leases, Mock.Of<ILogger<StagingPromotionLease>>()));
            var firstWork = first.HandleAsync(firstMessage);
            try
            {
                Assert.False(await second.HandleAsync(secondMessage));
                symbolHandler.Verify(handler => handler.HandleAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
                if (targetType == StagingPromotionTargetType.StagingGroup)
                {
                    groupHandler.Verify(handler => handler.HandleAsync(firstMessage), Times.Once);
                }
                else
                {
                    groupHandler.Verify(handler => handler.HandleAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
                }
            }
            finally
            {
                finish.TrySetResult(true);
                await firstWork;
            }

            Assert.True(await second.HandleAsync(secondMessage));
            if (targetType == StagingPromotionTargetType.StagedSymbolPackage)
            {
                symbolHandler.Verify(handler => handler.HandleAsync(secondMessage), Times.Once);
            }
            else
            {
                groupHandler.Verify(handler => handler.HandleAsync(secondMessage), Times.Exactly(2));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MissingAttemptIsConsumedWithoutEnteringAHandlerOrAcquiringALease(bool symbols)
        {
            var groupHandler = new Mock<IStagingPromotionMessageHandler<StagingGroup>>(MockBehavior.Strict);
            var packageHandler = new Mock<IStagingPromotionMessageHandler<StagedPackage>>(MockBehavior.Strict);
            var symbolHandler = new Lazy<IStagingPromotionMessageHandler<StagedSymbolPackage>>(() => throw new InvalidOperationException("Missing attempt dispatched."));
            var lease = new Mock<IStagingPromotionLease>(MockBehavior.Strict);
            var target = CreateTarget(groupHandler, packageHandler, symbolHandler, lease.Object);
            var message = StagingPromotionMessage.ForPackage(Guid.NewGuid(), 999);
            if (symbols)
            {
                message = StagingPromotionMessage.ForSymbolPackage(message.PromotionId, 999);
            }

            Assert.True(await target.HandleAsync(message));
            lease.VerifyNoOtherCalls();
        }

        private static StagingPromotionMessageHandler CreateTarget(
            Mock<IStagingPromotionMessageHandler<StagingGroup>> groupHandler,
            Mock<IStagingPromotionMessageHandler<StagedPackage>> packageHandler,
            Lazy<IStagingPromotionMessageHandler<StagedSymbolPackage>> symbolHandler = null,
            IStagingPromotionLease lease = null)
        {
            return new StagingPromotionMessageHandler(
                groupHandler.Object,
                packageHandler.Object,
                symbolHandler ?? new Lazy<IStagingPromotionMessageHandler<StagedSymbolPackage>>(() => Mock.Of<IStagingPromotionMessageHandler<StagedSymbolPackage>>()),
                CreateRepository(new StagedPackage { Key = 34, StagedPackageIdentityKey = 11 }),
                CreateRepository(new StagedSymbolPackage { Key = 43, StagedPackageIdentityKey = 11 }),
                lease ?? CreateLease(),
                Mock.Of<ILogger<StagingPromotionMessageHandler>>());
        }

        private static IEntityRepository<T> CreateRepository<T>(T entity) where T : class, new()
        {
            var repository = new Mock<IEntityRepository<T>>();
            repository.Setup(target => target.GetAll()).Returns(new List<T> { entity }.AsQueryable());
            return repository.Object;
        }

        private static IStagingPromotionLease CreateLease()
        {
            var lease = new Mock<IStagingPromotionLease>();
            lease.Setup(target => target.RunAsync(It.IsAny<string>(), It.IsAny<Func<Task<bool>>>()))
                .Returns<string, Func<Task<bool>>>((_, process) => process());
            return lease.Object;
        }
    }
}

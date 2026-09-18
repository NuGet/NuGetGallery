// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
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

        private static StagingPromotionMessageHandler CreateTarget(
            Mock<IStagingPromotionMessageHandler<StagingGroup>> groupHandler,
            Mock<IStagingPromotionMessageHandler<StagedPackage>> packageHandler)
        {
            return new StagingPromotionMessageHandler(
                groupHandler.Object,
                packageHandler.Object,
                Mock.Of<ILogger<StagingPromotionMessageHandler>>());
        }
    }
}

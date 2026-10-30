// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class SymbolValidationMessageHandlerRouterFacts
    {
        [Theory]
        [InlineData(PackageValidationMessageType.ProcessValidationSet, ValidatingType.SymbolPackage)]
        [InlineData(PackageValidationMessageType.ProcessValidationSet, ValidatingType.StagedSymbolPackage)]
        [InlineData(PackageValidationMessageType.CheckValidator, ValidatingType.SymbolPackage)]
        [InlineData(PackageValidationMessageType.CheckValidator, ValidatingType.StagedSymbolPackage)]
        [InlineData(PackageValidationMessageType.FailValidationSet, ValidatingType.SymbolPackage)]
        [InlineData(PackageValidationMessageType.FailValidationSet, ValidatingType.StagedSymbolPackage)]
        public async Task RoutesMessageToItsValidatingType(PackageValidationMessageType messageType, ValidatingType validatingType)
        {
            var symbolHandler = new Mock<IValidationMessageHandler<SymbolPackage>>();
            symbolHandler.Setup(x => x.HandleAsync(It.IsAny<PackageValidationMessageData>())).ReturnsAsync(true);
            var stagedHandler = new Mock<IValidationMessageHandler<StagedSymbolPackage>>();
            stagedHandler.Setup(x => x.HandleAsync(It.IsAny<PackageValidationMessageData>())).ReturnsAsync(true);
            var storage = new Mock<IValidationStorageService>();
            var message = CreateMessage(messageType, validatingType, storage);
            var router = new SymbolValidationMessageHandlerRouter(symbolHandler.Object, stagedHandler.Object,
                Mock.Of<IStagedSymbolPackagePromotionValidationMessageHandler>(), Mock.Of<IEntityService<StagedSymbolPackage>>(),
                storage.Object, Mock.Of<ILogger<SymbolValidationMessageHandlerRouter>>());

            Assert.True(await router.HandleAsync(message));
            symbolHandler.Verify(x => x.HandleAsync(message), validatingType == ValidatingType.SymbolPackage ? Times.Once() : Times.Never());
            stagedHandler.Verify(x => x.HandleAsync(message), validatingType == ValidatingType.StagedSymbolPackage ? Times.Once() : Times.Never());
        }

        [Fact]
        public async Task RejectsPackageMessages()
        {
            var storage = new Mock<IValidationStorageService>();
            var message = CreateMessage(PackageValidationMessageType.ProcessValidationSet, ValidatingType.Package, storage);
            var router = new SymbolValidationMessageHandlerRouter(
                Mock.Of<IValidationMessageHandler<SymbolPackage>>(), Mock.Of<IValidationMessageHandler<StagedSymbolPackage>>(),
                Mock.Of<IStagedSymbolPackagePromotionValidationMessageHandler>(), Mock.Of<IEntityService<StagedSymbolPackage>>(),
                storage.Object, Mock.Of<ILogger<SymbolValidationMessageHandlerRouter>>());

            await Assert.ThrowsAsync<NotSupportedException>(() => router.HandleAsync(message));
        }

        [Theory]
        [InlineData(PackageValidationMessageType.ProcessValidationSet)]
        [InlineData(PackageValidationMessageType.CheckValidator)]
        [InlineData(PackageValidationMessageType.FailValidationSet)]
        public async Task PersistedIngestionSetAlwaysUsesPromotionHandler(PackageValidationMessageType messageType)
        {
            var storage = new Mock<IValidationStorageService>();
            var set = new PackageValidationSet
            {
                ValidatingType = ValidatingType.StagedSymbolPackage,
                PackageValidations = new[] { new PackageValidation { Type = ValidatorName.SymbolsIngester } },
            };
            storage.Setup(service => service.GetValidationSetAsync(It.IsAny<Guid>())).ReturnsAsync(set);
            storage.Setup(service => service.TryGetParentValidationSetAsync(It.IsAny<Guid>())).ReturnsAsync(set);
            var message = CreateMessage(messageType, ValidatingType.StagedSymbolPackage, new Mock<IValidationStorageService>());
            var promotion = new Mock<IStagedSymbolPackagePromotionValidationMessageHandler>();
            promotion.Setup(handler => handler.HandleAsync(message)).ReturnsAsync(true);
            var router = new SymbolValidationMessageHandlerRouter(Mock.Of<IValidationMessageHandler<SymbolPackage>>(MockBehavior.Strict),
                Mock.Of<IValidationMessageHandler<StagedSymbolPackage>>(MockBehavior.Strict), promotion.Object,
                Mock.Of<IEntityService<StagedSymbolPackage>>(MockBehavior.Strict), storage.Object, Mock.Of<ILogger<SymbolValidationMessageHandlerRouter>>());

            Assert.True(await router.HandleAsync(message));

            promotion.Verify(handler => handler.HandleAsync(message), Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task InitialPromotionRoutingRequiresAcceptedTrackingId(bool matchesPromotion)
        {
            var attempt = new StagedSymbolPackage { Key = 43, ActivePromotionId = Guid.NewGuid(), Status = StagedPackageStatus.Promoting };
            var entities = new Mock<IEntityService<StagedSymbolPackage>>();
            entities.Setup(service => service.FindPackageByKey(attempt.Key)).Returns(new StagedSymbolPackageValidatingEntity(attempt));
            var trackingId = SymbolPromotionValidationTrackingId.Create(attempt.ActivePromotionId.Value, attempt.Key);
            var message = PackageValidationMessageData.NewProcessValidationSet("PackageA", "1.0.0",
                matchesPromotion ? trackingId : Guid.NewGuid(), ValidatingType.StagedSymbolPackage, attempt.Key);
            var staged = new Mock<IValidationMessageHandler<StagedSymbolPackage>>();
            staged.Setup(handler => handler.HandleAsync(message)).ReturnsAsync(true);
            var promotion = new Mock<IStagedSymbolPackagePromotionValidationMessageHandler>();
            promotion.Setup(handler => handler.HandleAsync(message)).ReturnsAsync(true);
            var router = new SymbolValidationMessageHandlerRouter(Mock.Of<IValidationMessageHandler<SymbolPackage>>(MockBehavior.Strict),
                staged.Object, promotion.Object, entities.Object, Mock.Of<IValidationStorageService>(), Mock.Of<ILogger<SymbolValidationMessageHandlerRouter>>());

            Assert.True(await router.HandleAsync(message));

            promotion.Verify(handler => handler.HandleAsync(message), matchesPromotion ? Times.Once() : Times.Never());
            staged.Verify(handler => handler.HandleAsync(message), matchesPromotion ? Times.Never() : Times.Once());
        }

        [Fact]
        public async Task PersistedScanSetCannotBeReroutedAsPromotion()
        {
            var trackingId = Guid.NewGuid();
            var storage = new Mock<IValidationStorageService>();
            storage.Setup(service => service.GetValidationSetAsync(trackingId)).ReturnsAsync(new PackageValidationSet
            {
                ValidatingType = ValidatingType.StagedSymbolPackage,
                PackageValidations = new[] { new PackageValidation { Type = ValidatorName.SymbolScan } },
            });
            var message = PackageValidationMessageData.NewProcessValidationSet("PackageA", "1.0.0", trackingId, ValidatingType.StagedSymbolPackage, 43);
            var staged = new Mock<IValidationMessageHandler<StagedSymbolPackage>>();
            staged.Setup(handler => handler.HandleAsync(message)).ReturnsAsync(true);
            var router = new SymbolValidationMessageHandlerRouter(Mock.Of<IValidationMessageHandler<SymbolPackage>>(MockBehavior.Strict),
                staged.Object, Mock.Of<IStagedSymbolPackagePromotionValidationMessageHandler>(MockBehavior.Strict),
                Mock.Of<IEntityService<StagedSymbolPackage>>(MockBehavior.Strict), storage.Object, Mock.Of<ILogger<SymbolValidationMessageHandlerRouter>>());

            Assert.True(await router.HandleAsync(message));

            staged.Verify(handler => handler.HandleAsync(message), Times.Once);
        }

        private static PackageValidationMessageData CreateMessage(
            PackageValidationMessageType messageType,
            ValidatingType validatingType,
            Mock<IValidationStorageService> storage)
        {
            switch (messageType)
            {
                case PackageValidationMessageType.ProcessValidationSet:
                    return PackageValidationMessageData.NewProcessValidationSet(
                        "PackageA", "1.0.0", Guid.NewGuid(), validatingType, entityKey: 43);
                case PackageValidationMessageType.CheckValidator:
                    var validationId = Guid.NewGuid();
                    storage.Setup(x => x.TryGetParentValidationSetAsync(validationId))
                        .ReturnsAsync(new PackageValidationSet
                        {
                            ValidatingType = validatingType,
                            PackageValidations = new[] { new PackageValidation { Type = ValidatorName.SymbolScan } },
                        });
                    return PackageValidationMessageData.NewCheckValidator(validationId);
                case PackageValidationMessageType.FailValidationSet:
                    var trackingId = Guid.NewGuid();
                    storage.Setup(x => x.GetValidationSetAsync(trackingId))
                        .ReturnsAsync(new PackageValidationSet
                        {
                            ValidatingType = validatingType,
                            PackageValidations = new[] { new PackageValidation { Type = ValidatorName.SymbolScan } },
                        });
                    return PackageValidationMessageData.NewFailValidationSet(trackingId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(messageType));
            }
        }
    }
}

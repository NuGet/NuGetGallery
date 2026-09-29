// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
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
                storage.Object, Mock.Of<ILogger<SymbolValidationMessageHandlerRouter>>());

            await Assert.ThrowsAsync<NotSupportedException>(() => router.HandleAsync(message));
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
                        .ReturnsAsync(new PackageValidationSet { ValidatingType = validatingType });
                    return PackageValidationMessageData.NewCheckValidator(validationId);
                case PackageValidationMessageType.FailValidationSet:
                    var trackingId = Guid.NewGuid();
                    storage.Setup(x => x.GetValidationSetAsync(trackingId))
                        .ReturnsAsync(new PackageValidationSet { ValidatingType = validatingType });
                    return PackageValidationMessageData.NewFailValidationSet(trackingId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(messageType));
            }
        }
    }
}

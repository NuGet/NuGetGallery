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
    public class PackageValidationMessageHandlerRouterFacts
    {
        [Theory]
        [InlineData(PackageValidationMessageType.ProcessValidationSet, ValidatingType.Package)]
        [InlineData(PackageValidationMessageType.ProcessValidationSet, ValidatingType.StagedPackage)]
        [InlineData(PackageValidationMessageType.CheckValidator, ValidatingType.Package)]
        [InlineData(PackageValidationMessageType.CheckValidator, ValidatingType.StagedPackage)]
        [InlineData(PackageValidationMessageType.FailValidationSet, ValidatingType.Package)]
        [InlineData(PackageValidationMessageType.FailValidationSet, ValidatingType.StagedPackage)]
        public async Task RoutesMessageToItsValidatingType(
            PackageValidationMessageType messageType,
            ValidatingType validatingType)
        {
            var packageHandler = new Mock<IValidationMessageHandler<Package>>();
            packageHandler
                .Setup(x => x.HandleAsync(It.IsAny<PackageValidationMessageData>()))
                .ReturnsAsync(true);
            var stagedPackageHandler = new Mock<IValidationMessageHandler<StagedPackage>>();
            stagedPackageHandler
                .Setup(x => x.HandleAsync(It.IsAny<PackageValidationMessageData>()))
                .ReturnsAsync(true);
            var routerStorage = new Mock<IValidationStorageService>();
            var message = CreateMessage(messageType, validatingType, routerStorage);
            var target = new PackageValidationMessageHandlerRouter(
                packageHandler.Object,
                stagedPackageHandler.Object,
                routerStorage.Object,
                Mock.Of<ILogger<PackageValidationMessageHandlerRouter>>());

            await target.HandleAsync(message);

            packageHandler.Verify(
                x => x.HandleAsync(message),
                validatingType == ValidatingType.Package ? Times.Once() : Times.Never());
            stagedPackageHandler.Verify(
                x => x.HandleAsync(message),
                validatingType == ValidatingType.StagedPackage ? Times.Once() : Times.Never());
        }

        private static PackageValidationMessageData CreateMessage(
            PackageValidationMessageType messageType,
            ValidatingType validatingType,
            Mock<IValidationStorageService> routerStorage)
        {
            switch (messageType)
            {
                case PackageValidationMessageType.ProcessValidationSet:
                    return PackageValidationMessageData.NewProcessValidationSet(
                        "PackageA",
                        "1.0.0",
                        Guid.NewGuid(),
                        validatingType,
                        entityKey: 43);
                case PackageValidationMessageType.CheckValidator:
                    var validationId = Guid.NewGuid();
                    routerStorage
                        .Setup(x => x.TryGetParentValidationSetAsync(validationId))
                        .ReturnsAsync(new PackageValidationSet { ValidatingType = validatingType });
                    return PackageValidationMessageData.NewCheckValidator(validationId);
                case PackageValidationMessageType.FailValidationSet:
                    var trackingId = Guid.NewGuid();
                    routerStorage
                        .Setup(x => x.GetValidationSetAsync(trackingId))
                        .ReturnsAsync(new PackageValidationSet { ValidatingType = validatingType });
                    return PackageValidationMessageData.NewFailValidationSet(trackingId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(messageType));
            }
        }

    }
}

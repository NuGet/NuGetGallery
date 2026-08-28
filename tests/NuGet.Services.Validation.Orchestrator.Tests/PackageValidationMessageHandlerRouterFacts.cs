// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Leases;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
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
            var package = new HandlerHarness<Package>();
            var stagedPackage = new HandlerHarness<StagedPackage>();
            var packageHandler = CreatePackageHandler(package);
            var stagedPackageHandler = CreateStagedPackageHandler(stagedPackage);
            var routerStorage = new Mock<IValidationStorageService>();
            var message = CreateMessage(messageType, validatingType, routerStorage);
            PrepareHandler(package, message);
            PrepareHandler(stagedPackage, message);
            var target = new PackageValidationMessageHandlerRouter(
                packageHandler,
                stagedPackageHandler,
                routerStorage.Object,
                Mock.Of<ILogger<PackageValidationMessageHandlerRouter>>());

            await target.HandleAsync(message);

            VerifyHandler(package, message, validatingType == ValidatingType.Package ? Times.Once() : Times.Never());
            VerifyHandler(stagedPackage, message, validatingType == ValidatingType.StagedPackage ? Times.Once() : Times.Never());
        }

        private static PackageValidationMessageHandler CreatePackageHandler(HandlerHarness<Package> harness)
        {
            return new PackageValidationMessageHandler(
                CreateOptions(),
                harness.EntityService.Object,
                harness.ValidationSetProvider.Object,
                Mock.Of<IValidationSetProcessor>(),
                Mock.Of<IValidationOutcomeProcessor<Package>>(),
                harness.ValidationStorageService.Object,
                Mock.Of<ILeaseService>(),
                Mock.Of<IPackageValidationEnqueuer>(),
                harness.FeatureFlagService.Object,
                Mock.Of<ITelemetryService>(),
                Mock.Of<ILogger<PackageValidationMessageHandler>>());
        }

        private static StagedPackageValidationMessageHandler CreateStagedPackageHandler(HandlerHarness<StagedPackage> harness)
        {
            return new StagedPackageValidationMessageHandler(
                CreateOptions(),
                harness.EntityService.Object,
                harness.ValidationSetProvider.Object,
                Mock.Of<IValidationSetProcessor>(),
                Mock.Of<IValidationOutcomeProcessor<StagedPackage>>(),
                harness.ValidationStorageService.Object,
                Mock.Of<ILeaseService>(),
                Mock.Of<IPackageValidationEnqueuer>(),
                harness.FeatureFlagService.Object,
                Mock.Of<ITelemetryService>(),
                Mock.Of<ILogger<StagedPackageValidationMessageHandler>>());
        }

        private static IOptionsSnapshot<ValidationConfiguration> CreateOptions()
        {
            var options = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            options
                .SetupGet(x => x.Value)
                .Returns(new ValidationConfiguration { MissingPackageRetryCount = 1 });
            return options.Object;
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

        private static void PrepareHandler<TEntity>(
            HandlerHarness<TEntity> harness,
            PackageValidationMessageData message)
            where TEntity : class, IEntity
        {
            switch (message.Type)
            {
                case PackageValidationMessageType.ProcessValidationSet:
                    harness.EntityService
                        .Setup(x => x.FindPackageByKey(message.ProcessValidationSet.EntityKey.Value))
                        .Returns((IValidatingEntity<TEntity>)null);
                    break;
                case PackageValidationMessageType.CheckValidator:
                    harness.FeatureFlagService
                        .Setup(x => x.IsQueueBackEnabled())
                        .Returns(true);
                    harness.ValidationSetProvider
                        .Setup(x => x.TryGetParentValidationSetAsync(message.CheckValidator.ValidationId))
                        .ReturnsAsync((PackageValidationSet)null);
                    break;
                case PackageValidationMessageType.FailValidationSet:
                    harness.ValidationStorageService
                        .Setup(x => x.GetValidationSetAsync(message.FailValidationSet.ValidationTrackingId))
                        .ReturnsAsync((PackageValidationSet)null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void VerifyHandler<TEntity>(
            HandlerHarness<TEntity> harness,
            PackageValidationMessageData message,
            Times times)
            where TEntity : class, IEntity
        {
            switch (message.Type)
            {
                case PackageValidationMessageType.ProcessValidationSet:
                    harness.EntityService.Verify(
                        x => x.FindPackageByKey(message.ProcessValidationSet.EntityKey.Value),
                        times);
                    break;
                case PackageValidationMessageType.CheckValidator:
                    harness.ValidationSetProvider.Verify(
                        x => x.TryGetParentValidationSetAsync(message.CheckValidator.ValidationId),
                        times);
                    break;
                case PackageValidationMessageType.FailValidationSet:
                    harness.ValidationStorageService.Verify(
                        x => x.GetValidationSetAsync(message.FailValidationSet.ValidationTrackingId),
                        times);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private class HandlerHarness<TEntity>
            where TEntity : class, IEntity
        {
            public Mock<IEntityService<TEntity>> EntityService { get; } = new Mock<IEntityService<TEntity>>();
            public Mock<IValidationSetProvider<TEntity>> ValidationSetProvider { get; } = new Mock<IValidationSetProvider<TEntity>>();
            public Mock<IValidationStorageService> ValidationStorageService { get; } = new Mock<IValidationStorageService>();
            public Mock<IFeatureFlagService> FeatureFlagService { get; } = new Mock<IFeatureFlagService>();
        }
    }
}

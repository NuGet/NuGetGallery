// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Leases;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationMessageHandlerStrictFacts : ValidationMessageHandlerFactsBase
    {
        public ValidationMessageHandlerStrictFacts(ITestOutputHelper output)
            : base(output, MockBehavior.Strict)
        {
        }

        [Fact]
        public async Task WaitsForValidationSetAvailabilityInValidationDBWithCheckValidator()
        {
            var messageData = PackageValidationMessageData.NewCheckValidator(Guid.NewGuid());
            var validationConfiguration = new ValidationConfiguration();

            ValidationSetProviderMock
                .Setup(ps => ps.TryGetParentValidationSetAsync(messageData.CheckValidator.ValidationId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            var handler = CreateHandler();

            var result = await handler.HandleAsync(messageData);

            ValidationSetProviderMock.Verify(
                ps => ps.TryGetParentValidationSetAsync(messageData.CheckValidator.ValidationId),
                Times.Once);

            Assert.False(result, "The handler should not have succeeded.");
        }

        [Theory]
        [InlineData(ValidatingType.SymbolPackage)]
        [InlineData(ValidatingType.Generic)]
        public async Task RejectsNonPackageValidationSetWithCheckValidator(ValidatingType validatingType)
        {
            var messageData = PackageValidationMessageData.NewCheckValidator(Guid.NewGuid());
            var validationConfiguration = new ValidationConfiguration();
            var validationSet = new PackageValidationSet { PackageKey = 42, ValidatingType = validatingType };

            ValidationSetProviderMock
                .Setup(ps => ps.TryGetParentValidationSetAsync(messageData.CheckValidator.ValidationId))
                .ReturnsAsync(validationSet)
                .Verifiable();

            var handler = CreateHandler();

            var result = await handler.HandleAsync(messageData);

            ValidationSetProviderMock.Verify(
                ps => ps.TryGetParentValidationSetAsync(messageData.CheckValidator.ValidationId),
                Times.Once);

            Assert.False(result, "The handler should not have succeeded.");
        }

        [Fact]
        public async Task DoesNotWaitForPackageAvailabilityInGalleryDBWithCheckValidator()
        {
            var messageData = PackageValidationMessageData.NewCheckValidator(Guid.NewGuid());
            var validationConfiguration = new ValidationConfiguration();
            var validationSet = new PackageValidationSet { PackageKey = 42, ValidatingType = ValidatingType.Package };

            ValidationSetProviderMock
                .Setup(ps => ps.TryGetParentValidationSetAsync(messageData.CheckValidator.ValidationId))
                .ReturnsAsync(validationSet)
                .Verifiable();
            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByKey(validationSet.PackageKey.Value))
                .Returns<SymbolPackage>(null)
                .Verifiable();

            var handler = CreateHandler();

            var result = await handler.HandleAsync(messageData);

            ValidationSetProviderMock.Verify(
                ps => ps.TryGetParentValidationSetAsync(messageData.CheckValidator.ValidationId),
                Times.Once);
            CorePackageServiceMock.Verify(
                ps => ps.FindPackageByKey(validationSet.PackageKey.Value),
                Times.Once);

            Assert.True(result, "The handler should have succeeded.");
        }

        [Fact]
        public async Task WaitsForPackageAvailabilityInGalleryDBWithProcessValidationSet()
        {
            var messageData = PackageValidationMessageData.NewProcessValidationSet(
                "packageId",
                "1.2.3",
                Guid.NewGuid(),
                ValidatingType.Package,
                entityKey: null);
            var validationConfiguration = new ValidationConfiguration();

            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(
                    messageData.ProcessValidationSet.PackageId,
                    messageData.ProcessValidationSet.PackageVersion))
                .Returns<Package>(null)
                .Verifiable();

            var handler = CreateHandler();

            await handler.HandleAsync(messageData);

            CorePackageServiceMock.Verify(
                ps => ps.FindPackageByIdAndVersionStrict(
                    messageData.ProcessValidationSet.PackageId,
                    messageData.ProcessValidationSet.PackageVersion),
                Times.Once);
        }

        [Fact]
        public async Task DropsMessageAfterMissingPackageRetryCountIsReached()
        {
            var validationTrackingId = Guid.NewGuid();
            var messageData = PackageValidationMessageData.NewProcessValidationSet(
                "packageId",
                "1.2.3",
                validationTrackingId,
                ValidatingType.Package,
                entityKey: null);

            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict("packageId", "1.2.3"))
                .Returns<Package>(null)
                .Verifiable();

            TelemetryServiceMock
                .Setup(t => t.TrackMissingPackageForValidationMessage("packageId", "1.2.3", validationTrackingId.ToString()))
                .Verifiable();

            var handler = CreateHandler();

            Assert.False(await handler.HandleAsync(OverrideDeliveryCount(messageData, deliveryCount: 1)));
            Assert.False(await handler.HandleAsync(OverrideDeliveryCount(messageData, deliveryCount: 2)));
            Assert.True(await handler.HandleAsync(OverrideDeliveryCount(messageData, deliveryCount: 3)));

            CorePackageServiceMock.Verify(ps => ps.FindPackageByIdAndVersionStrict("packageId", "1.2.3"), Times.Exactly(3));
            TelemetryServiceMock.Verify(t => t.TrackMissingPackageForValidationMessage("packageId", "1.2.3", validationTrackingId.ToString()), Times.Once);
        }

        private PackageValidationMessageData OverrideDeliveryCount(
            PackageValidationMessageData messageData,
            int deliveryCount)
        {
            var serializer = new ServiceBusMessageSerializer();

            var realBrokeredMessage = serializer.SerializePackageValidationMessageData(messageData);

            var fakedBrokeredMessage = new MessageWithCustomDeliveryCount(realBrokeredMessage, deliveryCount);

            return serializer.DeserializePackageValidationMessageData(fakedBrokeredMessage);
        }

        [Fact]
        public async Task DropsMessageOnDuplicateValidationRequest()
        {
            var messageData = PackageValidationMessageData.NewProcessValidationSet(
                "packageId",
                "1.2.3",
                Guid.NewGuid(),
                ValidatingType.Package,
                entityKey: null);
            var validationConfiguration = new ValidationConfiguration();
            var package = new Package();
            var packageValidatingEntity = new PackageValidatingEntity(package);

            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(
                    messageData.ProcessValidationSet.PackageId,
                    messageData.ProcessValidationSet.PackageVersion))
                .Returns(packageValidatingEntity)
                .Verifiable();

            ValidationSetProviderMock
                .Setup(vsp => vsp.TryGetOrCreateValidationSetAsync(
                    messageData.ProcessValidationSet,
                    packageValidatingEntity))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            var handler = CreateHandler();

            var result = await handler.HandleAsync(messageData);

            Assert.True(result);
            CorePackageServiceMock.Verify(
                ps => ps.FindPackageByIdAndVersionStrict(
                    messageData.ProcessValidationSet.PackageId,
                    messageData.ProcessValidationSet.PackageVersion),
                Times.Once);
            ValidationSetProviderMock.Verify(
                vsp => vsp.TryGetOrCreateValidationSetAsync(
                    messageData.ProcessValidationSet,
                    packageValidatingEntity),
                Times.Once);
        }

        [Fact]
        public async Task DropsMessageIfPackageIsSoftDeletedForProcessValidationSet()
        {
            var messageData = PackageValidationMessageData.NewProcessValidationSet(
                "packageId",
                "1.2.3",
                Guid.NewGuid(),
                ValidatingType.Package,
                entityKey: null);
            var validationConfiguration = new ValidationConfiguration();
            var package = new Package { PackageStatusKey = PackageStatus.Deleted };
            var packageValidatingEntity = new PackageValidatingEntity(package);

            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(
                    messageData.ProcessValidationSet.PackageId,
                    messageData.ProcessValidationSet.PackageVersion))
                .Returns(packageValidatingEntity);

            var handler = CreateHandler();

            var result = await handler.HandleAsync(messageData);

            Assert.True(result);
            CorePackageServiceMock.Verify(
                ps => ps.FindPackageByIdAndVersionStrict(
                    messageData.ProcessValidationSet.PackageId,
                    messageData.ProcessValidationSet.PackageVersion),
                Times.Once);
        }

        [Fact]
        public async Task DropsMessageIfPackageIsSoftDeletedForCheckValidator()
        {
            var messageData = PackageValidationMessageData.NewCheckValidator(Guid.NewGuid());
            var validationConfiguration = new ValidationConfiguration();
            var package = new Package { Key = 42, PackageStatusKey = PackageStatus.Deleted };
            var packageValidatingEntity = new PackageValidatingEntity(package);
            var validationSet = new PackageValidationSet { PackageKey = package.Key, ValidatingType = ValidatingType.Package };

            ValidationSetProviderMock
                .Setup(ps => ps.TryGetParentValidationSetAsync(messageData.CheckValidator.ValidationId))
                .ReturnsAsync(validationSet)
                .Verifiable();
            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByKey(package.Key))
                .Returns(packageValidatingEntity);

            var handler = CreateHandler();

            var result = await handler.HandleAsync(messageData);

            Assert.True(result);
            ValidationSetProviderMock.Verify(
                vsp => vsp.TryGetParentValidationSetAsync(messageData.CheckValidator.ValidationId),
                Times.Once);
            CorePackageServiceMock.Verify(
                ps => ps.FindPackageByKey(package.Key),
                Times.Once);
        }

        private class MessageWithCustomDeliveryCount : IReceivedBrokeredMessage
        {
            private readonly IBrokeredMessage _inner;

            public MessageWithCustomDeliveryCount(IBrokeredMessage inner, int deliveryCount)
            {
                _inner = inner;
                DeliveryCount = deliveryCount;
            }

            public int DeliveryCount { get; }
            public DateTimeOffset ExpiresAtUtc => throw new NotImplementedException();
            public TimeSpan TimeToLive => throw new NotImplementedException();
            public IReadOnlyDictionary<string, object> Properties => _inner.Properties.ToDictionary(x => x.Key, x => x.Value);
            public DateTimeOffset EnqueuedTimeUtc => throw new NotImplementedException();
            public DateTimeOffset ScheduledEnqueueTimeUtc => throw new NotImplementedException();
            public string MessageId => throw new NotImplementedException();

            public Task AbandonAsync() => throw new NotImplementedException();
            public Task CompleteAsync() => throw new NotImplementedException();
            public string GetBody() => _inner.GetBody();
            public TStream GetBody<TStream>() => _inner.GetBody<TStream>();

            public Stream GetRawBody()
            {
                throw new NotImplementedException();
            }
        }
    }

    public class ValidationMessageHandlerLooseFacts : ValidationMessageHandlerFactsBase
    {
        protected Package Package { get; }
        protected PackageValidationMessageData ProcessValidationSetData { get; }
        protected PackageValidationMessageData CheckValidatorData { get; }
        protected PackageValidationSet ValidationSet { get; }
        protected PackageValidatingEntity PackageValidatingEntity { get; }

        public ValidationMessageHandlerLooseFacts(ITestOutputHelper output)
            : base(output, MockBehavior.Loose)
        {
            Package = new Package { Key = 42 };
            ProcessValidationSetData = PackageValidationMessageData.NewProcessValidationSet(
                "packageId",
                "1.2.3",
                Guid.NewGuid(),
                ValidatingType.Package,
                entityKey: null);
            CheckValidatorData = PackageValidationMessageData.NewCheckValidator(Guid.NewGuid());
            ValidationSet = new PackageValidationSet { PackageKey = Package.Key, ValidatingType = ValidatingType.Package };
            PackageValidatingEntity = new PackageValidatingEntity(Package);

            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(
                    ProcessValidationSetData.ProcessValidationSet.PackageId,
                    ProcessValidationSetData.ProcessValidationSet.PackageVersion))
                .Returns(PackageValidatingEntity);
            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByKey(Package.Key))
                .Returns(PackageValidatingEntity);

            ValidationSetProviderMock
                .Setup(vsp => vsp.TryGetOrCreateValidationSetAsync(
                    ProcessValidationSetData.ProcessValidationSet,
                    PackageValidatingEntity))
                .ReturnsAsync(ValidationSet);
            ValidationSetProviderMock
                .Setup(vsp => vsp.TryGetParentValidationSetAsync(CheckValidatorData.CheckValidator.ValidationId))
                .ReturnsAsync(ValidationSet);
        }

        [Fact]
        public async Task MakesSureValidationSetExists()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(ProcessValidationSetData);

            ValidationSetProviderMock.Verify(vsp => vsp.TryGetOrCreateValidationSetAsync(
                ProcessValidationSetData.ProcessValidationSet,
                PackageValidatingEntity));
        }

        [Fact]
        public async Task CallsProcessValidationsForProcessValidationSet()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(ProcessValidationSetData);

            ValidationSetProcessorMock
                .Verify(vsp => vsp.ProcessValidationsAsync(ValidationSet), Times.Once());
        }

        [Fact]
        public async Task CallsProcessValidationOutcomeForProcessValidationSet()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(ProcessValidationSetData);

            ValidationOutcomeProcessorMock.Verify(
                vop => vop.ProcessValidationOutcomeAsync(
                    ValidationSet,
                    PackageValidatingEntity,
                    It.IsAny<ValidationSetProcessorResult>(),
                    true));
        }

        [Fact]
        public async Task SkipsCompletedValidationSets()
        {
            ValidationSet.ValidationSetStatus = ValidationSetStatus.Completed;

            var handler = CreateHandler();
            var output = await handler.HandleAsync(ProcessValidationSetData);

            Assert.True(output, "The message should have been successfully processed.");

            ValidationSetProcessorMock.Verify(
                vop => vop.ProcessValidationsAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
            ValidationOutcomeProcessorMock.Verify(
                vop => vop.ProcessValidationOutcomeAsync(
                    It.IsAny<PackageValidationSet>(),
                    It.IsAny<IValidatingEntity<Package>>(),
                    It.IsAny<ValidationSetProcessorResult>(),
                    It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task CallsProcessValidationsForCheckValidator()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(CheckValidatorData);

            ValidationSetProcessorMock
                .Verify(vsp => vsp.ProcessValidationsAsync(ValidationSet), Times.Once());
        }

        [Fact]
        public async Task IgnoresCheckValidatorWhenFeatureFlagIsOff()
        {
            FeatureFlagService.Setup(x => x.IsQueueBackEnabled()).Returns(false);

            var handler = CreateHandler();
            await handler.HandleAsync(CheckValidatorData);

            ValidationSetProcessorMock
                .Verify(vsp => vsp.ProcessValidationsAsync(ValidationSet), Times.Never());
        }

        [Fact]
        public async Task CallsProcessValidationOutcomeForCheckValidator()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(CheckValidatorData);

            ValidationOutcomeProcessorMock.Verify(
                vop => vop.ProcessValidationOutcomeAsync(
                    ValidationSet,
                    PackageValidatingEntity,
                    It.IsAny<ValidationSetProcessorResult>(),
                    false));
        }
    }

    public class ValidationMessageHandlerFactsBase
    {
        protected static readonly ValidationConfiguration Configuration = new ValidationConfiguration
        {
            MissingPackageRetryCount = 2,
        };

        protected Mock<IOptionsSnapshot<ValidationConfiguration>> ConfigurationAccessorMock { get; }
        protected Mock<IEntityService<Package>> CorePackageServiceMock { get; }
        protected Mock<IValidationSetProvider<Package>> ValidationSetProviderMock { get; }
        protected Mock<IValidationSetProcessor> ValidationSetProcessorMock { get; }
        protected Mock<IValidationOutcomeProcessor<Package>> ValidationOutcomeProcessorMock { get; }
        protected Mock<ILeaseService> LeaseService { get; }
        protected Mock<IPackageValidationEnqueuer> ValidationEnqueuer { get; }
        protected Mock<IFeatureFlagService> FeatureFlagService { get; }
        protected Mock<ITelemetryService> TelemetryServiceMock { get; }
        protected ILogger<PackageValidationMessageHandler> Logger { get; }

        public ValidationMessageHandlerFactsBase(
            ITestOutputHelper output,
            MockBehavior mockBehavior)
        {
            ConfigurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            CorePackageServiceMock = new Mock<IEntityService<Package>>(mockBehavior);
            ValidationSetProviderMock = new Mock<IValidationSetProvider<Package>>(mockBehavior);
            ValidationSetProcessorMock = new Mock<IValidationSetProcessor>(mockBehavior);
            ValidationOutcomeProcessorMock = new Mock<IValidationOutcomeProcessor<Package>>(mockBehavior);
            LeaseService = new Mock<ILeaseService>(mockBehavior);
            ValidationEnqueuer = new Mock<IPackageValidationEnqueuer>(mockBehavior);
            FeatureFlagService = new Mock<IFeatureFlagService>(mockBehavior);
            TelemetryServiceMock = new Mock<ITelemetryService>(mockBehavior);

            FeatureFlagService.Setup(x => x.IsQueueBackEnabled()).Returns(true);
            FeatureFlagService.Setup(x => x.IsOrchestratorLeaseEnabled()).Returns(false);

            // we generally don't care about how logger is called, so don't make a strict mock.
            Logger = new LoggerFactory().AddXunit(output).CreateLogger<PackageValidationMessageHandler>();

            ConfigurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(Configuration);
        }

        public PackageValidationMessageHandler CreateHandler()
        {
            return new PackageValidationMessageHandler(
                ConfigurationAccessorMock.Object,
                CorePackageServiceMock.Object,
                ValidationSetProviderMock.Object,
                ValidationSetProcessorMock.Object,
                ValidationOutcomeProcessorMock.Object,
                LeaseService.Object,
                ValidationEnqueuer.Object,
                FeatureFlagService.Object,
                TelemetryServiceMock.Object,
                Logger);
        }
    }
}

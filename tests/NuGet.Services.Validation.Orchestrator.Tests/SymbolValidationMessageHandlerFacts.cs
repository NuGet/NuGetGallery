// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
    public class SymbolValidationMessageHandlerStrictFacts : SymbolValidationMessageHandlerFactsBase
    {
        public SymbolValidationMessageHandlerStrictFacts(ITestOutputHelper output)
            : base(output, MockBehavior.Strict) { }

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

        [Fact]
        public async Task RejectsNonSymbolPackageValidationSetWithCheckValidator()
        {
            var messageData = PackageValidationMessageData.NewCheckValidator(Guid.NewGuid());
            var validationConfiguration = new ValidationConfiguration();
            var validationSet = new PackageValidationSet { PackageKey = 42, ValidatingType = ValidatingType.Package };

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
        public async Task WaitsForPackageAvailabilityInGalleryDBWithCheckValidator()
        {
            var messageData = PackageValidationMessageData.NewCheckValidator(Guid.NewGuid());
            var validationConfiguration = new ValidationConfiguration();
            var validationSet = new PackageValidationSet { PackageKey = 42, ValidatingType = ValidatingType.SymbolPackage };

            ValidationSetProviderMock
                .Setup(ps => ps.TryGetParentValidationSetAsync(messageData.CheckValidator.ValidationId))
                .ReturnsAsync(validationSet)
                .Verifiable();
            CoreSymbolPackageServiceMock
                .Setup(ps => ps.FindPackageByKey(validationSet.PackageKey))
                .Returns<SymbolPackage>(null)
                .Verifiable();

            var handler = CreateHandler();

            var result = await handler.HandleAsync(messageData);

            ValidationSetProviderMock.Verify(
                ps => ps.TryGetParentValidationSetAsync(messageData.CheckValidator.ValidationId),
                Times.Once);
            CoreSymbolPackageServiceMock.Verify(
                ps => ps.FindPackageByKey(validationSet.PackageKey),
                Times.Once);

            Assert.False(result, "The handler should not have succeeded.");
        }

        [Fact]
        public async Task WaitsForPackageAvailabilityInGalleryDBWithProcessValidationSet()
        {
            var messageData = PackageValidationMessageData.NewProcessValidationSet(
                "packageId",
                "1.2.3",
                Guid.NewGuid(),
                ValidatingType.SymbolPackage,
                entityKey: null);
            var validationConfiguration = new ValidationConfiguration();

            CoreSymbolPackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(messageData.ProcessValidationSet.PackageId, messageData.ProcessValidationSet.PackageVersion))
                .Returns<SymbolPackage>(null)
                .Verifiable();

            var handler = CreateHandler();

            await handler.HandleAsync(messageData);

            CoreSymbolPackageServiceMock.Verify(
                ps => ps.FindPackageByIdAndVersionStrict(messageData.ProcessValidationSet.PackageId, messageData.ProcessValidationSet.PackageVersion),
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
                ValidatingType.SymbolPackage,
                entityKey: null);

            CoreSymbolPackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict("packageId", "1.2.3"))
                .Returns<SymbolPackage>(null)
                .Verifiable();

            TelemetryServiceMock
                .Setup(t => t.TrackMissingPackageForValidationMessage("packageId", "1.2.3", validationTrackingId.ToString()))
                .Verifiable();

            var handler = CreateHandler();

            Assert.False(await handler.HandleAsync(OverrideDeliveryCount(messageData, deliveryCount: 1)));
            Assert.False(await handler.HandleAsync(OverrideDeliveryCount(messageData, deliveryCount: 2)));
            Assert.True(await handler.HandleAsync(OverrideDeliveryCount(messageData, deliveryCount: 3)));

            CoreSymbolPackageServiceMock.Verify(ps => ps.FindPackageByIdAndVersionStrict("packageId", "1.2.3"), Times.Exactly(3));
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
                ValidatingType.SymbolPackage,
                entityKey: null);
            var validationConfiguration = new ValidationConfiguration();
            var symbolPackage = new SymbolPackage() { Package = new Package() };
            var symbolPackageValidatingEntity = new SymbolPackageValidatingEntity(symbolPackage);

            CoreSymbolPackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(
                    messageData.ProcessValidationSet.PackageId,
                    messageData.ProcessValidationSet.PackageVersion))
                .Returns(symbolPackageValidatingEntity)
                .Verifiable();

            ValidationSetProviderMock
                .Setup(vsp => vsp.TryGetOrCreateValidationSetAsync(messageData.ProcessValidationSet, symbolPackageValidatingEntity))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            var handler = CreateHandler();

            var result = await handler.HandleAsync(messageData);

            Assert.True(result);
            CoreSymbolPackageServiceMock.Verify(
                ps => ps.FindPackageByIdAndVersionStrict(
                    messageData.ProcessValidationSet.PackageId,
                    messageData.ProcessValidationSet.PackageVersion),
                Times.Once);
            ValidationSetProviderMock.Verify(
                vsp => vsp.TryGetOrCreateValidationSetAsync(
                    messageData.ProcessValidationSet,
                    symbolPackageValidatingEntity),
                Times.Once);
        }

        private class MessageWithCustomDeliveryCount : IBrokeredMessage
        {
            private readonly IBrokeredMessage _inner;

            public MessageWithCustomDeliveryCount(IBrokeredMessage inner, int deliveryCount)
            {
                _inner = inner;
                DeliveryCount = deliveryCount;
            }

            public int DeliveryCount { get; private set; }

            public string GetBody() => _inner.GetBody();
            public IDictionary<string, object> Properties => _inner.Properties;

            public DateTimeOffset ScheduledEnqueueTimeUtc { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public DateTimeOffset ExpiresAtUtc => throw new NotImplementedException();
            public DateTimeOffset EnqueuedTimeUtc => throw new NotImplementedException();
            public TimeSpan TimeToLive { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public string MessageId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public Task AbandonAsync() => throw new NotImplementedException();
            public IBrokeredMessage Clone() => throw new NotImplementedException();
            public Task CompleteAsync() => throw new NotImplementedException();
            public void Dispose() => throw new NotImplementedException();
        }
    }

    public class SymbolValidationMessageHandlerLooseFacts : SymbolValidationMessageHandlerFactsBase
    {
        protected SymbolPackage SymbolPackage { get; }
        protected PackageValidationMessageData ProcessValidationSetData { get; }
        protected PackageValidationMessageData CheckValidatorData { get; }
        protected PackageValidationSet ValidationSet { get; }
        protected SymbolPackageValidatingEntity SymbolPackageValidatingEntity { get; }

        public SymbolValidationMessageHandlerLooseFacts(ITestOutputHelper output)
            : base(output, MockBehavior.Loose)
        {
            SymbolPackage = new SymbolPackage() { Package = new Package(), Key = 42 };
            ProcessValidationSetData = PackageValidationMessageData.NewProcessValidationSet(
                "packageId",
                "1.2.3",
                Guid.NewGuid(),
                ValidatingType.SymbolPackage,
                entityKey: null);
            CheckValidatorData = PackageValidationMessageData.NewCheckValidator(Guid.NewGuid());
            ValidationSet = new PackageValidationSet { ValidatingType = ValidatingType.SymbolPackage, PackageKey = SymbolPackage.Key };
            SymbolPackageValidatingEntity = new SymbolPackageValidatingEntity(SymbolPackage);

            CoreSymbolPackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(
                    ProcessValidationSetData.ProcessValidationSet.PackageId,
                    ProcessValidationSetData.ProcessValidationSet.PackageVersion))
                .Returns(SymbolPackageValidatingEntity);
            CoreSymbolPackageServiceMock
                .Setup(ps => ps.FindPackageByKey(SymbolPackage.Key))
                .Returns(SymbolPackageValidatingEntity);

            ValidationSetProviderMock
                .Setup(vsp => vsp.TryGetOrCreateValidationSetAsync(
                    ProcessValidationSetData.ProcessValidationSet,
                    SymbolPackageValidatingEntity))
                .ReturnsAsync(ValidationSet);
            ValidationSetProviderMock
                .Setup(vsp => vsp.TryGetParentValidationSetAsync(CheckValidatorData.CheckValidator.ValidationId))
                .ReturnsAsync(ValidationSet);
        }

        [Fact]
        public async Task MakesSureValidationSetExistsForProcessValidationSet()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(ProcessValidationSetData);

            ValidationSetProviderMock.Verify(
                vsp => vsp.TryGetOrCreateValidationSetAsync(
                    ProcessValidationSetData.ProcessValidationSet,
                    SymbolPackageValidatingEntity));
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

            ValidationOutcomeProcessorMock
                .Verify(vop => vop.ProcessValidationOutcomeAsync(
                    ValidationSet,
                    SymbolPackageValidatingEntity,
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
                    It.IsAny<IValidatingEntity<SymbolPackage>>(),
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
        public async Task CallsProcessValidationOutcomeForCheckValidator()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(CheckValidatorData);

            ValidationOutcomeProcessorMock
                .Verify(vop => vop.ProcessValidationOutcomeAsync(
                    ValidationSet,
                    SymbolPackageValidatingEntity,
                    It.IsAny<ValidationSetProcessorResult>(),
                    false));
        }
    }

    public class SymbolValidationMessageHandlerFactsBase
    {
        protected static readonly ValidationConfiguration Configuration = new ValidationConfiguration
        {
            MissingPackageRetryCount = 2,
        };

        protected Mock<IOptionsSnapshot<ValidationConfiguration>> ConfigurationAccessorMock { get; }
        protected Mock<IEntityService<SymbolPackage>> CoreSymbolPackageServiceMock { get; }
        protected Mock<IValidationSetProvider<SymbolPackage>> ValidationSetProviderMock { get; }
        protected Mock<IValidationSetProcessor> ValidationSetProcessorMock { get; }
        protected Mock<IValidationOutcomeProcessor<SymbolPackage>> ValidationOutcomeProcessorMock { get; }
        protected Mock<ILeaseService> LeaseService { get; }
        protected Mock<IPackageValidationEnqueuer> ValidationEnqueuer { get; }
        protected Mock<IFeatureFlagService> FeatureFlagService { get; }
        protected Mock<ITelemetryService> TelemetryServiceMock { get; }
        protected ILogger<SymbolValidationMessageHandler> Logger { get; }

        public SymbolValidationMessageHandlerFactsBase(ITestOutputHelper output, MockBehavior mockBehavior)
        {
            ConfigurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            CoreSymbolPackageServiceMock = new Mock<IEntityService<SymbolPackage>>(mockBehavior);
            ValidationSetProviderMock = new Mock<IValidationSetProvider<SymbolPackage>>(mockBehavior);
            ValidationSetProcessorMock = new Mock<IValidationSetProcessor>(mockBehavior);
            ValidationOutcomeProcessorMock = new Mock<IValidationOutcomeProcessor<SymbolPackage>>(mockBehavior);
            LeaseService = new Mock<ILeaseService>(mockBehavior);
            ValidationEnqueuer = new Mock<IPackageValidationEnqueuer>(mockBehavior);
            FeatureFlagService = new Mock<IFeatureFlagService>(mockBehavior);
            TelemetryServiceMock = new Mock<ITelemetryService>(mockBehavior);

            FeatureFlagService.Setup(x => x.IsQueueBackEnabled()).Returns(true);
            FeatureFlagService.Setup(x => x.IsOrchestratorLeaseEnabled()).Returns(false);

            // we generally don't care about how logger is called, so don't make a strict mock.
            Logger = new LoggerFactory().AddXunit(output).CreateLogger<SymbolValidationMessageHandler>();

            ConfigurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(Configuration);
        }

        public SymbolValidationMessageHandler CreateHandler()
        {
            return new SymbolValidationMessageHandler(
                ConfigurationAccessorMock.Object,
                CoreSymbolPackageServiceMock.Object,
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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class SymbolValidationMessageHandlerStrictFacts : SymbolValidationMessageHandlerFactsBase
    {
        public SymbolValidationMessageHandlerStrictFacts()
            : base(MockBehavior.Strict) { }

        [Fact]
        public async Task WaitsForPackageAvailabilityInGalleryDB()
        {
            var messageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid(), ValidatingType.SymbolPackage);
            var validationConfiguration = new ValidationConfiguration();

            CoreSymbolPackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion))
                .Returns<SymbolPackage>(null)
                .Verifiable();

            var handler = CreateHandler();

            await handler.HandleAsync(messageData);

            CoreSymbolPackageServiceMock.Verify(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion), Times.Once());
        }

        [Fact]
        public async Task DropsMessageAfterMissingPackageRetryCountIsReached()
        {
            var validationTrackingId = Guid.NewGuid();
            var messageData = new PackageValidationMessageData("packageId", "1.2.3", validationTrackingId, ValidatingType.SymbolPackage);

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
            var messageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid(), ValidatingType.SymbolPackage);
            var validationConfiguration = new ValidationConfiguration();
            var symbolPackage = new SymbolPackage() { Package = new Package() };
            var symbolPackageValidatingEntity = new SymbolPackageValidatingEntity(symbolPackage);

            CoreSymbolPackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion))
                .Returns(symbolPackageValidatingEntity)
                .Verifiable();

            ValidationSetProviderMock
                .Setup(vsp => vsp.TryGetOrCreateValidationSetAsync(messageData, symbolPackageValidatingEntity))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            var handler = CreateHandler();

            var result = await handler.HandleAsync(messageData);

            Assert.True(result);
            CoreSymbolPackageServiceMock
                .Verify(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion), Times.Once());
            ValidationSetProviderMock
                .Verify(vsp => vsp.TryGetOrCreateValidationSetAsync(messageData, symbolPackageValidatingEntity), Times.Once());
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
        protected PackageValidationMessageData MessageData { get; }
        protected PackageValidationSet ValidationSet { get; }
        protected SymbolPackageValidatingEntity SymbolPackageValidatingEntity { get; }

        public SymbolValidationMessageHandlerLooseFacts()
            : base(MockBehavior.Loose)
        {
            SymbolPackage = new SymbolPackage() { Package = new Package() };
            MessageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid(), ValidatingType.SymbolPackage);
            ValidationSet = new PackageValidationSet();
            SymbolPackageValidatingEntity = new SymbolPackageValidatingEntity(SymbolPackage);

            CoreSymbolPackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(MessageData.PackageId, MessageData.PackageVersion))
                .Returns(SymbolPackageValidatingEntity);

            ValidationSetProviderMock
                .Setup(vsp => vsp.TryGetOrCreateValidationSetAsync(MessageData, SymbolPackageValidatingEntity))
                .ReturnsAsync(ValidationSet);
        }

        [Fact]
        public async Task MakesSureValidationSetExists()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(MessageData);

            ValidationSetProviderMock
                .Verify(vsp => vsp.TryGetOrCreateValidationSetAsync(MessageData, SymbolPackageValidatingEntity));
        }

        [Fact]
        public async Task SkipsCompletedValidationSets()
        {
            ValidationSet.ValidationSetStatus = ValidationSetStatus.Completed;

            var handler = CreateHandler();
            var output = await handler.HandleAsync(MessageData);

            Assert.True(output, "The message should have been successfully processed.");

            ValidationSetProcessorMock.Verify(
                vop => vop.ProcessValidationsAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
            ValidationOutcomeProcessorMock.Verify(
                vop => vop.ProcessValidationOutcomeAsync(
                    It.IsAny<PackageValidationSet>(),
                    It.IsAny<IValidatingEntity<SymbolPackage>>(),
                    It.IsAny<ValidationSetProcessorResult>()),
                Times.Never);
        }

        [Fact]
        public async Task CallsProcessValidations()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(MessageData);

            ValidationSetProcessorMock
                .Verify(vsp => vsp.ProcessValidationsAsync(ValidationSet), Times.Once());
        }

        [Fact]
        public async Task CallsProcessValidationOutcome()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(MessageData);

            ValidationOutcomeProcessorMock
                .Verify(vop => vop.ProcessValidationOutcomeAsync(ValidationSet, SymbolPackageValidatingEntity, It.IsAny<ValidationSetProcessorResult>()));
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
        protected Mock<ITelemetryService> TelemetryServiceMock { get; }
        protected Mock<ILogger<SymbolValidationMessageHandler>> LoggerMock { get; }

        public SymbolValidationMessageHandlerFactsBase(MockBehavior mockBehavior)
        {
            ConfigurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            CoreSymbolPackageServiceMock = new Mock<IEntityService<SymbolPackage>>(mockBehavior);
            ValidationSetProviderMock = new Mock<IValidationSetProvider<SymbolPackage>>(mockBehavior);
            ValidationSetProcessorMock = new Mock<IValidationSetProcessor>(mockBehavior);
            ValidationOutcomeProcessorMock = new Mock<IValidationOutcomeProcessor<SymbolPackage>>(mockBehavior);
            TelemetryServiceMock = new Mock<ITelemetryService>(mockBehavior);
            LoggerMock = new Mock<ILogger<SymbolValidationMessageHandler>>(); // we generally don't care about how logger is called, so it's loose all the time

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
                TelemetryServiceMock.Object,
                LoggerMock.Object);
        }
    }
}

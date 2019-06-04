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
    public class ValidationMessageHandlerStrictFacts : ValidationMessageHandlerFactsBase
    {
        public ValidationMessageHandlerStrictFacts()
            : base(MockBehavior.Strict)
        {
        }

        [Fact]
        public async Task WaitsForPackageAvailabilityInGalleryDB()
        {
            var messageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid());
            var validationConfiguration = new ValidationConfiguration();

            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion))
                .Returns<Package>(null)
                .Verifiable();

            var handler = CreateHandler();

            await handler.HandleAsync(messageData);

            CorePackageServiceMock.Verify(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion), Times.Once());
        }

        [Fact]
        public async Task DropsMessageAfterMissingPackageRetryCountIsReached()
        {
            var validationTrackingId = Guid.NewGuid();
            var messageData = new PackageValidationMessageData("packageId", "1.2.3", validationTrackingId);

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
            var messageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid());
            var validationConfiguration = new ValidationConfiguration();
            var package = new Package();
            var packageValidatingEntity = new PackageValidatingEntity(package);

            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion))
                .Returns(packageValidatingEntity)
                .Verifiable();

            ValidationSetProviderMock
                .Setup(vsp => vsp.TryGetOrCreateValidationSetAsync(messageData, packageValidatingEntity))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            var handler = CreateHandler();

            var result = await handler.HandleAsync(messageData);

            Assert.True(result);
            CorePackageServiceMock
                .Verify(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion), Times.Once());
            ValidationSetProviderMock
                .Verify(vsp => vsp.TryGetOrCreateValidationSetAsync(messageData, packageValidatingEntity), Times.Once());
        }

        [Fact]
        public async Task DropsMessageIfPackageIsSoftDeleted()
        {
            var messageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid());
            var validationConfiguration = new ValidationConfiguration();
            var package = new Package { PackageStatusKey = PackageStatus.Deleted };
            var packageValidatingEntity = new PackageValidatingEntity(package);

            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion))
                .Returns(packageValidatingEntity);

            var handler = CreateHandler();

            var result = await handler.HandleAsync(messageData);

            Assert.True(result);
            CorePackageServiceMock
                .Verify(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion), Times.Once);
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

    public class ValidationMessageHandlerLooseFacts : ValidationMessageHandlerFactsBase
    {
        protected Package Package { get; }
        protected PackageValidationMessageData MessageData { get; }
        protected PackageValidationSet ValidationSet { get; }
        protected PackageValidatingEntity PackageValidatingEntity { get; }

        public ValidationMessageHandlerLooseFacts()
            : base(MockBehavior.Loose)
        {
            Package = new Package();
            MessageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid());
            ValidationSet = new PackageValidationSet();
            PackageValidatingEntity = new PackageValidatingEntity(Package);

            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(MessageData.PackageId, MessageData.PackageVersion))
                .Returns(PackageValidatingEntity);

            ValidationSetProviderMock
                .Setup(vsp => vsp.TryGetOrCreateValidationSetAsync(MessageData, PackageValidatingEntity))
                .ReturnsAsync(ValidationSet);
        }

        [Fact]
        public async Task MakesSureValidationSetExists()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(MessageData);

            ValidationSetProviderMock
                .Verify(vsp => vsp.TryGetOrCreateValidationSetAsync(MessageData, PackageValidatingEntity));
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
                    It.IsAny<IValidatingEntity<Package>>(),
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
                .Verify(vop => vop.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, It.IsAny<ValidationSetProcessorResult>()));
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
        protected Mock<ITelemetryService> TelemetryServiceMock { get; }
        protected Mock<ILogger<PackageValidationMessageHandler>> LoggerMock { get; }

        public ValidationMessageHandlerFactsBase(MockBehavior mockBehavior)
        {
            ConfigurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            CorePackageServiceMock = new Mock<IEntityService<Package>>(mockBehavior);
            ValidationSetProviderMock = new Mock<IValidationSetProvider<Package>>(mockBehavior);
            ValidationSetProcessorMock = new Mock<IValidationSetProcessor>(mockBehavior);
            ValidationOutcomeProcessorMock = new Mock<IValidationOutcomeProcessor<Package>>(mockBehavior);
            TelemetryServiceMock = new Mock<ITelemetryService>(mockBehavior);
            LoggerMock = new Mock<ILogger<PackageValidationMessageHandler>>(); // we generally don't care about how logger is called, so it's loose all the time

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
                TelemetryServiceMock.Object,
                LoggerMock.Object);
        }
    }
}

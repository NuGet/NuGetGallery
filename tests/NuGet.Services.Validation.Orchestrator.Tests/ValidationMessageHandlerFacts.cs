// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
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

            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion))
                .Returns(package)
                .Verifiable();

            ValidationSetProviderMock
                .Setup(vsp => vsp.TryGetOrCreateValidationSetAsync(messageData.ValidationTrackingId, package))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            var handler = CreateHandler();

            var result = await handler.HandleAsync(messageData);

            Assert.True(result);
            CorePackageServiceMock
                .Verify(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion), Times.Once());
            ValidationSetProviderMock
                .Verify(vsp => vsp.TryGetOrCreateValidationSetAsync(messageData.ValidationTrackingId, package), Times.Once());
        }

        [Fact]
        public async Task DropsMessageIfPackageIsSoftDeleted()
        {
            var messageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid());
            var validationConfiguration = new ValidationConfiguration();
            var package = new Package { PackageStatusKey = PackageStatus.Deleted };

            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion))
                .Returns(package);

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

        public ValidationMessageHandlerLooseFacts()
            : base(MockBehavior.Loose)
        {
            Package = new Package();
            MessageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid());
            ValidationSet = new PackageValidationSet();

            CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(MessageData.PackageId, MessageData.PackageVersion))
                .Returns(Package);

            ValidationSetProviderMock
                .Setup(vsp => vsp.TryGetOrCreateValidationSetAsync(MessageData.ValidationTrackingId, Package))
                .ReturnsAsync(ValidationSet);
        }

        [Fact]
        public async Task MakesSureValidationSetExists()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(MessageData);

            ValidationSetProviderMock
                .Verify(vsp => vsp.TryGetOrCreateValidationSetAsync(MessageData.ValidationTrackingId, Package));
        }

        [Fact]
        public async Task CallsProcessValidations()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(MessageData);

            ValidationSetProcessorMock
                .Verify(vsp => vsp.ProcessValidationsAsync(ValidationSet, Package), Times.Once());
        }

        [Fact]
        public async Task CallsProcessValidationOutcome()
        {
            var handler = CreateHandler();
            await handler.HandleAsync(MessageData);

            ValidationOutcomeProcessorMock
                .Verify(vop => vop.ProcessValidationOutcomeAsync(ValidationSet, Package, It.IsAny<ValidationSetProcessorResult>()));
        }
    }

    public class ValidationMessageHandlerFactsBase
    {
        protected static readonly ValidationConfiguration Configuration = new ValidationConfiguration
        {
            MissingPackageRetryCount = 2,
        };

        protected Mock<IOptionsSnapshot<ValidationConfiguration>> ConfigurationAccessorMock { get; }
        protected Mock<ICorePackageService> CorePackageServiceMock { get; }
        protected Mock<IValidationSetProvider> ValidationSetProviderMock { get; }
        protected Mock<IValidationSetProcessor> ValidationSetProcessorMock { get; }
        protected Mock<IValidationOutcomeProcessor> ValidationOutcomeProcessorMock { get; }
        protected Mock<ITelemetryService> TelemetryServiceMock { get; }
        protected Mock<ILogger<ValidationMessageHandler>> LoggerMock { get; }

        public ValidationMessageHandlerFactsBase(MockBehavior mockBehavior)
        {
            ConfigurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            CorePackageServiceMock = new Mock<ICorePackageService>(mockBehavior);
            ValidationSetProviderMock = new Mock<IValidationSetProvider>(mockBehavior);
            ValidationSetProcessorMock = new Mock<IValidationSetProcessor>(mockBehavior);
            ValidationOutcomeProcessorMock = new Mock<IValidationOutcomeProcessor>(mockBehavior);
            TelemetryServiceMock = new Mock<ITelemetryService>(mockBehavior);
            LoggerMock = new Mock<ILogger<ValidationMessageHandler>>(); // we generally don't care about how logger is called, so it's loose all the time

            ConfigurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(Configuration);
        }

        public ValidationMessageHandler CreateHandler()
        {
            return new ValidationMessageHandler(
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

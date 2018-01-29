// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
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
                .Verify(vop => vop.ProcessValidationOutcomeAsync(ValidationSet, Package));
        }
    }

    public class ValidationMessageHandlerFactsBase
    {
        protected Mock<ICorePackageService> CorePackageServiceMock { get; }
        protected Mock<IValidationSetProvider> ValidationSetProviderMock { get; }
        protected Mock<IValidationSetProcessor> ValidationSetProcessorMock { get; }
        protected Mock<IValidationOutcomeProcessor> ValidationOutcomeProcessorMock { get; }
        protected Mock<ILogger<ValidationMessageHandler>> LoggerMock { get; }

        public ValidationMessageHandlerFactsBase(MockBehavior mockBehavior)
        {
            CorePackageServiceMock = new Mock<ICorePackageService>(mockBehavior);
            ValidationSetProviderMock = new Mock<IValidationSetProvider>(mockBehavior);
            ValidationSetProcessorMock = new Mock<IValidationSetProcessor>(mockBehavior);
            ValidationOutcomeProcessorMock = new Mock<IValidationOutcomeProcessor>(mockBehavior);
            LoggerMock = new Mock<ILogger<ValidationMessageHandler>>(); // we generally don't care about how logger is called, so it's loose all the time
        }

        public ValidationMessageHandler CreateHandler()
        {
            return new ValidationMessageHandler(
                CorePackageServiceMock.Object,
                ValidationSetProviderMock.Object,
                ValidationSetProcessorMock.Object,
                ValidationOutcomeProcessorMock.Object,
                LoggerMock.Object);
        }
    }
}

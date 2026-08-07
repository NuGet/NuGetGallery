// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.Orchestrator;
using Moq;
using Xunit;


namespace NuGet.Services.Validation.Symbols
{
    public class SymbolMessageEnqueuerFacts
    {
        [Fact]
        public async Task SendsSerializeMessage()
        {
            SymbolsValidatorMessage message = null;
            _serializer
                .Setup(x => x.Serialize(It.IsAny<SymbolsValidatorMessage>()))
                .Returns(() => _brokeredMessage.Object)
                .Callback<SymbolsValidatorMessage>(x => message = x);

            await _target.EnqueueSymbolsValidationMessageAsync(_validationRequest);

            Assert.IsType<SymbolsValidatorMessage>(message);
            Assert.Equal(_validationRequest.ValidationId, message.ValidationId);
            Assert.Equal(_validationRequest.PackageId, message.PackageId);
            Assert.Equal(_validationRequest.PackageVersion, message.PackageNormalizedVersion);

            Assert.Equal(_validationRequest.NupkgUrl, message.SnupkgUrl);
            _serializer.Verify(
                x => x.Serialize(It.IsAny<SymbolsValidatorMessage>()),
                Times.Once);
            _topicClient.Verify(x => x.SendAsync(_brokeredMessage.Object), Times.Once);
            _topicClient.Verify(x => x.SendAsync(It.IsAny<IBrokeredMessage>()), Times.Once);
        }

        [Fact]
        public async Task SendsParentSnapshotUrl()
        {
            SymbolsValidatorMessage message = null;
            _serializer
                .Setup(x => x.Serialize(It.IsAny<SymbolsValidatorMessage>()))
                .Returns(() => _brokeredMessage.Object)
                .Callback<SymbolsValidatorMessage>(x => message = x);
            var request = new SymbolsValidationRequest(
                _validationRequest.ValidationId,
                42,
                _validationRequest.PackageId,
                _validationRequest.PackageVersion,
                _validationRequest.NupkgUrl,
                "http://example/parent/nuget.versioning.4.6.0.nupkg?my-sas");

            await _target.EnqueueSymbolsValidationMessageAsync(request);

            Assert.Equal(request.ValidationId, message.ValidationId);
            Assert.Equal(request.NupkgUrl, message.SnupkgUrl);
            Assert.Equal(request.ParentNupkgSnapshotUrl, message.ParentNupkgSnapshotUrl);
            _topicClient.Verify(x => x.SendAsync(_brokeredMessage.Object), Times.Once);
        }

        private readonly Mock<ITopicClient> _topicClient;
        private readonly Mock<IBrokeredMessageSerializer<SymbolsValidatorMessage>> _serializer;
        private readonly SymbolsValidationConfiguration _configuration;
        private readonly Mock<IBrokeredMessage> _brokeredMessage;
        private readonly SymbolsValidationRequest _validationRequest;
        private readonly SymbolsMessageEnqueuer _target;

        public SymbolMessageEnqueuerFacts()
        {
            _configuration = new SymbolsValidationConfiguration();
            _brokeredMessage = new Mock<IBrokeredMessage>();
            _validationRequest = new SymbolsValidationRequest(
                new Guid("ab2629ce-2d67-403a-9a42-49748772ae90"),
                42,
                "NuGet.Versioning",
                "4.6.0",
                "http://example/nuget.versioning.4.6.0.nupkg?my-sas");
            _brokeredMessage.SetupProperty(x => x.ScheduledEnqueueTimeUtc);

            _topicClient = new Mock<ITopicClient>();

            _serializer = new Mock<IBrokeredMessageSerializer<SymbolsValidatorMessage>>();
            _serializer
                .Setup(x => x.Serialize(It.IsAny<SymbolsValidatorMessage>()))
                .Returns(() => _brokeredMessage.Object);

            _target = new SymbolsMessageEnqueuer(
                _topicClient.Object,
                _serializer.Object,
                TimeSpan.FromSeconds(1));
        }
    }
}

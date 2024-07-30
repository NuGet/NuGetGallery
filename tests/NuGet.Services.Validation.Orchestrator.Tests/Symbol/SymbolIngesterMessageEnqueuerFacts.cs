// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.ServiceBus;
using Moq;
using Xunit;


namespace NuGet.Services.Validation.Symbols
{
    public class SymbolIngesterMessageEnqueuerFacts
    {
        [Fact]
        public async Task SendsSerializeMessage()
        {
            // Arrange
            SymbolsIngesterMessage message = null;
            _serializer
                .Setup(x => x.Serialize(It.IsAny<SymbolsIngesterMessage>()))
                .Returns(() => _brokeredMessage.Object)
                .Callback<SymbolsIngesterMessage>(x => message = x);

            // Act
            await _target.EnqueueSymbolsIngestionMessageAsync(_validationRequest.Object);

            // Assert
            Assert.Equal(_validationRequest.Object.ValidationId, message.ValidationId);
            Assert.Equal(_validationRequest.Object.PackageId, message.PackageId);
            Assert.Equal(_validationRequest.Object.PackageVersion, message.PackageNormalizedVersion);
            Assert.Equal($"{_validationRequest.Object.PackageKey}_{_validationRequest.Object.ValidationId}", message.RequestName);

            Assert.Equal(_validationRequest.Object.NupkgUrl, message.SnupkgUrl);
            _serializer.Verify(
                x => x.Serialize(It.IsAny<SymbolsIngesterMessage>()),
                Times.Once);
            _topicClient.Verify(x => x.SendAsync(_brokeredMessage.Object), Times.Once);
            _topicClient.Verify(x => x.SendAsync(It.IsAny<IBrokeredMessage>()), Times.Once);
        }

        private readonly Mock<ITopicClient> _topicClient;
        private readonly Mock<IBrokeredMessageSerializer<SymbolsIngesterMessage>> _serializer;
        private readonly SymbolsValidationConfiguration _configuration;
        private readonly Mock<IBrokeredMessage> _brokeredMessage;
        private readonly Mock<INuGetValidationRequest> _validationRequest;
        private readonly SymbolsIngesterMessageEnqueuer _target;

        public SymbolIngesterMessageEnqueuerFacts()
        {
            _configuration = new SymbolsValidationConfiguration();
            _brokeredMessage = new Mock<IBrokeredMessage>();
            _validationRequest = new Mock<INuGetValidationRequest>();

            _validationRequest.Setup(x => x.ValidationId).Returns(new Guid("ab2629ce-2d67-403a-9a42-49748772ae90"));
            _validationRequest.Setup(x => x.PackageId).Returns("NuGet.Versioning");
            _validationRequest.Setup(x => x.PackageKey).Returns(123);
            _validationRequest.Setup(x => x.PackageVersion).Returns("4.6.0");
            _validationRequest.Setup(x => x.NupkgUrl).Returns("http://example/nuget.versioning.4.6.0.nupkg?my-sas");
            _brokeredMessage.SetupProperty(x => x.ScheduledEnqueueTimeUtc);

            _topicClient = new Mock<ITopicClient>();

            _serializer = new Mock<IBrokeredMessageSerializer<SymbolsIngesterMessage>>();
            _serializer
                .Setup(x => x.Serialize(It.IsAny<SymbolsIngesterMessage>()))
                .Returns(() => _brokeredMessage.Object);

            _target = new SymbolsIngesterMessageEnqueuer(
                _topicClient.Object,
                _serializer.Object,
                TimeSpan.FromSeconds(1));
        }
    }
}

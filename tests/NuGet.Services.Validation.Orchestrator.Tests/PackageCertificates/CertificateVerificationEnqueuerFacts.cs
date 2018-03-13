// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.PackageCertificates;
using Xunit;

namespace NuGet.Services.Validation.PackageSigning
{
    public class CertificateVerificationEnqueuerFacts
    {
        [Fact]
        public async Task UsesConfiguredMessageDelay()
        {
            var messageDelay = TimeSpan.FromMinutes(1);
            _configuration.MessageDelay = messageDelay;

            var before = DateTimeOffset.UtcNow;
            await _target.EnqueueVerificationAsync(_validationRequest.Object, _endCertificate);
            var after = DateTimeOffset.UtcNow;

            Assert.InRange(_brokeredMessage.Object.ScheduledEnqueueTimeUtc, before.Add(messageDelay), after.Add(messageDelay));
        }

        [Fact]
        public async Task DefaultsNullMessageDelayToZero()
        {
            var before = DateTimeOffset.UtcNow;
            await _target.EnqueueVerificationAsync(_validationRequest.Object, _endCertificate);
            var after = DateTimeOffset.UtcNow;

            Assert.InRange(_brokeredMessage.Object.ScheduledEnqueueTimeUtc, before, after);
        }

        [Fact]
        public async Task SendsSerializeMessage()
        {
            CertificateValidationMessage message = null;
            _serializer
                .Setup(x => x.Serialize(It.IsAny<CertificateValidationMessage>()))
                .Returns(() => _brokeredMessage.Object)
                .Callback<CertificateValidationMessage>(x => message = x);

            await _target.EnqueueVerificationAsync(_validationRequest.Object, _endCertificate);

            Assert.Equal(_validationRequest.Object.ValidationId, message.ValidationId);
            Assert.Equal(_endCertificate.Key, message.CertificateKey);
            Assert.False(message.RevalidateRevokedCertificate);
            _serializer.Verify(
                x => x.Serialize(It.IsAny<CertificateValidationMessage>()),
                Times.Once);
            _topicClient.Verify(x => x.SendAsync(_brokeredMessage.Object), Times.Once);
            _topicClient.Verify(x => x.SendAsync(It.IsAny<IBrokeredMessage>()), Times.Once);
        }

        private readonly Mock<ITopicClient> _topicClient;
        private readonly Mock<IBrokeredMessageSerializer<CertificateValidationMessage>> _serializer;
        private readonly Mock<IOptionsSnapshot<PackageCertificatesConfiguration>> _options;
        private readonly PackageCertificatesConfiguration _configuration;
        private readonly Mock<IBrokeredMessage> _brokeredMessage;
        private readonly Mock<IValidationRequest> _validationRequest;
        private readonly EndCertificate _endCertificate;
        private readonly CertificateVerificationEnqueuer _target;

        public CertificateVerificationEnqueuerFacts()
        {
            _configuration = new PackageCertificatesConfiguration();
            _brokeredMessage = new Mock<IBrokeredMessage>();
            _validationRequest = new Mock<IValidationRequest>();
            _endCertificate = new EndCertificate { Key = 23 };

            _validationRequest.Setup(x => x.ValidationId).Returns(new Guid("68fc78da-af04-4e4e-8128-de68dcfec3ba"));
            _brokeredMessage.SetupProperty(x => x.ScheduledEnqueueTimeUtc);

            _topicClient = new Mock<ITopicClient>();
            _serializer = new Mock<IBrokeredMessageSerializer<CertificateValidationMessage>>();
            _options = new Mock<IOptionsSnapshot<PackageCertificatesConfiguration>>();

            _options.Setup(x => x.Value).Returns(() => _configuration);
            _serializer
                .Setup(x => x.Serialize(It.IsAny<CertificateValidationMessage>()))
                .Returns(() => _brokeredMessage.Object);

            _target = new CertificateVerificationEnqueuer(
                _topicClient.Object,
                _serializer.Object,
                _options.Object);
        }
    }
}

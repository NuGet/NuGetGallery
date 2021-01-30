// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.PackageSigning.ProcessSignature;
using Xunit;

namespace NuGet.Services.Validation.PackageSigning
{
    public class PackageSignatureVerificationEnqueuerFacts
    {
        [Fact]
        public async Task UsesConfiguredMessageDelay()
        {
            var messageDelay = TimeSpan.FromMinutes(1);
            _configuration.MessageDelay = messageDelay;

            var before = DateTimeOffset.UtcNow;
            await _target.EnqueueProcessSignatureAsync(_validationRequest.Object, requireRepositorySignature: false);
            var after = DateTimeOffset.UtcNow;

            Assert.InRange(_brokeredMessage.Object.ScheduledEnqueueTimeUtc, before.Add(messageDelay), after.Add(messageDelay));
        }

        [Fact]
        public async Task DefaultsNullMessageDelayToZero()
        {
            var before = DateTimeOffset.UtcNow;
            await _target.EnqueueProcessSignatureAsync(_validationRequest.Object, requireRepositorySignature: false);
            var after = DateTimeOffset.UtcNow;

            Assert.InRange(_brokeredMessage.Object.ScheduledEnqueueTimeUtc, before, after);
        }

        [Fact]
        public async Task SendsSerializeMessage()
        {
            SignatureValidationMessage message = null;
            _serializer
                .Setup(x => x.Serialize(It.IsAny<SignatureValidationMessage>()))
                .Returns(() => _brokeredMessage.Object)
                .Callback<SignatureValidationMessage>(x => message = x);

            await _target.EnqueueProcessSignatureAsync(_validationRequest.Object, requireRepositorySignature: false);

            Assert.Equal(_validationRequest.Object.ValidationId, message.ValidationId);
            Assert.Equal(_validationRequest.Object.PackageId, message.PackageId);
            Assert.Equal(_validationRequest.Object.PackageVersion, message.PackageVersion);
            Assert.Equal(_validationRequest.Object.NupkgUrl, message.NupkgUri.AbsoluteUri);
            _serializer.Verify(
                x => x.Serialize(It.IsAny<SignatureValidationMessage>()),
                Times.Once);
            _topicClient.Verify(x => x.SendAsync(_brokeredMessage.Object), Times.Once);
            _topicClient.Verify(x => x.SendAsync(It.IsAny<IBrokeredMessage>()), Times.Once);
        }

        private readonly Mock<ITopicClient> _topicClient;
        private readonly Mock<IBrokeredMessageSerializer<SignatureValidationMessage>> _serializer;
        private readonly Mock<IOptionsSnapshot<ProcessSignatureConfiguration>> _options;
        private readonly ProcessSignatureConfiguration _configuration;
        private readonly Mock<IBrokeredMessage> _brokeredMessage;
        private readonly Mock<INuGetValidationRequest> _validationRequest;
        private readonly ProcessSignatureEnqueuer _target;

        public PackageSignatureVerificationEnqueuerFacts()
        {
            _configuration = new ProcessSignatureConfiguration();
            _brokeredMessage = new Mock<IBrokeredMessage>();
            _validationRequest = new Mock<INuGetValidationRequest>();

            _validationRequest.Setup(x => x.ValidationId).Returns(new Guid("ab2629ce-2d67-403a-9a42-49748772ae90"));
            _validationRequest.Setup(x => x.PackageId).Returns("NuGet.Versioning");
            _validationRequest.Setup(x => x.PackageVersion).Returns("4.6.0");
            _validationRequest.Setup(x => x.NupkgUrl).Returns("http://example/nuget.versioning.4.6.0.nupkg?my-sas");
            _brokeredMessage.SetupProperty(x => x.ScheduledEnqueueTimeUtc);

            _topicClient = new Mock<ITopicClient>();
            _serializer = new Mock<IBrokeredMessageSerializer<SignatureValidationMessage>>();
            _options = new Mock<IOptionsSnapshot<ProcessSignatureConfiguration>>();

            _options.Setup(x => x.Value).Returns(() => _configuration);
            _serializer
                .Setup(x => x.Serialize(It.IsAny<SignatureValidationMessage>()))
                .Returns(() => _brokeredMessage.Object);

            _target = new ProcessSignatureEnqueuer(
                _topicClient.Object,
                _serializer.Object,
                _options.Object);
        }
    }
}

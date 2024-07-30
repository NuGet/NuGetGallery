// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.ContentScan;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;
using Xunit;

namespace Validation.ContentScan.Tests
{
    public class TheContentScanEnqueuerConstructor : ContentScanEnqueuerFactsBase
    {
        [Fact]
        public void ThrowsWhenTopicClientIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ContentScanEnqueuer(
                null,
                _serializerMock.Object,
                _configurationAccessorMock.Object,
                _logger));

            Assert.Equal("topicClient", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenSerializerIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ContentScanEnqueuer(
                _topicClientMock.Object,
                null,
                _configurationAccessorMock.Object,
                _logger));

            Assert.Equal("serializer", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenConfigurationAccessorIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ContentScanEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                null,
                _logger));

            Assert.Equal("configurationAccessor", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenConfigurationAccessorValueIsNull()
        {
            _configurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns((ContentScanEnqueuerConfiguration)null);

            var ex = Assert.Throws<ArgumentException>(() => new ContentScanEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                _configurationAccessorMock.Object,
                _logger));

            Assert.Equal("configurationAccessor", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenLoggerIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ContentScanEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                _configurationAccessorMock.Object,
                null));

            Assert.Equal("logger", ex.ParamName);
        }
    }

    public class EnqueueContentScanAsyncMethod : ContentScanEnqueuerFactsBase
    {
        [Fact]
        public async Task ThrowsWhenUrlIsNull()
        {
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.EnqueueContentScanAsync(Guid.NewGuid(), null));
            Assert.Equal("inputUrl", ex.ParamName);
        }

        [Fact]
        public async Task PassesDataToSerializedMessage()
        {
            await _target.EnqueueContentScanAsync(_validationRequest.ValidationStepId, _validationRequest.InputUrl);

            _serializerMock
                .Verify(s => s.Serialize(It.IsAny<ContentScanData>()), Times.Once);

            Assert.Equal(_validationRequest.ValidationStepId, _capturedMessage.StartContentScan.ValidationStepId);
            Assert.Equal(_validationRequest.InputUrl, _capturedMessage.StartContentScan.BlobUri);
            Assert.Equal(ContentScanOperationType.StartScan, _capturedMessage.Type);
        }

        [Fact]
        public async Task SetsEnqueueTime()
        {
            const int messageDelayDays = 137;
            _configuration.MessageDelay = TimeSpan.FromDays(messageDelayDays);

            await _target.EnqueueContentScanAsync(_validationRequest.ValidationStepId, _validationRequest.InputUrl);

            Assert.Equal(messageDelayDays, (_serializedMessage.ScheduledEnqueueTimeUtc - DateTimeOffset.UtcNow).TotalDays, 0);
        }

        [Theory]
        [InlineData(23, 25, 25)]
        [InlineData(42, 25, 25)]
        public async Task SetsEnqueueTimeWhenOverriden(int cfgDelayDays, int argDelayDays, int expectedDelayDays)
        {
            _configuration.MessageDelay = TimeSpan.FromDays(cfgDelayDays);

            await _target.EnqueueContentScanAsync(
                _validationRequest.ValidationStepId,
                _validationRequest.InputUrl,
                messageDeliveryDelayOverride: TimeSpan.FromDays(argDelayDays));

            Assert.Equal(expectedDelayDays, (_serializedMessage.ScheduledEnqueueTimeUtc - DateTimeOffset.UtcNow).TotalDays, 0);
        }

        [Fact]
        public async Task SendsMessage()
        {
            var request = new ValidationRequest(Guid.NewGuid(), new Uri("https://example.com/testpackage.nupkg"));
            await _target.EnqueueContentScanAsync(request.ValidationStepId, request.InputUrl);

            Assert.Same(_serializedMessage, _capturedBrokeredMessage);
        }
    }

    public class EnqueueCheckContentScanStatusAsyncMethod : ContentScanEnqueuerFactsBase
    {
        [Fact]
        public async Task PassesDataToSerializedMessage()
        {
            await _target.EnqueueContentScanStatusCheckAsync(_validationRequest.ValidationStepId);

            _serializerMock
                .Verify(s => s.Serialize(It.IsAny<ContentScanData>()), Times.Once);

            Assert.Equal(_validationRequest.ValidationStepId, _capturedMessage.CheckContentScanStatus.ValidationStepId);
            Assert.Equal(ContentScanOperationType.CheckStatus, _capturedMessage.Type);
        }

        [Fact]
        public async Task SetsEnqueueTime()
        {
            const int messageDelayDays = 137;
            _configuration.MessageDelay = TimeSpan.FromDays(messageDelayDays);

            await _target.EnqueueContentScanStatusCheckAsync(_validationRequest.ValidationStepId);

            Assert.Equal(messageDelayDays, (_serializedMessage.ScheduledEnqueueTimeUtc - DateTimeOffset.UtcNow).TotalDays, 0);
        }

        [Theory]
        [InlineData(23, 25, 25)]
        [InlineData(42, 25, 25)]
        public async Task SetsEnqueueTimeWhenOverriden(int cfgDelayDays, int argDelayDays, int expectedDelayDays)
        {
            _configuration.MessageDelay = TimeSpan.FromDays(cfgDelayDays);

            await _target.EnqueueContentScanStatusCheckAsync(
                _validationRequest.ValidationStepId,
                messageDeliveryDelayOverride: TimeSpan.FromDays(argDelayDays));

            Assert.Equal(expectedDelayDays, (_serializedMessage.ScheduledEnqueueTimeUtc - DateTimeOffset.UtcNow).TotalDays, 0);
        }

        [Fact]
        public async Task SendsMessage()
        {
            var request = new ValidationRequest(Guid.NewGuid(), new Uri("https://example.com/testpackage.nupkg"));
            await _target.EnqueueContentScanStatusCheckAsync(request.ValidationStepId);

            Assert.Same(_serializedMessage, _capturedBrokeredMessage);
        }
    }

    public class ContentScanEnqueuerFactsBase
    {
        protected Mock<ITopicClient> _topicClientMock;
        protected Mock<IBrokeredMessageSerializer<ContentScanData>> _serializerMock;
        protected ILogger<ContentScanEnqueuer> _logger;
        protected Mock<IOptionsSnapshot<ContentScanEnqueuerConfiguration>> _configurationAccessorMock;
        protected ContentScanEnqueuerConfiguration _configuration;
        protected ContentScanEnqueuer _target;

        protected ContentScanData _capturedMessage;
        protected IBrokeredMessage _capturedBrokeredMessage;
        protected ServiceBusMessageWrapper _serializedMessage;

        protected readonly IValidationRequest _validationRequest;
        protected readonly List<string> _owners;

        public ContentScanEnqueuerFactsBase()
        {
            _topicClientMock = new Mock<ITopicClient>();
            _serializerMock = new Mock<IBrokeredMessageSerializer<ContentScanData>>();
            _logger = Mock.Of<ILogger<ContentScanEnqueuer>>();
            _configurationAccessorMock = new Mock<IOptionsSnapshot<ContentScanEnqueuerConfiguration>>();

            _configuration = new ContentScanEnqueuerConfiguration();
            _configurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(_configuration);

            _target = new ContentScanEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                _configurationAccessorMock.Object,
                _logger);

            _validationRequest = new ValidationRequest(Guid.NewGuid(), new Uri("https://example.com/testpackage.nupkg"));

            _serializedMessage = new ServiceBusMessageWrapper("somedata");

            _serializerMock
                .Setup(s => s.Serialize(It.IsAny<ContentScanData>()))
                .Callback<ContentScanData>(m => _capturedMessage = m)
                .Returns(_serializedMessage);

            _topicClientMock
                .Setup(tc => tc.SendAsync(It.IsAny<IBrokeredMessage>()))
                .Callback<IBrokeredMessage>(m => _capturedBrokeredMessage = m)
                .Returns(Task.CompletedTask);
        }
    }
}

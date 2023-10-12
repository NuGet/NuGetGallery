// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Orchestrator;
using Xunit;

namespace Validation.PackageSigning.ScanAndSign.Tests
{
    public class TheScanAndSignEnqueuerConstructor : ScanAndSignEnqueuerFactsBase
    {
        [Fact]
        public void ThrowsWhenTopicClientIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignEnqueuer(
                null,
                _serializerMock.Object,
                _configurationAccessorMock.Object,
                _logger));

            Assert.Equal("topicClient", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenSerializerIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                null,
                _configurationAccessorMock.Object,
                _logger));

            Assert.Equal("serializer", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenConfigurationAccessorIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignEnqueuer(
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
                .Returns((ScanAndSignEnqueuerConfiguration)null);

            var ex = Assert.Throws<ArgumentException>(() => new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                _configurationAccessorMock.Object,
                _logger));

            Assert.Equal("configurationAccessor", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenLoggerIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                _configurationAccessorMock.Object,
                null));

            Assert.Equal("logger", ex.ParamName);
        }
    }

    public class TheEnqueueScanAsyncMethod : ScanAndSignEnqueuerFactsBase
    {
        [Fact]
        public async Task ThrowsWhenUrlIsNull()
        {
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.EnqueueScanAsync(Guid.NewGuid(), null, new Dictionary<string, string>()));
            Assert.Equal("nupkgUrl", ex.ParamName);
        }

        [Fact]
        public async Task ThrowsWhenContextIsNull()
        {
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.EnqueueScanAsync(Guid.NewGuid(), "https://url", null));
            Assert.Equal("context", ex.ParamName);
        }

        [Fact]
        public async Task PassesDataToSerializedMessage()
        {
            var context = new Dictionary<string, string> { { "key1", "value4" }, { "key2", "value2" }, { "key4", "value1" }, };

            await _target.EnqueueScanAsync(_validationRequest.ValidationId, _validationRequest.NupkgUrl, context);

            _serializerMock
                .Verify(s => s.Serialize(It.IsAny<ScanAndSignMessage>()), Times.Once);

            Assert.Equal(_validationRequest.ValidationId, _capturedMessage.PackageValidationId);
            Assert.Equal(OperationRequestType.Scan, _capturedMessage.OperationRequestType);
            Assert.Equal(_validationRequest.NupkgUrl, _capturedMessage.BlobUri.AbsoluteUri);
            Assert.Null(_capturedMessage.V3ServiceIndexUrl);
            Assert.Null(_capturedMessage.Owners);
            Assert.Equal(context, _capturedMessage.Context);
        }

        [Fact]
        public async Task SetsEnqueueTime()
        {
            const int messageDelayDays = 137;
            _configuration.MessageDelay = TimeSpan.FromDays(messageDelayDays);

            await _target.EnqueueScanAsync(_validationRequest.ValidationId, _validationRequest.NupkgUrl, new Dictionary<string, string>());

            Assert.Equal(messageDelayDays, (_serializedMessage.ScheduledEnqueueTimeUtc - DateTimeOffset.UtcNow).TotalDays, 0);
        }

        [Theory]
        [InlineData(23, 25, 25)]
        [InlineData(42, 25, 25)]
        public async Task SetsEnqueueTimeWhenOverriden(int cfgDelayDays, int argDelayDays, int expectedDelayDays)
        {
            _configuration.MessageDelay = TimeSpan.FromDays(cfgDelayDays);

            await _target.EnqueueScanAsync(
                _validationRequest.ValidationId,
                _validationRequest.NupkgUrl,
                new Dictionary<string, string>(),
                messageDeliveryDelayOverride: TimeSpan.FromDays(argDelayDays));

            Assert.Equal(expectedDelayDays, (_serializedMessage.ScheduledEnqueueTimeUtc - DateTimeOffset.UtcNow).TotalDays, 0);
        }

        [Fact]
        public async Task SendsMessage()
        {
            var request = new NuGetValidationRequest(Guid.NewGuid(), 42, "somepackage", "someversion", "https://example.com/testpackage.nupkg");
            await _target.EnqueueScanAsync(request.ValidationId, request.NupkgUrl, new Dictionary<string, string>());

            Assert.Same(_serializedMessage, _capturedBrokeredMessage);
        }
    }

    public class TheEnqueueScanAndSignAsyncMethod : ScanAndSignEnqueuerFactsBase
    {
        public const string V3ServiceIndexUrl = "https://someurl";

        [Fact]
        public async Task ThrowsWhenParametersAreMissing()
        {
            var ex1 = await Assert.ThrowsAsync<ArgumentNullException>(()
                => _target.EnqueueScanAndSignAsync(_validationRequest.ValidationId, null, V3ServiceIndexUrl, _owners, new Dictionary<string, string>()));
            var ex2 = await Assert.ThrowsAsync<ArgumentException>(()
                => _target.EnqueueScanAndSignAsync(_validationRequest.ValidationId, _validationRequest.NupkgUrl, null, _owners, new Dictionary<string, string>()));
            var ex3 = await Assert.ThrowsAsync<ArgumentNullException>(()
                => _target.EnqueueScanAndSignAsync(_validationRequest.ValidationId, _validationRequest.NupkgUrl, V3ServiceIndexUrl, null, new Dictionary<string, string>()));
            var ex4 = await Assert.ThrowsAsync<ArgumentNullException>(()
                => _target.EnqueueScanAndSignAsync(_validationRequest.ValidationId, _validationRequest.NupkgUrl, V3ServiceIndexUrl, _owners, null));

            Assert.Equal("nupkgUrl", ex1.ParamName);
            Assert.Equal("v3ServiceIndexUrl", ex2.ParamName);
            Assert.Equal("owners", ex3.ParamName);
            Assert.Equal("context", ex4.ParamName);
        }

        [Fact]
        public async Task PassesDataToSerializedMessage()
        {
            await _target.EnqueueScanAndSignAsync(_validationRequest.ValidationId, _validationRequest.NupkgUrl, V3ServiceIndexUrl, _owners, new Dictionary<string, string>());

            _serializerMock
                .Verify(s => s.Serialize(It.IsAny<ScanAndSignMessage>()), Times.Once);

            Assert.Equal(_validationRequest.ValidationId, _capturedMessage.PackageValidationId);
            Assert.Equal(OperationRequestType.Sign, _capturedMessage.OperationRequestType);
            Assert.Equal(_validationRequest.NupkgUrl, _capturedMessage.BlobUri.AbsoluteUri);
            Assert.Equal(V3ServiceIndexUrl, _capturedMessage.V3ServiceIndexUrl);
            Assert.Equal(_owners, _capturedMessage.Owners);
        }

        [Fact]
        public async Task SetsEnqueueTime()
        {
            const int messageDelayDays = 137;
            _configuration.MessageDelay = TimeSpan.FromDays(messageDelayDays);

            await _target.EnqueueScanAndSignAsync(_validationRequest.ValidationId, _validationRequest.NupkgUrl, V3ServiceIndexUrl, _owners, new Dictionary<string, string>());

            Assert.Equal(messageDelayDays, (_serializedMessage.ScheduledEnqueueTimeUtc - DateTimeOffset.UtcNow).TotalDays, 0);
        }

        [Theory]
        [InlineData(23, 25, 25)]
        [InlineData(42, 25, 25)]
        public async Task SetsEnqueueTimeWhenOverriden(int cfgDelayDays, int argDelayDays, int expectedDelayDays)
        {
            _configuration.MessageDelay = TimeSpan.FromDays(cfgDelayDays);

            await _target.EnqueueScanAndSignAsync(_validationRequest.ValidationId,
                _validationRequest.NupkgUrl,
                V3ServiceIndexUrl,
                _owners,
                new Dictionary<string, string>(),
                messageDeliveryDelayOverride: TimeSpan.FromDays(argDelayDays));

            Assert.Equal(expectedDelayDays, (_serializedMessage.ScheduledEnqueueTimeUtc - DateTimeOffset.UtcNow).TotalDays, 0);
        }

        [Fact]
        public async Task SendsMessage()
        {
            await _target.EnqueueScanAndSignAsync(_validationRequest.ValidationId, _validationRequest.NupkgUrl, V3ServiceIndexUrl, _owners, new Dictionary<string, string>());

            Assert.Same(_serializedMessage, _capturedBrokeredMessage);
        }
    }

    public class ScanAndSignEnqueuerFactsBase
    {
        protected Mock<ITopicClient> _topicClientMock;
        protected Mock<IBrokeredMessageSerializer<ScanAndSignMessage>> _serializerMock;
        protected ILogger<ScanAndSignEnqueuer> _logger;
        protected Mock<IOptionsSnapshot<ScanAndSignEnqueuerConfiguration>> _configurationAccessorMock;
        protected ScanAndSignEnqueuerConfiguration _configuration;
        protected ScanAndSignEnqueuer _target;

        protected ScanAndSignMessage _capturedMessage;
        protected IBrokeredMessage _capturedBrokeredMessage;
        protected ServiceBusMessageWrapper _serializedMessage;

        protected readonly INuGetValidationRequest _validationRequest;
        protected readonly List<string> _owners;

        public ScanAndSignEnqueuerFactsBase()
        {
            _topicClientMock = new Mock<ITopicClient>();
            _serializerMock = new Mock<IBrokeredMessageSerializer<ScanAndSignMessage>>();
            _logger = Mock.Of<ILogger<ScanAndSignEnqueuer>>();
            _configurationAccessorMock = new Mock<IOptionsSnapshot<ScanAndSignEnqueuerConfiguration>>();

            _configuration = new ScanAndSignEnqueuerConfiguration();
            _configurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(_configuration);

            _target = new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                _configurationAccessorMock.Object,
                _logger);

            _validationRequest = new NuGetValidationRequest(Guid.NewGuid(), 42, "somepackage", "someversion", "https://example.com/testpackage.nupkg");
            _owners = new List<string> {"Billy", "Bob"};

            _serializedMessage = new ServiceBusMessageWrapper("somedata");

            _serializerMock
                .Setup(s => s.Serialize(It.IsAny<ScanAndSignMessage>()))
                .Callback<ScanAndSignMessage>(m => _capturedMessage = m)
                .Returns(_serializedMessage);

            _topicClientMock
                .Setup(tc => tc.SendAsync(It.IsAny<IBrokeredMessage>()))
                .Callback<IBrokeredMessage>(m => _capturedBrokeredMessage = m)
                .Returns(Task.CompletedTask);
        }
    }
}

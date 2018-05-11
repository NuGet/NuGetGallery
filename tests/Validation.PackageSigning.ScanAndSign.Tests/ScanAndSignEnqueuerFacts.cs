// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
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
                _configurationAccessorMock.Object));

            Assert.Equal("topicClient", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenSerializerIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                null,
                _configurationAccessorMock.Object));

            Assert.Equal("serializer", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenConfigurationAccessorIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                null));

            Assert.Equal("configurationAccessor", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenConfigurationAccessorValueIsNull()
        {
            _configurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns((ScanAndSignConfiguration)null);


            var ex = Assert.Throws<ArgumentException>(() => new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                _configurationAccessorMock.Object));

            Assert.Equal("configurationAccessor", ex.ParamName);
        }
    }

    public class TheEnqueueScanAsyncMethod : ScanAndSignEnqueuerFactsBase
    {
        [Fact]
        public async Task ThrowsWhenRequestIsNull()
        {
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.EnqueueScanAsync(null));
            Assert.Equal("request", ex.ParamName);
        }

        [Fact]
        public async Task PassesDataToSerializedMessage()
        {
            var request = new ValidationRequest(Guid.NewGuid(), 42, "somepackage", "someversion", "https://example.com/testpackage.nupkg");

            await _target.EnqueueScanAsync(request);

            _serializerMock
                .Verify(s => s.Serialize(It.IsAny<ScanAndSignMessage>()), Times.Once);
            Assert.Equal(request.ValidationId, _capturedMessage.PackageValidationId);
            Assert.Equal(OperationRequestType.Scan, _capturedMessage.OperationRequestType);
            Assert.Equal(request.NupkgUrl, _capturedMessage.BlobUri.AbsoluteUri);
        }

        [Fact]
        public async Task SetsEnqueueTime()
        {
            const int messageDelayDays = 137;
            _configuration.MessageDelay = TimeSpan.FromDays(messageDelayDays);
            var request = new ValidationRequest(Guid.NewGuid(), 42, "somepackage", "someversion", "https://example.com/testpackage.nupkg");

            await _target.EnqueueScanAsync(request);

            Assert.Equal(messageDelayDays, (_serializedMessage.ScheduledEnqueueTimeUtc - DateTimeOffset.UtcNow).TotalDays, 0);
        }

        [Fact]
        public async Task SendsMessage()
        {
            var request = new ValidationRequest(Guid.NewGuid(), 42, "somepackage", "someversion", "https://example.com/testpackage.nupkg");
            await _target.EnqueueScanAsync(request);

            Assert.Same(_serializedMessage, _capturedBrokeredMessage);
        }

        private ScanAndSignMessage _capturedMessage;
        private IBrokeredMessage _capturedBrokeredMessage;
        private BrokeredMessageWrapper _serializedMessage;

        public TheEnqueueScanAsyncMethod()
        {
            _serializedMessage = new BrokeredMessageWrapper("somedata");

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

    public class ScanAndSignEnqueuerFactsBase
    {
        protected Mock<ITopicClient> _topicClientMock;
        protected Mock<IBrokeredMessageSerializer<ScanAndSignMessage>> _serializerMock;
        protected Mock<IOptionsSnapshot<ScanAndSignConfiguration>> _configurationAccessorMock;
        protected ScanAndSignConfiguration _configuration;
        protected ScanAndSignEnqueuer _target;

        public ScanAndSignEnqueuerFactsBase()
        {
            _topicClientMock = new Mock<ITopicClient>();
            _serializerMock = new Mock<IBrokeredMessageSerializer<ScanAndSignMessage>>();
            _configurationAccessorMock = new Mock<IOptionsSnapshot<ScanAndSignConfiguration>>();

            _configuration = new ScanAndSignConfiguration();
            _configurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(_configuration);

            _target = new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                _configurationAccessorMock.Object);
        }
    }
}

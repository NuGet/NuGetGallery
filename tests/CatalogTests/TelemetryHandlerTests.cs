// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Moq;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class TelemetryHandlerTests
    {
        private readonly Mock<TelemetryService> _telemetryService;
        private Func<Task<HttpResponseMessage>> _sendAsync;
        private readonly FuncHttpMessageHandler _innerHandler;
        private readonly HttpRequestMessage _request;
        private readonly TelemetryHandler _target;
        private readonly HttpClient _httpClient;
        private IDictionary<string, string> _properties;

        public TelemetryHandlerTests()
        {
            _telemetryService = new Mock<TelemetryService>(new TelemetryClient());
            _telemetryService.Setup(x => x.TrackDuration(TelemetryConstants.HttpHeaderDurationSeconds, It.IsAny<IDictionary<string, string>>()))
                             .Callback((string name, IDictionary<string, string> properties) => { _properties = properties; }).CallBase();

            _sendAsync = () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Hello, world!"),
            });
            _innerHandler = new FuncHttpMessageHandler(() => _sendAsync);
            _request = new HttpRequestMessage(HttpMethod.Get, "http://example/robots.txt");
            _target = new TelemetryHandler(_telemetryService.Object, _innerHandler);
            _httpClient = new HttpClient(_target);
        }

        [Fact]
        public async Task EmitsTelemetryOnException()
        {
            // Arrange
            var expected = new HttpRequestException("Bad!");
            _sendAsync = () => throw expected;

            // Act & Assert
            var actual = await Assert.ThrowsAsync<HttpRequestException>(() => _httpClient.SendAsync(_request));
            Assert.Same(expected, actual);

            _telemetryService.Verify(x => x.TrackDuration(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Once);

            Assert.Equal(2, _properties.Count);
            Assert.Equal(_request.Method.ToString(), _properties[TelemetryConstants.Method]);
            Assert.Equal(_request.RequestUri.AbsoluteUri, _properties[TelemetryConstants.Uri]);
        }

        [Fact]
        public async Task EmitsTelemetryOnFailedRequest()
        {
            // Arrange
            var expected = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Bad!"),
            };
            _sendAsync = () => Task.FromResult(expected);

            // Act
            var actual = await _httpClient.SendAsync(_request);

            // Assert
            Assert.Same(expected, actual);
            _telemetryService.Verify(x => x.TrackDuration(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Once);

            Assert.Equal(5, _properties.Count);
            Assert.Equal(_request.Method.ToString(), _properties[TelemetryConstants.Method]);
            Assert.Equal(_request.RequestUri.AbsoluteUri, _properties[TelemetryConstants.Uri]);
            Assert.Equal(((int)expected.StatusCode).ToString(), _properties[TelemetryConstants.StatusCode]);
            Assert.Equal("False", _properties[TelemetryConstants.Success]);
            Assert.Equal(_properties[TelemetryConstants.ContentLength], expected.Content.Headers.ContentLength.Value.ToString());
        }

        [Fact]
        public async Task EmitsTelemetryOnSuccessfulRequest()
        {
            // Arrange
            var expected = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Hello, world!"),
            };
            _sendAsync = () => Task.FromResult(expected);

            // Act
            var actual = await _httpClient.SendAsync(_request);

            // Assert
            Assert.Same(expected, actual);
            _telemetryService.Verify(x => x.TrackDuration(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Once);

            Assert.Equal(5, _properties.Count);
            Assert.Equal(_request.Method.ToString(), _properties[TelemetryConstants.Method]);
            Assert.Equal(_request.RequestUri.AbsoluteUri, _properties[TelemetryConstants.Uri]);
            Assert.Equal(((int)expected.StatusCode).ToString(), _properties[TelemetryConstants.StatusCode]);
            Assert.Equal("True", _properties[TelemetryConstants.Success]);
            Assert.Equal(_properties[TelemetryConstants.ContentLength], expected.Content.Headers.ContentLength.Value.ToString());
        }

        [Fact]
        public async Task AllowsNoContentLength()
        {
            // Arrange
            var expected = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new NoLengthStream()),
            };
            _sendAsync = () => Task.FromResult(expected);

            // Act
            var actual = await _httpClient.SendAsync(_request, HttpCompletionOption.ResponseHeadersRead);

            // Assert
            Assert.Same(expected, actual);
            _telemetryService.Verify(x => x.TrackDuration(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Once);

            Assert.Equal(5, _properties.Count);
            Assert.Equal(_request.Method.ToString(), _properties[TelemetryConstants.Method]);
            Assert.Equal(_request.RequestUri.AbsoluteUri, _properties[TelemetryConstants.Uri]);
            Assert.Equal(((int)expected.StatusCode).ToString(), _properties[TelemetryConstants.StatusCode]);
            Assert.Equal("True", _properties[TelemetryConstants.Success]);
            Assert.Equal("0", _properties[TelemetryConstants.ContentLength]);
        }

        private class NoLengthStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => throw new NotSupportedException();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private class FuncHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<Func<Task<HttpResponseMessage>>> _getSendAsync;

            public FuncHttpMessageHandler(Func<Func<Task<HttpResponseMessage>>> getSend)
            {
                _getSendAsync = getSend ?? throw new ArgumentNullException(nameof(getSend));
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Yield();
                var sendAsync = _getSendAsync();
                return await sendAsync();
            }
        }
    }
}

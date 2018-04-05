// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class TelemetryHandlerTests
    {
        private readonly Mock<ITelemetryService> _telemetryService;
        private Func<Task<HttpResponseMessage>> _sendAsync;
        private readonly FuncHttpMessageHandler _innerHandler;
        private readonly HttpRequestMessage _request;
        private readonly TelemetryHandler _target;
        private readonly HttpClient _httpClient;

        public TelemetryHandlerTests()
        {
            _telemetryService = new Mock<ITelemetryService>();
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
            _telemetryService.Verify(
                x => x.TrackHttpHeaderDuration(
                    It.Is<TimeSpan>(ts => ts > TimeSpan.Zero),
                    _request.Method,
                    _request.RequestUri,
                    false,
                    null,
                    null),
                Times.Once);
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
            _telemetryService.Verify(
                x => x.TrackHttpHeaderDuration(
                    It.Is<TimeSpan>(ts => ts > TimeSpan.Zero),
                    _request.Method,
                    _request.RequestUri,
                    false,
                    expected.StatusCode,
                    expected.Content.Headers.ContentLength.Value),
                Times.Once);
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
            _telemetryService.Verify(
                x => x.TrackHttpHeaderDuration(
                    It.Is<TimeSpan>(ts => ts > TimeSpan.Zero),
                    _request.Method,
                    _request.RequestUri,
                    true,
                    expected.StatusCode,
                    expected.Content.Headers.ContentLength.Value),
                Times.Once);
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
            _telemetryService.Verify(
                x => x.TrackHttpHeaderDuration(
                    It.Is<TimeSpan>(ts => ts > TimeSpan.Zero),
                    _request.Method,
                    _request.RequestUri,
                    true,
                    expected.StatusCode,
                    null),
                Times.Once);
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

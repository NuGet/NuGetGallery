// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery.Infrastructure.Search.Correlation;
using Xunit;

namespace NuGetGallery.SearchClient
{
    public class CorrelatingHttpClientHandlerFacts : IDisposable
    {
        [Fact]
        public async Task AddsCorrelationIdToRequestWhenAvailable()
        {
            // Arrange
            var correlatingHttpClientHandler = new CorrelatingHttpClientHandler();
            var testHandler = new TestHandler();
            correlatingHttpClientHandler.InnerHandler = testHandler;

            var correlationId = Guid.NewGuid();
            CallContext.LogicalSetData(WebApiCorrelationHandler.CallContextKey, correlationId);

            // Act
            using (var client = new HttpClient(correlatingHttpClientHandler))
            {
                await client.GetAsync("https://example");
            }

            // Assert
            var request = testHandler.Requests.FirstOrDefault();
            Assert.NotNull(request);
            Assert.True(request.Headers.Contains(WebApiCorrelationHandler.CorrelationIdHttpHeaderName));
            Assert.Equal(correlationId.ToString(), request.Headers.GetValues(WebApiCorrelationHandler.CorrelationIdHttpHeaderName).FirstOrDefault());
        }

        [Fact]
        public async Task DoesNotAddCorrelationIdToRequestWhenNotAvailable()
        {
            // Arrange
            var correlatingHttpClientHandler = new CorrelatingHttpClientHandler();
            var testHandler = new TestHandler();
            correlatingHttpClientHandler.InnerHandler = testHandler;

            // Act
            using (var client = new HttpClient(correlatingHttpClientHandler))
            {
                await client.GetAsync("https://example");
            }

            // Assert
            var request = testHandler.Requests.FirstOrDefault();
            Assert.NotNull(request);
            Assert.False(request.Headers.Contains(WebApiCorrelationHandler.CorrelationIdHttpHeaderName));
        }

        public void Dispose()
        {
            CallContext.FreeNamedDataSlot(WebApiCorrelationHandler.CorrelationIdHttpHeaderName);
        }

        public class TestHandler : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }
    }
}
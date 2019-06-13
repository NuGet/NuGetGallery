// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using NuGetGallery.Infrastructure.Search.Correlation;
using Xunit;

namespace NuGetGallery.SearchClient
{
    public class CorrelatingHttpClientHandlerFacts
    {
        [Fact]
        public async Task AddsCorrelationIdToRequestWhenAvailable()
        {
            // Arrange
            var correlatingHttpClientHandler = new CorrelatingHttpClientHandler();
            correlatingHttpClientHandler.InnerHandler = new HttpClientHandler();

            var inspectingHandler = new RequestInspectingHandler();
            inspectingHandler.InnerHandler = correlatingHttpClientHandler;

            var correlationId = Guid.NewGuid();
            CallContext.LogicalSetData(WebApiCorrelationHandler.CallContextKey, correlationId);

            // Act
            using (var client = new HttpClient(inspectingHandler))
            {
                await client.GetAsync("https://www.nuget.org");
            }

            // Assert
            var request = inspectingHandler.Requests.FirstOrDefault();
            Assert.NotNull(request);
            Assert.True(request.Headers.Contains(WebApiCorrelationHandler.CorrelationIdHttpHeaderName));
            Assert.Equal(correlationId.ToString(), request.Headers.GetValues(WebApiCorrelationHandler.CorrelationIdHttpHeaderName).FirstOrDefault());
        }

        [Fact]
        public async Task DoesNotAddCorrelationIdToRequestWhenNotAvailable()
        {
            // Arrange
            var correlatingHttpClientHandler = new CorrelatingHttpClientHandler();
            correlatingHttpClientHandler.InnerHandler = new HttpClientHandler();

            var inspectingHandler = new RequestInspectingHandler();
            inspectingHandler.InnerHandler = correlatingHttpClientHandler;

            // Act
            using (var client = new HttpClient(inspectingHandler))
            {
                await client.GetAsync("https://www.nuget.org");
            }

            // Assert
            var request = inspectingHandler.Requests.FirstOrDefault();
            Assert.NotNull(request);
            Assert.False(request.Headers.Contains(WebApiCorrelationHandler.CorrelationIdHttpHeaderName));
        }
    }
}
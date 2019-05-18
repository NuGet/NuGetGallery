// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Hosting;
using NuGetGallery.Infrastructure.Search.Correlation;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery.SearchClient
{
    public class WebApiCorrelationHandlerFacts
    {
        private static HttpRequestMessage CreateRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            request.Content = new StringContent(string.Empty);
            request.Properties[HttpPropertyKeys.RequestContextKey] = new HttpRequestContext
            {
                Configuration = new HttpConfiguration()
            };

            return request;
        }

        public WebApiCorrelationHandler CreateWebApiCorrelationHandler()
        {
            var handler = new WebApiCorrelationHandler();
            handler.InnerHandler = new MockHandler();

            return handler;
        }

        [Fact]
        public async Task PropagatesCorrelationIdFromWebApi()
        {
            // Arrange
            var handler = CreateWebApiCorrelationHandler();

            var invoker = new HttpMessageInvoker(handler);

            var request = CreateRequest(HttpMethod.Get, "http://localhost:8888/api");

            var correlationId = request.GetCorrelationId();

            // Act
            var response = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.Contains(WebApiCorrelationHandler.CorrelationIdHttpHeaderName));
            Assert.Equal(correlationId.ToString(),
                response.Headers.GetValues(WebApiCorrelationHandler.CorrelationIdHttpHeaderName).FirstOrDefault());
        }



        [Fact]
        public async Task PropagatesCorrelationIdFromHeader()
        {
            // Arrange
            var handler = CreateWebApiCorrelationHandler();

            var invoker = new HttpMessageInvoker(handler);

            var correlationId = Guid.NewGuid();

            var request = CreateRequest(HttpMethod.Get, "http://localhost:8888/api");
            request.Headers.Add(WebApiCorrelationHandler.CorrelationIdHttpHeaderName, correlationId.ToString("D"));

            // Act
            var response = await invoker.SendAsync(request, CancellationToken.None);
            var requestCorrelationId = request.GetCorrelationId(); // should match our header

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.Contains(WebApiCorrelationHandler.CorrelationIdHttpHeaderName));
            Assert.Equal(correlationId.ToString(),
                response.Headers.GetValues(WebApiCorrelationHandler.CorrelationIdHttpHeaderName).FirstOrDefault());
            Assert.Equal(correlationId.ToString(), requestCorrelationId.ToString());
        }
    }
}
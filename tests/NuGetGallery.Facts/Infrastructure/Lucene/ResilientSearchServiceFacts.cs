// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGetGallery;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Search;
using NuGetGallery.Services.Telemetry;
using Moq;
using Xunit;

namespace NuGet.Services.Search.Client
{
    public class ResilientSearchServiceFacts
    {
        public class TheGetAsyncMethod
        {
            [Fact]
            public async Task GetAsyncReturnsNotAvailableWhenServicesAreNotAvailable()
            {
                // Arrange
                string path = "query";
                string queryString = "queryString";
                string baseAddress1 = "https://foo222.com";
                string baseAddress2 = "https://foo222.com";
                Uri u1 = new Uri($"{baseAddress1}/{path}?{queryString}");
                Uri u2 = new Uri($"{baseAddress2}/{path}?{queryString}");
                Mock<IHttpClientWrapper> mockISearchHttpClient1;
                Mock<IHttpClientWrapper> mockISearchHttpClient2;
                var resilientTestClient = GetResilientSearchClient(baseAddress1, baseAddress2,
                    GetResponseMessage(u1, HttpStatusCode.BadRequest), GetResponseMessage(u2, HttpStatusCode.BadRequest),
                    out mockISearchHttpClient1, out mockISearchHttpClient2);

                // Act
                var result = await resilientTestClient.GetAsync(path, queryString);

                // Assert
                Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
                Assert.Equal(HttpMethod.Get, result.RequestMessage.Method);
                string contentAsString = await result.Content.ReadAsStringAsync();
                Assert.Equal("{\r\n  \"data\": {\r\n    \"message\": \"Search Service is not available. Please try again later.\"\r\n  }\r\n}", contentAsString);
                mockISearchHttpClient1.Verify(x => x.GetAsync(u1), Times.Once);
                mockISearchHttpClient2.Verify(x => x.GetAsync(u2), Times.Once);
            }

            [Fact]
            public async Task GetAsyncReturnsFirstClientResponseIfAvailable()
            {
                // Arrange
                string path = "query";
                string queryString = "queryString";
                string baseAddress1 = "https://foo111.com";
                string baseAddress2 = "https://foo222.com";
                Uri u1 = new Uri($"{baseAddress1}/{path}?{queryString}");
                Uri u2 = new Uri($"{baseAddress2}/{path}?{queryString}");
                Mock<IHttpClientWrapper> mockISearchHttpClient1;
                Mock<IHttpClientWrapper> mockISearchHttpClient2;
                var response1 = GetResponseMessage(u1, HttpStatusCode.OK);
                var response2 = GetResponseMessage(u2, HttpStatusCode.OK);
                var resilientTestClient = GetResilientSearchClient(baseAddress1, baseAddress2, response1, response2, out mockISearchHttpClient1, out mockISearchHttpClient2);

                // Act
                var result = await resilientTestClient.GetAsync(path, queryString);

                // Assert
                Assert.Equal(response1, result);
                mockISearchHttpClient1.Verify(x => x.GetAsync(u1), Times.Once);
                mockISearchHttpClient2.Verify(x => x.GetAsync(u2), Times.Never);
            }

            [Fact]
            public async Task GetAsyncReturnsSecondClientResponseIfFirstIsNotAvailable()
            {
                // Arrange
                string path = "query";
                string queryString = "queryString";
                string baseAddress1 = "https://foo111.com";
                string baseAddress2 = "https://foo222.com";
                Uri u1 = new Uri($"{baseAddress1}/{path}?{queryString}");
                Uri u2 = new Uri($"{baseAddress2}/{path}?{queryString}");
                Mock<IHttpClientWrapper> mockISearchHttpClient1;
                Mock<IHttpClientWrapper> mockISearchHttpClient2;
                var response1 = GetResponseMessage(u1, HttpStatusCode.ServiceUnavailable);
                var response2 = GetResponseMessage(u2, HttpStatusCode.OK);
                var resilientTestClient = GetResilientSearchClient(baseAddress1, baseAddress2, response1, response2, out mockISearchHttpClient1, out mockISearchHttpClient2);

                // Act
                var result = await resilientTestClient.GetAsync(path, queryString);

                // Assert
                Assert.Equal(response2, result);
                mockISearchHttpClient1.Verify(x => x.GetAsync(u1), Times.Once);
                mockISearchHttpClient2.Verify(x => x.GetAsync(u2), Times.Once);
            }
        }

        private static ILogger<ResilientSearchHttpClient> GetLogger()
        {
            var mockConfiguration = new Mock<ILogger<ResilientSearchHttpClient>>();
            return mockConfiguration.Object;
        }

        private static IResilientSearchClient GetResilientSearchClient(string primaryBaseAddress,
            string secondaryBaseAddress,
            HttpResponseMessage getAsyncResultMessage1,
            HttpResponseMessage getAsyncResultMessage2,
            out Mock<IHttpClientWrapper> mockISearchHttpClient1,
            out Mock<IHttpClientWrapper> mockISearchHttpClient2)
        {
            var mockTelemetryService = new Mock<ITelemetryService>();
            mockISearchHttpClient1 = new Mock<IHttpClientWrapper>();
            mockISearchHttpClient1.SetupGet(x => x.BaseAddress).Returns(new Uri(primaryBaseAddress));
            mockISearchHttpClient1.Setup(x => x.GetAsync(It.IsAny<Uri>())).ReturnsAsync(getAsyncResultMessage1);

            mockISearchHttpClient2 = new Mock<IHttpClientWrapper>();
            mockISearchHttpClient2.SetupGet(x => x.BaseAddress).Returns(new Uri(secondaryBaseAddress));
            mockISearchHttpClient2.Setup(x => x.GetAsync(It.IsAny<Uri>())).ReturnsAsync(getAsyncResultMessage2);

            List<IHttpClientWrapper> clients = new List<IHttpClientWrapper>() { mockISearchHttpClient1.Object, mockISearchHttpClient2.Object };
            return new ResilientSearchHttpClient(clients, GetLogger(), mockTelemetryService.Object);
        }

        private static HttpResponseMessage GetResponseMessage(Uri uri, HttpStatusCode statusCode)
        {
            string path = uri.AbsolutePath;
            string queryString = uri.Query;

            var content = new JObject(
                           new JProperty("queryString", queryString),
                           new JProperty("path", path));

            return new HttpResponseMessage()
            {
                Content = new StringContent(content.ToString(), Encoding.UTF8, CoreConstants.TextContentType),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, $"{path}/{queryString}"),
                StatusCode = statusCode
            };
        }
    }
}

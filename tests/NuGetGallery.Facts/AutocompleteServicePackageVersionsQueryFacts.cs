// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Search;
using Moq;
using Xunit;
using System.Web;
using System.Threading;
using System.Net;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public class AutocompleteServicePackageVersionsQueryFacts
    {
        private TestHandler _testHandler;

        private IAppConfiguration GetConfiguration()
        {
            var mockConfiguration = new Mock<IAppConfiguration>();
            return mockConfiguration.Object;
        }

        private ILogger<ResilientSearchHttpClient> GetLogger()
        {
            var mockConfiguration = new Mock<ILogger<ResilientSearchHttpClient>>();
            return mockConfiguration.Object;
        }

        private IResilientSearchClient GetResilientSearchClient()
        {
            _testHandler = new TestHandler();

            var mockTelemetryService = new Mock<ITelemetryService>();
            List<IHttpClientWrapper> clients = new List<IHttpClientWrapper>();
            clients.Add(new HttpClientWrapper(new HttpClient(_testHandler)
            {
                BaseAddress = new Uri("https://example")
            }));
            return new ResilientSearchHttpClient(clients, GetLogger(), mockTelemetryService.Object);
        }

        [Fact]
        public async Task ExecuteThrowsForEmptyId()
        {
            var query = new AutocompleteServicePackageVersionsQuery(GetConfiguration(), GetResilientSearchClient());
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await query.Execute(string.Empty, false));
            Assert.Empty(_testHandler.Requests);
        }

        [Fact]
        public async Task ExecuteReturnsResultsForSpecificQuery()
        {
            var query = new AutocompleteServicePackageVersionsQuery(GetConfiguration(), GetResilientSearchClient());
            var result = await query.Execute("newtonsoft.json", false);
            Assert.True(result.Any());
            var request = Assert.Single(_testHandler.Requests);
            Assert.Equal("https://example/autocomplete?id=newtonsoft.json&prerelease=False", request.RequestUri.AbsoluteUri);
        }

        [Theory]
        [InlineData(true, null, "?id=Newtonsoft.Json&prerelease=True")]
        [InlineData(true, "2.0.0", "?id=Newtonsoft.Json&prerelease=True&semVerLevel=2.0.0")]
        [InlineData(false, null, "?id=Newtonsoft.Json&prerelease=False")]
        [InlineData(false, "2.0.0", "?id=Newtonsoft.Json&prerelease=False&semVerLevel=2.0.0")]
        public void PackageVersionsQueryBuildsCorrectQueryString(bool includePrerelease, string semVerLevel, string expectedQueryString)
        {
            // Arrange
            var query = new AutocompleteServicePackageVersionsQuery(GetConfiguration(), GetResilientSearchClient());

            // Act
            var actualQueryString = query.BuildQueryString("id=Newtonsoft.Json", includePrerelease, semVerLevel);

            // Assert
            Assert.Equal(expectedQueryString, actualQueryString);
        }

        private class TestHandler : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);

                var queryString = HttpUtility.ParseQueryString(request.RequestUri.Query);

                var id = queryString["id"] ?? string.Empty;
                var take = 10;

                var data = Enumerable
                    .Range(0, 10)
                    .Select(x => $"1.0.{x}")
                    .ToList();

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StringContent(
                        JsonConvert.SerializeObject(new { totalHits = take, data }),
                        System.Text.Encoding.UTF8,
                        "application/json"),
                });
            }
        }
    }
}
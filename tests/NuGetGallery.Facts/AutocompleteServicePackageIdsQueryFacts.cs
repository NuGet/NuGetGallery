// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Search;
using Xunit;

namespace NuGetGallery
{
    public class AutocompleteServicePackageIdsQueryFacts
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
        public async Task ExecuteReturns30ResultsForEmptyQuery()
        {
            var query = new AutocompleteServicePackageIdsQuery(GetConfiguration(), GetResilientSearchClient());
            var result = await query.Execute("", false);

            Assert.True(result.Count() == 30);
            var request = Assert.Single(_testHandler.Requests);
            Assert.Equal("https://example/autocomplete?take=30&q=&prerelease=False", request.RequestUri.AbsoluteUri);
        }

        [Fact]
        public async Task ExecuteReturns30ResultsForNullQuery()
        {
            var query = new AutocompleteServicePackageIdsQuery(GetConfiguration(), GetResilientSearchClient());
            var result = await query.Execute(null, false);
            Assert.True(result.Count() == 30);
            var request = Assert.Single(_testHandler.Requests);
            Assert.Equal("https://example/autocomplete?take=30&q=&prerelease=False", request.RequestUri.AbsoluteUri);
        }

        [Fact]
        public async Task ExecuteReturnsResultsForSpecificQuery()
        {
            var query = new AutocompleteServicePackageIdsQuery(GetConfiguration(), GetResilientSearchClient());
            var result = await query.Execute("jquery", false);
            Assert.Contains("jquery", result, StringComparer.OrdinalIgnoreCase);
            var request = Assert.Single(_testHandler.Requests);
            Assert.Equal("https://example/autocomplete?take=30&q=jquery&prerelease=False", request.RequestUri.AbsoluteUri);
        }

        [Theory]
        [InlineData(true, null, "?take=30&q=Json&prerelease=True")]
        [InlineData(true, "2.0.0", "?take=30&q=Json&prerelease=True&semVerLevel=2.0.0")]
        [InlineData(false, null, "?take=30&q=Json&prerelease=False")]
        [InlineData(false, "2.0.0", "?take=30&q=Json&prerelease=False&semVerLevel=2.0.0")]
        public void PackageIdQueryBuildsCorrectQueryString(bool includePrerelease, string semVerLevel, string expectedQueryString)
        {
            // Arrange
            var query = new AutocompleteServicePackageIdsQuery(GetConfiguration(), GetResilientSearchClient());

            // Act
            var actualQueryString = query.BuildQueryString("take=30&q=Json", includePrerelease, semVerLevel);

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

                var q = queryString["q"] ?? string.Empty;

                if (!int.TryParse(queryString["take"], out var take))
                {
                    take = 20;
                }

                var data = Enumerable
                    .Range(0, take)
                    .Select(x => $"{q}{(x == 0 ? string.Empty : $"-{x}")}")
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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.Configuration;
using GitHubVulnerabilities2Db.GraphQL;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GitHubVulnerabilities2Db.Facts
{
    public class QueryServiceFacts
    {
        private readonly GitHubVulnerabilities2DbConfiguration _configuration;
        private readonly QueryServiceHttpClientHandler _handler;
        private readonly QueryService _service;

        public QueryServiceFacts()
        {
            _configuration = new GitHubVulnerabilities2DbConfiguration();
            _handler = new QueryServiceHttpClientHandler();
            _service = new QueryService(
                _configuration, new HttpClient(_handler));
        }

        [Fact]
        public async Task Success()
        {
            // Arrange
            var query = "someString";
            _handler.ExpectedQueryContent = (new JObject { ["query"] = query }).ToString();

            _handler.ExpectedEndpoint = new Uri("https://graphQL.net");
            _configuration.GitHubGraphQLQueryEndpoint = _handler.ExpectedEndpoint;

            _handler.ExpectedApiKey = "patpatpat";
            _configuration.GitHubPersonalAccessToken = _handler.ExpectedApiKey;

            var response = new QueryResponse();
            _handler.ResponseMessage = new HttpResponseMessage
            {
                Content = new StringContent(JsonConvert.SerializeObject(response))
            };

            // Act
            await _service.QueryAsync(query, new CancellationToken());

            // Assert
            Assert.True(_handler.WasCalled);
        }

        private class QueryServiceHttpClientHandler : HttpClientHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                WasCalled = true;

                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(ExpectedEndpoint, request.RequestUri);
                Assert.Equal(ExpectedQueryContent, request.Content.ReadAsStringAsync().Result);
                Assert.Equal(
                    new AuthenticationHeaderValue("Bearer", ExpectedApiKey),
                    request.Headers.Authorization);

                Assert.Contains(ProductInfoHeaderValue.Parse(QueryService.UserAgent), request.Headers.UserAgent);
                return Task.FromResult(ResponseMessage);
            }

            public Uri ExpectedEndpoint { get; set; }
            public string ExpectedQueryContent { get; set; }
            public string ExpectedApiKey { get; set; }
            public HttpResponseMessage ResponseMessage { get; set; }

            public bool WasCalled { get; private set; }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using BasicSearchTests.FunctionalTests.Core.Models;
using BasicSearchTests.FunctionalTests.Core.TestSupport;
using Xunit;
using System;
using System.Net;

namespace BasicSearchTests.FunctionalTests.Core
{
    public class V2SearchFunctionalTests
    {
        private HttpClient _client;

        public V2SearchFunctionalTests()
        {
            // Arrange
            _client = new HttpClient(new RetryHandler(new HttpClientHandler())) { BaseAddress = new Uri(EnvironmentSettings.SearchServiceBaseUrl) };
        }

        [Fact]
        public async Task CanGetEmptyResult()
        {
            // Act
            var response = await _client.GetAsync(new V2SearchBuilder { Query = Constants.NonExistentSearchString }.RequestUri);
            var result = await response.Content.ReadAsAsync<V2SearchResult>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, result.TotalHits);
            Assert.Empty(result.Data);
        }

        [Fact]
        public async Task ShouldGetResultsForEmptyString()
        {
            // Act
            var response = await _client.GetAsync(new V2SearchBuilder().RequestUri);
            var result = await response.Content.ReadAsAsync<V2SearchResult>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(result.TotalHits.HasValue && result.TotalHits.Value > 0, "No results found, should find atleast some results for empty string query.");
            Assert.NotNull(result.Data);
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using BasicSearchTests.FunctionalTests.Core.Models;
using BasicSearchTests.FunctionalTests.Core.TestSupport;
using Xunit;
using System.Net;

namespace BasicSearchTests.FunctionalTests.Core
{
    public class V3SearchFunctionalTests : BaseFunctionalTests
    {
        [Fact]
        public async Task CanGetEmptyResult()
        {
            // Act
            var response = await Client.GetAsync(new V3SearchBuilder { Query = Constants.NonExistentSearchString }.RequestUri);
            var result = await response.Content.ReadAsAsync<V3SearchResult>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, result.TotalHits);
            Assert.Empty(result.Data);
        }

        [Fact]
        public async Task ShouldGetResultsForEmptyString()
        {
            // Act
            var response = await Client.GetAsync(new V3SearchBuilder().RequestUri);
            var result = await response.Content.ReadAsAsync<V3SearchResult>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(result.TotalHits.HasValue && result.TotalHits.Value > 0, "No results found, should find at least some results for empty string query.");
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task EmptyQueryResultHasDownloads()
        {
            // Act
            var response = await Client.GetAsync(new V3SearchBuilder().RequestUri);
            var result = await response.Content.ReadAsAsync<V3SearchResult>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(result.TotalHits.HasValue && result.TotalHits.Value > 0, "No results found, should find at least some results for empty string query.");
            Assert.NotNull(result.Data);

            var topResult = result.Data[0];

            var downloads = topResult.TotalDownloads;
            Assert.True(downloads > 0);
        }
    }
}
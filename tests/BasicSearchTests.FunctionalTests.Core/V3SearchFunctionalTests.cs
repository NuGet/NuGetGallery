// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using BasicSearchTests.FunctionalTests.Core.Models;
using BasicSearchTests.FunctionalTests.Core.TestSupport;
using Xunit;
using System.Net;
using Newtonsoft.Json;
using System;
using System.Linq;

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

        [Fact]
        public void ShouldThrowExceptionForInvalidSearchResults()
        {
            var content = @"{
                '@context': {
                    '@vocab': 'http://schema.nuget.org/schema#',
                    '@base': 'https://api.nuget.org/v3/registration1/'
                },
                'totalHits': 1,
                'lastReopen': '2000-01-23T01:23:45.0123456Z',
                'index': 'v3-lucene1-v2v3-20170110',
                'data': [
                    {
                    }
                ]
            }";

            // Assert that a JsonSerializationException is thrown if the search results
            // are missing required fields.
            Assert.Throws<JsonSerializationException>(() =>
            {
                JsonConvert.DeserializeObject<V3SearchResult>(content);
            });
        }

        [Fact]
        public async Task EmptyQueryResultHaveRequiredProperties()
        {
            // Act
            var response = await Client.GetAsync(new V3SearchBuilder().RequestUri);
            var content = await response.Content.ReadAsStringAsync();

            // Assert that all required properties are present in the search result by
            // deserializing the response. An exception will be thrown if required fields
            // are missing.
            Assert.Null(Record.Exception(() =>
            {
                JsonConvert.DeserializeObject<V3SearchResult>(content);
            }));
        }

        [Fact]
        public async Task EnsureTestPackageHasSearchResults()
        {
            // Act
            var response = await Client.GetAsync(new V3SearchBuilder() { Query = Constants.TestPackageId }.RequestUri);
            var result = await response.Content.ReadAsAsync<V3SearchResult>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(result.TotalHits.HasValue);
            Assert.True(result.TotalHits.Value > 0, $"Could not find test package {Constants.TestPackageId}");
        }

        [Fact]
        public async Task EnsureTestPackageIsValid()
        {
            // Act
            var response = await Client.GetAsync(new V3SearchBuilder() { Query = Constants.TestPackageId }.RequestUri);
            var result = await response.Content.ReadAsAsync<V3SearchResult>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.True(result.TotalHits.HasValue);
            Assert.True(result.TotalHits.Value > 0, $"Could not find test package {Constants.TestPackageId}");
            Assert.True(result.Data.Count > 0, $"Could not find test package {Constants.TestPackageId}");

            Assert.NotNull(result.AtContext);
            Assert.True(result.AtContext.AtVocab == "http://schema.nuget.org/schema#");
            Assert.False(string.IsNullOrEmpty(result.AtContext.AtBase));
            Assert.True(result.LastReopen.HasValue);
            Assert.True(result.LastReopen.Value != default(DateTime));
            Assert.False(string.IsNullOrEmpty(result.Index));

            // Assert that the package result whose Id is "BaseTestPackage" with Version "1.0.0"
            // matches exactly what is expected.
            var package = result.Data
                .Where(p => p.Id == Constants.TestPackageId)
                .Where(p => p.Version == Constants.TestPackageVersion)
                .FirstOrDefault();

            Assert.NotNull(package);
            Assert.False(string.IsNullOrEmpty(package.AtId));
            Assert.True(package.AtType == "Package");
            Assert.False(string.IsNullOrEmpty(package.Registration));
            Assert.True(package.Description == Constants.TestPackageDescription);
            Assert.True(package.Summary == Constants.TestPackageSummary);
            Assert.True(package.Title == Constants.TestPackageTitle);
            Assert.True(package.Tags.Count() == 2);
            Assert.True(package.Tags[0] == "Tag1");
            Assert.True(package.Tags[1] == "Tag2");
            Assert.True(package.Authors.Count() == 1);
            Assert.True(package.Authors[0] == Constants.TestPackageAuthor);
            Assert.True(package.TotalDownloads != default(long));
            Assert.True(package.Versions.Count() == 1);
            Assert.True(package.Versions[0].Version == "1.0.0");
            Assert.True(package.Versions[0].Downloads != default(long));
            Assert.False(string.IsNullOrEmpty(package.Versions[0].AtId));
        }

        [Fact]
        public async Task TestPackageResultsHaveRequiredProperties()
        {
            // Act - Newtonsoft's Json deserialization will throw exceptions if required
            // properties are missing.
            var response = await Client.GetAsync(new V3SearchBuilder { Query = Constants.TestPackageId }.RequestUri);
            var content = await response.Content.ReadAsStringAsync();

            // Assert that all required properties are present in the search result by
            // deserializing the response. An exception will be thrown if required fields
            // are missing.
            Assert.Null(Record.Exception(() =>
            {
                var result = JsonConvert.DeserializeObject<V3SearchResult>(content);

                Assert.True(result.Data.Count > 0, $"No hits for query '{Constants.TestPackageId}'");
            }));
        }
    }
}
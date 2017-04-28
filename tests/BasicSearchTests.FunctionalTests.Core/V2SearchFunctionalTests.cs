// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using BasicSearchTests.FunctionalTests.Core.Models;
using BasicSearchTests.FunctionalTests.Core.TestSupport;
using Xunit;
using System.Net;
using Newtonsoft.Json;
using System.Linq;
using System;

namespace BasicSearchTests.FunctionalTests.Core
{
    public class V2SearchFunctionalTests : BaseFunctionalTests
    {
        [Fact]
        public async Task CanGetEmptyResult()
        {
            // Act
            var response = await Client.GetAsync(new V2SearchBuilder { Query = Constants.NonExistentSearchString }.RequestUri);
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
            var response = await Client.GetAsync(new V2SearchBuilder().RequestUri);
            var result = await response.Content.ReadAsAsync<V2SearchResult>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(result.TotalHits.HasValue && result.TotalHits.Value > 0, "No results found, should find atleast some results for empty string query.");
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void ShouldThrowExceptionForInvalidSearchResults()
        {
            var content = @"{
                'totalHits': 1,
                'index': 'C:\\NuGetSearchData\\Lucene-v2v3',
                'indexTimestamp': '1/23/2000 1:23:45 AM',
                'data': [
                    {
                    }
                ]
            }";

            // Assert that a JsonSerializationException is thrown if the search results
            // are missing required fields.
            Assert.Throws<JsonSerializationException>(() =>
            {
                JsonConvert.DeserializeObject<V2SearchResult>(content);
            });
        }

        [Fact]
        public async Task EmptyQueryResultHaveRequiredProperties()
        {
            // Act
            var response = await Client.GetAsync(new V2SearchBuilder().RequestUri);
            var content = await response.Content.ReadAsStringAsync();

            // Assert that all required properties are present in the search result by
            // deserializing the response. An exception will be thrown if required fields
            // are missing.
            Assert.Null(Record.Exception(() =>
            {
                JsonConvert.DeserializeObject<V2SearchResult>(content);
            }));
        }

        [Fact]
        public async Task EnsureTestPackageIsValid()
        {
            // Act
            var response = await Client.GetAsync(new V2SearchBuilder() { Query = Constants.TestPackageId }.RequestUri);
            var result = await response.Content.ReadAsAsync<V2SearchResult>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.True(result.TotalHits.HasValue);
            Assert.True(result.TotalHits.Value > 0, $"Could not find test package {Constants.TestPackageId}");
            Assert.True(result.Data.Count > 0, $"Could not find test package {Constants.TestPackageId}");

            Assert.False(string.IsNullOrEmpty(result.Index));
            Assert.True(result.IndexTimestamp.HasValue);
            Assert.True(result.IndexTimestamp.Value != default(DateTime));

            // Assert that the package result whose Id is "BaseTestPackage" with Version "1.0.0"
            // matches exactly what is expected.
            var package = result.Data
                .Where(p => p.PackageRegistration.Id == Constants.TestPackageId)
                .Where(p => p.Version == Constants.TestPackageVersion)
                .FirstOrDefault();

            Assert.NotNull(package);
            Assert.True(package.PackageRegistration.DownloadCount != default(long));
            Assert.True(package.PackageRegistration.Owners.Count == 1);
            Assert.False(string.IsNullOrEmpty(package.PackageRegistration.Owners[0]));
            Assert.True(package.NormalizedVersion == "1.0.0");
            Assert.True(package.Title == Constants.TestPackageTitle);
            Assert.True(package.Description == Constants.TestPackageDescription);
            Assert.True(package.Summary == Constants.TestPackageSummary);
            Assert.True(package.Authors == Constants.TestPackageAuthor);
            Assert.True(package.Copyright == Constants.TestPackageCopyright);
            Assert.True(package.Tags == "Tag1 Tag2");
            Assert.True(package.ReleaseNotes == "Summary of changes made in this release of the package.");
            Assert.True(package.IsLatestStable);
            Assert.True(package.IsLatest);
            Assert.True(package.Listed);
            Assert.True(package.Created != default(DateTime));
            Assert.True(package.Published != default(DateTime));
            Assert.True(package.LastUpdated != default(DateTime));
            Assert.True(package.DownloadCount != default(long));
            Assert.True(package.FlattenedDependencies == "");
            Assert.True(package.Dependencies.Count() == 0);
            Assert.True(package.SupportedFrameworks.Count() == 0);
            Assert.True(package.Hash == "5KJqge5+IYZkmba5C/pRVwjqwwaF1YM28xs6AiWMoxfxE/dzFVXJ5QGR7Rx2JmKWPLwz0R3eO+jWjd4lRX1WxA==");
            Assert.True(package.HashAlgorithm == "SHA512");
            Assert.True(package.PackageFileSize == 3943);
            Assert.True(package.RequireslicenseAcceptance == false);
        }

        [Fact]
        public async Task TestPackageResultsHaveRequiredProperties()
        {
            // Act.
            var response = await Client.GetAsync(new V2SearchBuilder { Query = Constants.TestPackageId }.RequestUri);
            var content = await response.Content.ReadAsStringAsync();

            // Assert that all required properties are present in the search result by
            // deserializing the response. An exception will be thrown if required fields
            // are missing.
            Assert.Null(Record.Exception(() =>
            {
                var result = JsonConvert.DeserializeObject<V2SearchResult>(content);

                Assert.True(result.Data.Count > 0, $"No hits for query '{Constants.TestPackageId}'");
            }));
        }
    }
}
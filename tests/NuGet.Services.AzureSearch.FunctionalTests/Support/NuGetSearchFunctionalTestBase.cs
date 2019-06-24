// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using BasicSearchTests.FunctionalTests.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    /// <summary>
    /// Base for functional tests on the NuGet search service.
    /// </summary>
    public class NuGetSearchFunctionalTestBase : BaseFunctionalTests, IClassFixture<CommonFixture>
    {
        private ITestOutputHelper _testOutputHelper;

        public NuGetSearchFunctionalTestBase(CommonFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture.AzureSearchConfiguration.AzureSearchAppServiceUrl)
        {
            Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
            _testOutputHelper.WriteLine($"Running tests against: {fixture.AzureSearchConfiguration.AzureSearchAppServiceUrl}");
        }

        protected CommonFixture Fixture { get; private set; }

        /// <summary>
        /// Queries the NuGet Search API.
        /// See: https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#search-for-packages
        /// </summary>
        /// <param name="query">The search terms to filter packages.</param>
        /// <param name="includePrerelease">Whether prerelease results should be included.</param>
        /// <param name="includeSemVer2">Whether semver2 results should be included.</param>
        /// <returns>The package ids' that matches the query, lowercased.</returns>
        protected async Task<IReadOnlyList<string>> SearchAsync(string query, bool includePrerelease = true, bool includeSemVer2 = true)
        {
            var requestUri = $"/query?q={HttpUtility.UrlEncode(query)}";

            if (includePrerelease)
            {
                requestUri += "&prerelease=true";
            }

            if (includeSemVer2)
            {
                requestUri += "&semVerLevel=2.0.0";
            }

            using (var response = await Client.GetAsync(requestUri))
            {
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<SearchResponse>(json);

                return result.Data.Select(t => t.Id.ToLowerInvariant()).ToList();
            }
        }

        /// <summary>
        /// NuGet Search API response
        /// See: https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#response
        /// </summary>
        private class SearchResponse
        {
            public IReadOnlyList<SearchResult> Data { get; set; }
        }

        /// <summary>
        /// An entry in the search response.
        /// See: https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#search-result
        /// </summary>
        private class SearchResult
        {
            public string Id { get; set; }
        }
    }
}

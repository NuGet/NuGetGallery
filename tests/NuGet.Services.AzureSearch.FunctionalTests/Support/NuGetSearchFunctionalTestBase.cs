// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using BasicSearchTests.FunctionalTests.Core;
using BasicSearchTests.FunctionalTests.Core.Models;
using BasicSearchTests.FunctionalTests.Core.TestSupport;
using Newtonsoft.Json;
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
        protected async Task<IReadOnlyList<string>> SearchAsync(
            string query,
            int? skip = 0,
            int? take = 20,
            bool includePrerelease = true,
            bool includeSemVer2 = true)
        {
            var results = await V3SearchAsync(new V3SearchBuilder()
                {
                    Query = HttpUtility.UrlEncode(query),
                    Skip = skip,
                    Take = take,
                    Prerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2
                });

            return results.Data.Select(t => t.Id.ToLowerInvariant()).ToList();
        }

        protected async Task<V2SearchResult> V2SearchAsync(V2SearchBuilder searchBuilder)
        {
            return await SearchAsync<V2SearchResult>(searchBuilder);
        }

        protected async Task<V3SearchResult> V3SearchAsync(V3SearchBuilder searchBuilder)
        {
            return await SearchAsync<V3SearchResult>(searchBuilder);
        }

        private async Task<T> SearchAsync<T>(QueryBuilder searchBuilder) where T: SearchResult
        {
            var queryUrl = searchBuilder.RequestUri;
            _testOutputHelper.WriteLine($"Fetching: {queryUrl}");
            using (var response = await Client.GetAsync(queryUrl))
            {
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<T>(json);

                return result;
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Infrastructure.Search.Models;
using Newtonsoft.Json.Linq;
using Xunit;
using System.Linq;
using System.Web;

namespace NuGetGallery.Infrastructure.Search
{
    public class GallerySearchServiceFacts
    {
        [Fact]
        public void CtorNullArgException()
        {
            // Arrange + Act + Assert
            Assert.Throws<ArgumentNullException>(() => new GallerySearchClient(null));
        }

        [Fact]
        public async Task GetDiagnosticsUsesTheCorrectPath()
        {
            // Arrange 
            var gallerySearchClient = new GallerySearchClient(ResilientClientForTest.GetTestInstance(HttpStatusCode.OK));

            // Act
            var response = await gallerySearchClient.GetDiagnostics();

            var httpResponseContentAsString = await response.HttpResponse.Content.ReadAsStringAsync();
            var path = JObject.Parse(httpResponseContentAsString)["path"].Value<string>();
            var queryString = JObject.Parse(httpResponseContentAsString)["queryString"].Value<string>();
            var statusCode = response.StatusCode;

            // Assert
            Assert.Equal(200, (int)statusCode);
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal("search/diag", path);
            Assert.Null(queryString);
        }

        [Theory]
        [MemberData(nameof(AllSortOrders))]
        public async Task MapsAllSortOrders(SortOrder sortOrder)
        {
            // Arrange 
            var gallerySearchClient = new GallerySearchClient(ResilientClientForTest.GetTestInstance(HttpStatusCode.OK));

            // Act
            var response = await gallerySearchClient.Search(query: string.Empty, sortBy: sortOrder);

            // Assert
            var httpResponseContentAsString = await response.HttpResponse.Content.ReadAsStringAsync();
            var queryString = JObject.Parse(httpResponseContentAsString)["queryString"].Value<string>();
            var parsedQueryString = HttpUtility.ParseQueryString(queryString);
            Assert.Contains(sortOrder, SortNames.Keys);
            Assert.Equal(SortNames[sortOrder], parsedQueryString["sortBy"]);
        }

        [Theory]
        [InlineData(null, null, false, "", "", true, "all", "dependency", SortOrder.Relevance, 1, 10, false, false, false, false, null, null, 
            "q=&skip=1&take=10&includeComputedFrameworks=true&frameworkFilterMode=all&packageType=dependency&luceneQuery=false&sortBy=relevance")]
        [InlineData("query", "projectTypeFilter", true, null, null, true, "all", "dotnettool" ,SortOrder.LastEdited, 1, 10, true, true, true, true, "supportedFramework", "semVerLevel",
            "q=query&skip=1&take=10&includeComputedFrameworks=true&frameworkFilterMode=all&packageType=dotnettool&semVerLevel=semVerLevel&supportedFramework=supportedFramework&projectType=projectTypeFilter&prerelease=true&explanation=true&ignoreFilter=true&countOnly=true&sortBy=lastEdited")]
        [InlineData("query", "projectTypeFilter", true, "net", "netstandard2.1", true, "all", "template", SortOrder.Published, 1, 10, true, true, true, true, "supportedFramework", "semVerLevel",
            "q=query&skip=1&take=10&frameworks=net&tfms=netstandard2.1&includeComputedFrameworks=true&frameworkFilterMode=all&packageType=template&semVerLevel=semVerLevel&supportedFramework=supportedFramework&projectType=projectTypeFilter&prerelease=true&explanation=true&ignoreFilter=true&countOnly=true&sortBy=published")]
        [InlineData("query", "projectTypeFilter", true, "", "net472", true, "all", "dependency", SortOrder.TitleAscending, 1, 10, true, true, true, true, "supportedFramework", "semVerLevel",
            "q=query&skip=1&take=10&tfms=net472&includeComputedFrameworks=true&frameworkFilterMode=all&packageType=dependency&semVerLevel=semVerLevel&supportedFramework=supportedFramework&projectType=projectTypeFilter&prerelease=true&explanation=true&ignoreFilter=true&countOnly=true&sortBy=title-asc")]
        [InlineData("query", "projectTypeFilter", true, "netstandard,netframework,", "netcoreapp3.1,", true, "all", "dependency", SortOrder.TitleDescending, 1, 10, true, true, true, true, "supportedFramework", "semVerLevel",
            "q=query&skip=1&take=10&frameworks=netstandard%2Cnetframework%2C&tfms=netcoreapp3.1%2C&includeComputedFrameworks=true&frameworkFilterMode=all&packageType=dependency&semVerLevel=semVerLevel&supportedFramework=supportedFramework&projectType=projectTypeFilter&prerelease=true&explanation=true&ignoreFilter=true&countOnly=true&sortBy=title-desc")]
        [InlineData("query", "projectTypeFilter", true, "netcoreapp", "net481,net5.0", true, "any", null, SortOrder.TitleDescending, 1, 10, true, true, true, true, "supportedFramework", "semVerLevel",
            "q=query&skip=1&take=10&frameworks=netcoreapp&tfms=net481%2Cnet5.0&includeComputedFrameworks=true&frameworkFilterMode=any&semVerLevel=semVerLevel&supportedFramework=supportedFramework&projectType=projectTypeFilter&prerelease=true&explanation=true&ignoreFilter=true&countOnly=true&sortBy=title-desc")]
        [InlineData("query", "projectTypeFilter", true, "", "netstandard17.9", false, "all", "", SortOrder.TitleDescending, 1, 10, true, true, true, true, "supportedFramework", "semVerLevel",
            "q=query&skip=1&take=10&tfms=netstandard17.9&includeComputedFrameworks=false&frameworkFilterMode=all&semVerLevel=semVerLevel&supportedFramework=supportedFramework&projectType=projectTypeFilter&prerelease=true&explanation=true&ignoreFilter=true&countOnly=true&sortBy=title-desc")]
        public async Task SearchArgumentsAreCorrectSet(string query,
            string projectTypeFilter,
            bool includePrerelease,
            string frameworks,
            string tfms,
            bool includeComputedFrameworks,
            string frameworkFilterMode,
            string packageType,
            SortOrder sortBy,
            int skip,
            int take,
            bool isLuceneQuery,
            bool countOnly ,
            bool explain,
            bool getAllVersions,
            string supportedFramework,
            string semVerLevel,
            string expectedResult)
        {
            // Arrange 
            var gallerySearchClient = new GallerySearchClient(ResilientClientForTest.GetTestInstance(HttpStatusCode.OK));

            // Act
            var response = await gallerySearchClient.Search(query,
                projectTypeFilter,
                includePrerelease,
                frameworks,
                tfms,
                includeComputedFrameworks,
                frameworkFilterMode,
                packageType,
                sortBy,
                skip,
                take,
                isLuceneQuery,
                countOnly,
                explain,
                getAllVersions,
                supportedFramework,
                semVerLevel);

            var httpResponseContentAsString = await response.HttpResponse.Content.ReadAsStringAsync();
            var path = JObject.Parse(httpResponseContentAsString)["path"].Value<string>();
            var queryString = JObject.Parse(httpResponseContentAsString)["queryString"].Value<string>();
            var statusCode = response.StatusCode;

            // Assert
            Assert.Equal(200, (int)statusCode);
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal("search/query", path);
            Assert.Equal(expectedResult, queryString);
        }

        public static IEnumerable<object[]> AllSortOrders => Enum
            .GetValues(typeof(SortOrder))
            .Cast<SortOrder>()
            .Select(so => new object[] { so });

        private static readonly Dictionary<SortOrder, string> SortNames = new Dictionary<SortOrder, string>
        {
            {SortOrder.LastEdited, "lastEdited"},
            {SortOrder.Relevance, "relevance"},
            {SortOrder.Published, "published"},
            {SortOrder.TitleAscending, "title-asc"},
            {SortOrder.TitleDescending, "title-desc"},
            {SortOrder.CreatedAscending, "created-asc"},
            {SortOrder.CreatedDescending, "created-desc"},
            {SortOrder.TotalDownloadsAscending, "totalDownloads-asc"},
            {SortOrder.TotalDownloadsDescending, "totalDownloads-desc"},
        };

        public class ResilientClientForTest : IResilientSearchClient
        {
            HttpStatusCode _statusCodeForGetAsync;

            private ResilientClientForTest(HttpStatusCode statusCodeForGetAsync)
            {
                _statusCodeForGetAsync = statusCodeForGetAsync;
            }

            public static ResilientClientForTest GetTestInstance(HttpStatusCode statusCodeForGetAsync)
            {
                return new ResilientClientForTest(statusCodeForGetAsync);
            }

            public async Task<HttpResponseMessage> GetAsync(string path, string queryString)
            {
                await Task.Yield();

                var content = new JObject(
                            new JProperty("queryString", queryString),
                            new JProperty("path", path));

                return new HttpResponseMessage()
                {
                    Content = new StringContent(content.ToString(), Encoding.UTF8, CoreConstants.TextContentType),
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, $"{path}/{queryString}"),
                    StatusCode = _statusCodeForGetAsync
                };
            }

            public async Task<string> GetStringAsync(string path, string queryString)
            {
                await Task.Yield();
                return $"{path} {queryString}";
            }
        }
    }
}

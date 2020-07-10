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
        [InlineData(null, null, false, GalleryConstants.PackageTypeFilterNames.Dependency, SortOrder.Relevance, 1, 10, false, false, false, false, null, null, "q=&skip=1&take=10&packageType=dependency&sortBy=relevance&luceneQuery=false")]
        [InlineData("query", "projectTypeFilter", true, GalleryConstants.PackageTypeFilterNames.DotNetTool ,SortOrder.LastEdited, 1, 10, true, true, true, true, "supportedFramework", "semVerLevel", "q=query&skip=1&take=10&packageType=dotnettool&sortBy=lastEdited&semVerLevel=semVerLevel&supportedFramework=supportedFramework&projectType=projectTypeFilter&prerelease=true&explanation=true&ignoreFilter=true&countOnly=true")]
        [InlineData("query", "projectTypeFilter", true, GalleryConstants.PackageTypeFilterNames.Template, SortOrder.Published, 1, 10, true, true, true, true, "supportedFramework", "semVerLevel", "q=query&skip=1&take=10&packageType=template&sortBy=published&semVerLevel=semVerLevel&supportedFramework=supportedFramework&projectType=projectTypeFilter&prerelease=true&explanation=true&ignoreFilter=true&countOnly=true")]
        [InlineData("query", "projectTypeFilter", true, GalleryConstants.PackageTypeFilterNames.Dependency, SortOrder.TitleAscending, 1, 10, true, true, true, true, "supportedFramework", "semVerLevel", "q=query&skip=1&take=10&packageType=dependency&sortBy=title-asc&semVerLevel=semVerLevel&supportedFramework=supportedFramework&projectType=projectTypeFilter&prerelease=true&explanation=true&ignoreFilter=true&countOnly=true")]
        [InlineData("query", "projectTypeFilter", true, GalleryConstants.PackageTypeFilterNames.Dependency, SortOrder.TitleDescending, 1, 10, true, true, true, true, "supportedFramework", "semVerLevel", "q=query&skip=1&take=10&packageType=dependency&sortBy=title-desc&semVerLevel=semVerLevel&supportedFramework=supportedFramework&projectType=projectTypeFilter&prerelease=true&explanation=true&ignoreFilter=true&countOnly=true")]

        public async Task SearchArgumentsAreCorrectSet(string query,
            string projectTypeFilter,
            bool includePrerelease,
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

        private static readonly Dictionary<PackageTypeFilter, string> PackageTypeNames = new Dictionary<PackageTypeFilter, string>
        {
            {PackageTypeFilter.All, GalleryConstants.PackageTypeFilterNames.All},
            {PackageTypeFilter.Dependency, GalleryConstants.PackageTypeFilterNames.Dependency},
            {PackageTypeFilter.DotNetTool, GalleryConstants.PackageTypeFilterNames.DotNetTool},
            {PackageTypeFilter.Template, GalleryConstants.PackageTypeFilterNames.Template},
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

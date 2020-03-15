// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Infrastructure.Search.Models;
using NuGetGallery.OData;
using NuGetGallery.WebApi;
using Xunit;

namespace NuGetGallery
{
    public class SearchAdaptorFacts
    {
        protected static ODataQueryOptions<V2FeedPackage> GetODataQueryOptionsForTest(Uri requestUri)
        {
            return new ODataQueryOptions<V2FeedPackage>(
                new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), 
                new HttpRequestMessage(HttpMethod.Get, requestUri));
        }

        protected static ODataQuerySettings GetODataQuerySettingsForTest()
        {
            return new ODataQuerySettings(QueryResultDefaults.DefaultQuerySettings)
            {
                PageSize = 100
            };
        }

        public class TheSearchCoreMethod : Facts
        {
            public TheSearchCoreMethod()
            {
                RawUrl = "http://example/api/v2/Search()?q=%27json%27&";
                SearchTerm = "json";
                TargetFramework = string.Empty;
                IncludePrerelease = true;
            }

            public string SearchTerm { get; }
            public string TargetFramework { get; }
            public bool IncludePrerelease { get; }

            [Theory]
            [MemberData(nameof(DefaultOrderByData))]
            public async Task DefaultsToOrderByRelevance(string queryString)
            {
                await TestOrderBy(queryString, SortOrder.Relevance);
            }

            [Theory]
            [MemberData(nameof(OrderByData))]
            public async Task ProperlyParsesOrderBy(string queryString, SortOrder? expected)
            {
                await TestOrderBy(queryString, expected);
            }

            private async Task TestOrderBy(string queryString, SortOrder? expected)
            {
                RawUrl += "&" + queryString;

                var result = await SearchAdaptor.SearchCore(
                    SearchService.Object,
                    Request.Object,
                    () => Packages,
                    SearchTerm,
                    TargetFramework,
                    IncludePrerelease,
                    SemVerLevel);

                VerifySortOrder(expected);
            }
        }

        public class TheFindByIdAndVersionCoreMethod : Facts
        {
            public TheFindByIdAndVersionCoreMethod()
            {
                RawUrl = "http://example/api/v2/FindPackagesById()?id=%27NuGet.Versioning%27";
                Id = "NuGet.Versioning";
                Version = null;
            }

            public string Id { get; }
            public string Version { get; set; }

            [Theory]
            [MemberData(nameof(DefaultOrderByData))]
            public async Task DefaultsToOrderByRelevance(string queryString)
            {
                await TestOrderBy(queryString, SortOrder.CreatedAscending);
            }

            [Theory]
            [MemberData(nameof(OrderByData))]
            public async Task ProperlyParsesOrderBy(string queryString, SortOrder? expected)
            {
                await TestOrderBy(queryString, expected);
            }

            private async Task TestOrderBy(string queryString, SortOrder? expected)
            {
                RawUrl += "&" + queryString;

                var result = await SearchAdaptor.FindByIdAndVersionCore(
                    SearchService.Object,
                    Request.Object,
                    () => Packages,
                    Id,
                    Version,
                    SemVerLevel);

                VerifySortOrder(expected);
            }
        }

        public abstract class Facts
        {
            public Facts()
            {
                SearchService = new Mock<ISearchService>();
                Request = new Mock<HttpRequestBase>();
                Packages = new List<Package>().AsQueryable();

                RawUrl = "http://example/api/v2/Packages()";
                SemVerLevel = SemVerLevelKey.SemVerLevel2;
                SearchResults = new SearchResults(0, indexTimestampUtc: null);

                Request
                    .Setup(x => x.RawUrl)
                    .Returns(() => RawUrl);
                SearchService
                    .Setup(x => x.ContainsAllVersions)
                    .Returns(true);
                SearchService
                    .Setup(x => x.Search(It.IsAny<SearchFilter>()))
                    .ReturnsAsync(() => SearchResults)
                    .Callback<SearchFilter>(sf => LastSearchFilter = sf);
            }

            public Mock<ISearchService> SearchService { get; }
            public Mock<HttpRequestBase> Request { get; }
            public IQueryable<Package> Packages { get; }
            public string RawUrl { get; set; }
            public string SemVerLevel { get; }
            public SearchResults SearchResults { get; }
            public SearchFilter LastSearchFilter { get; set; }

            public static IEnumerable<object[]> DefaultOrderByData => new[]
            {
                new object[] { "" },
                new object[] { "$orderby" },
                new object[] { "$orderby=" },
            };

            public static IEnumerable<object[]> OrderByData => new[]
            {
                new object[] { "$orderby=Created", SortOrder.CreatedAscending },
                new object[] { "$orderby=Published&$orderby=Created", SortOrder.CreatedAscending }, // Last one wins
                new object[] { "$orderby=Created%20asc", SortOrder.CreatedAscending },
                new object[] { "$orderby=Created%20ASC", SortOrder.CreatedAscending },
                new object[] { "$orderby=Created%20foo", SortOrder.CreatedAscending },
                new object[] { "$orderby=CreatedFOO", SortOrder.CreatedAscending }, // Probably shouldn't work, but it does today
                new object[] { "$orderby=Created%20DESC", SortOrder.CreatedAscending },
                new object[] { "$orderby=Created%20desc", SortOrder.CreatedDescending },
                new object[] { "$orderby=Created%20asc%20desc", SortOrder.CreatedDescending }, // Probably shouldn't work, but it does today
                new object[] { "$orderby=DownloadCount", SortOrder.Relevance },
                new object[] { "$orderby=Published", SortOrder.Published },
                new object[] { "$orderby=LastEdited", SortOrder.LastEdited },
                new object[] { "$orderby=Id", SortOrder.TitleAscending },
                new object[] { "$orderby=Id%20desc", SortOrder.TitleAscending }, // Probably should be TitleDescending, but it doesn't do this today
                new object[] { "$orderby=concat", SortOrder.TitleAscending },
                new object[] { "$orderby=concat%20desc", SortOrder.TitleDescending },
                new object[] { "$orderby=Dependencies", null },
                new object[] { "$orderby=created", null },
            };

            public void VerifySortOrder(SortOrder? expected)
            {
                if (expected == null)
                {
                    // The hijack to search service was not possible given the parameters.
                    Assert.Null(LastSearchFilter);
                    SearchService.Verify(x => x.Search(It.IsAny<SearchFilter>()), Times.Never);
                }
                else
                {
                    Assert.NotNull(LastSearchFilter);
                    Assert.Equal(expected, LastSearchFilter.SortOrder);
                    SearchService.Verify(x => x.Search(It.IsAny<SearchFilter>()), Times.Once);
                }
            }
        }

        public class TheGetNextLinkMethod
        {
            [Fact]
            public void DoesNotGenerateNextLinkWhenNoAdditionalResultsOnPage()
            {
                // Arrange
                var requestUri = new Uri("https://localhost:8081/api/v2/Packages");
                var resultCount = 20; // our result set contains 20 elements

                // Act
                var nextLink = SearchAdaptor.GetNextLink(requestUri, resultCount, null,
                    GetODataQueryOptionsForTest(requestUri), 
                    GetODataQuerySettingsForTest());

                // Assert
                Assert.Null(nextLink);
            }

            [Fact]
            public void DoesNotGenerateNextLinkWhenSkipCountLargerThanResultSet()
            {
                // Arrange
                var requestUri = new Uri("https://localhost:8081/api/v2/Packages?$skip=300"); // skip 300 items
                var resultCount = 200; // our result set contains 200 elements

                // Act
                var nextLink = SearchAdaptor.GetNextLink(requestUri, resultCount, null,
                    GetODataQueryOptionsForTest(requestUri), 
                    GetODataQuerySettingsForTest());

                // Assert
                Assert.Null(nextLink);
            }

            [Fact]
            public void GeneratesNextLinkForSimpleUrl1()
            {
                // Arrange
                var requestUri = new Uri("https://localhost:8081/api/v2/Packages");
                var resultCount = 200; // our result set contains 200 elements

                // Act
                var nextLink = SearchAdaptor.GetNextLink(requestUri, resultCount, null, 
                    GetODataQueryOptionsForTest(requestUri), 
                    GetODataQuerySettingsForTest());

                // Assert
                Assert.Equal(new Uri("https://localhost:8081/api/v2/Packages?$skip=100"), nextLink);

                // Act 2
                nextLink = SearchAdaptor.GetNextLink(nextLink, resultCount, null, 
                    GetODataQueryOptionsForTest(nextLink), 
                    GetODataQuerySettingsForTest());

                // Assert 2
                Assert.Null(nextLink);
            }

            [Fact]
            public void GeneratesNextLinkForSimpleUrl2()
            {
                // Arrange
                var requestUri = new Uri("https://localhost:8081/api/v2/Packages");
                var resultCount = 210; // our result set contains 210 elements

                // Act
                var nextLink = SearchAdaptor.GetNextLink(requestUri, resultCount, null, 
                    GetODataQueryOptionsForTest(requestUri),
                    GetODataQuerySettingsForTest());

                // Assert
                Assert.Equal(new Uri("https://localhost:8081/api/v2/Packages?$skip=100"), nextLink);

                // Act 2
                nextLink = SearchAdaptor.GetNextLink(nextLink, resultCount, null, 
                    GetODataQueryOptionsForTest(nextLink), 
                    GetODataQuerySettingsForTest());

                // Assert 2
                Assert.Equal(new Uri("https://localhost:8081/api/v2/Packages?$skip=200"), nextLink);

                // Act 3
                nextLink = SearchAdaptor.GetNextLink(nextLink, resultCount, null,
                    GetODataQueryOptionsForTest(nextLink), 
                    GetODataQuerySettingsForTest());

                // Assert 3
                Assert.Null(nextLink);
            }

            [Fact]
            public void GeneratesNextLinkForComplexUrl()
            {
                // Arrange
                var requestUri = new Uri("https://localhost:8081/api/v2/Search()?searchTerm='foo'&$orderby=Id&$skip=100&$top=1000");
                var resultCount = 2000; // our result set contains 2000 elements

                // Act
                var nextLink = SearchAdaptor.GetNextLink(requestUri, resultCount, new { searchTerm = "foo" },
                    GetODataQueryOptionsForTest(requestUri),
                    GetODataQuerySettingsForTest());

                // Assert
                Assert.Equal(new Uri("https://localhost:8081/api/v2/Search()?searchTerm='foo'&$orderby=Id&$skip=200&$top=1000"), nextLink);
            }

            [Fact]
            public void GeneratesNextLinkForComplexUrlWithSemVerLevel2()
            {
                // Arrange
                var requestUri = new Uri("https://localhost:8081/api/v2/Search()?searchTerm='foo'&$orderby=Id&$skip=100&$top=1000&semVerLevel=2.0.0");
                var resultCount = 2000; // our result set contains 2000 elements

                // Act
                var nextLink = SearchAdaptor.GetNextLink(requestUri, resultCount, new { searchTerm = "foo" },
                    GetODataQueryOptionsForTest(requestUri),
                    GetODataQuerySettingsForTest(),
                    SemVerLevelKey.SemVer2);

                // Assert
                Assert.Equal(new Uri("https://localhost:8081/api/v2/Search()?searchTerm='foo'&$orderby=Id&$skip=200&$top=1000&semVerLevel=2.0.0"), nextLink);
            }
        }
    }
}
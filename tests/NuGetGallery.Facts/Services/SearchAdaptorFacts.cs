// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
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
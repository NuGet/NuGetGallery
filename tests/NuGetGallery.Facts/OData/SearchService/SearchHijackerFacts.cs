// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using NuGetGallery.OData;
using Xunit;

namespace NuGetGallery
{
    public class SearchHijackerFacts
    {
        protected static ODataQueryOptions<V2FeedPackage> GetODataQueryOptionsForTest(Uri requestUri)
        {
            return new ODataQueryOptions<V2FeedPackage>(
                new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)),
                new HttpRequestMessage(HttpMethod.Get, requestUri));
        }

        public class TheIsHijackableMethod
        {
            [Fact]
            public void IsHijackableReturnsFalseWhenValidSubstringOfInSingleValueExpression()
            {
                AssertIsNotHijackable("https://nuget.localtest.me/api/v2/Packages()?$filter=substringof(Id,%27MyPackage%27)");
            }

            [Fact]
            public void IsHijackableReturnsFalseWhenValidSubstringOfInBinaryOperatorWithConvertNode()
            {
                AssertIsNotHijackable("https://nuget.localtest.me/api/v2/Packages()?$filter=substringof(Id,%27MyPackage%27)%20and%20Id%20eq%20%27MyPackageId%27");
            }

            [Fact]
            public void IsHijackableReturnsFalseWhenInvalidSubstringOfInSingleValueExpression()
            {
                AssertIsNotHijackable("https://localhost:8081/api/v2/Packages?$filter=substringof(null,Tags)");
            }

            [Fact]
            public void IsHijackableReturnsFalseWhenInvalidSubstringOfInBinaryOperatorExpressionLeft()
            {
                AssertIsNotHijackable("https://nuget.localtest.me/api/v2/Packages()?$filter=substringof(null,Tags)%20and%20IsLatestVersion%20and%20IsLatestVersion");
            }

            [Fact]
            public void IsHijackableReturnsFalseWhenInvalidSubstringOfInBinaryOperatorExpressionRight()
            {
                AssertIsNotHijackable("https://nuget.localtest.me/api/v2/Packages()?$filter=IsLatestVersion%20and%20substringof(null,Tags)");
            }

            private void AssertIsNotHijackable(string uri)
            {
                // Arrange
                var requestUri = new Uri(uri);

                // Act
                HijackableQueryParameters hijackableQueryParameters = null;
                var result = SearchHijacker.IsHijackable(GetODataQueryOptionsForTest(requestUri), out hijackableQueryParameters);

                // Assert
                Assert.False(result);
            }
        }
    }
}
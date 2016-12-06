// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
            public static IEnumerable<object[]> IsHijackableReturnsFalseIfFilterContainsSubstringOf_Input
            {
                get
                {
                    return new []
                    {
                        // Valid substringof in SingleValueExpression
                        new object[] { "https://nuget.localtest.me/api/v2/Packages()?$filter=substringof(Id,%27MyPackage%27)" },
                        // Valid substringof in BinaryOperatorExpression, nested in ConvertNode
                        new object[] { "https://nuget.localtest.me/api/v2/Packages()?$filter=substringof(Id,%27MyPackage%27)%20and%20Id%20eq%20%27MyPackageId%27" },
                        // Invalid substringof in SingleValueExpression
                        new object[] { "https://localhost:8081/api/v2/Packages?$filter=substringof(null,Tags)" },
                        // Invalid substringof in left-most node of BinaryOperationExpression (traversal is right to left)
                        new object[] { "https://nuget.localtest.me/api/v2/Packages()?$filter=substringof(null,Tags)%20and%20IsLatestVersion%20and%20IsLatestVersion" },
                        // Invalid substringof in right node of BinaryOperationExpression (traversal is right to left)
                        new object[] { "https://nuget.localtest.me/api/v2/Packages()?$filter=IsLatestVersion%20and%20substringof(null,Tags)" }
                    };
                }
            }

            [Theory]
            [MemberData("IsHijackableReturnsFalseIfFilterContainsSubstringOf_Input")]
            public void IsHijackableReturnsFalseIfFilterContainsSubstringOf(string uri)
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
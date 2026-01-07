// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGetGallery
{
    public class UriExtensionsFacts
    {
        public class TheAppendQueryStringToRelativeUriMethod
        {
            [Theory]
            [InlineData("/abac/something?q1=123", "q2=ads&q3=4324", "/abac/something?q1=123&q2=ads&q3=4324")]
            [InlineData("/abac/something?q1=123", "q2=ads", "/abac/something?q1=123&q2=ads")]
            [InlineData("/abac/something", "q2=ads", "/abac/something?q2=ads")]
            [InlineData("/abac/something/", "", "/abac/something/")]
            public void ReturnsExpectedUrl(string url, string queryParameters, string expectedUrl)
            {
                // Arrange
                var queryString = string.IsNullOrEmpty(queryParameters)
                    ? new List<KeyValuePair<string, string>>()
                    : queryParameters
                        .Split('&')
                        .Select(qv => qv.Split('='))
                        .Select(qv => new KeyValuePair<string, string>(qv[0], qv[1]))
                        .ToList();

                // Act
                var returnUrl = UriExtensions.AppendQueryStringToRelativeUri(url, queryString);

                // Assert
                Assert.Equal(expectedUrl, returnUrl);
            }
        }
    }
}

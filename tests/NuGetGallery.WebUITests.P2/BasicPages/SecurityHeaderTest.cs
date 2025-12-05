// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NuGetGallery.FunctionalTests.Helpers;
using Xunit;

namespace NuGetGallery.FunctionalTests.WebUITests.BasicPages
{
    /// <summary>
    ///     Verify that an expected series of security headers is returned as part of the response.
    ///     Priority :P2
    /// </summary>
    public class SecurityHeaderTest
    {
        [Priority(2)]
        [Fact]
        public async Task HomePageReturnsExpectedSecurityHeaders()
        {
            // Arrange
            using var client = new HttpClient();

            // Act
            var response = await client.GetAsync(UrlHelper.BaseUrl);

            // Assert
            response.EnsureSuccessStatusCode();

            AssertSecurityHeaders(response);
        }

        [Priority(2)]
        [Fact]
        public async Task PackagesPageReturnsExpectedSecurityHeaders()
        {
            // Arrange
            using var client = new HttpClient();

            // Act
            var response = await client.GetAsync(UrlHelper.PackagesPageUrl);

            // Assert
            response.EnsureSuccessStatusCode();

            AssertSecurityHeaders(response);
        }

        private void AssertSecurityHeaders(HttpResponseMessage response)
        {
            VerifyHeaderValue(response, "X-Frame-Options", "deny");
            VerifyHeaderValue(response, "X-XSS-Protection", "1; mode=block");
            VerifyHeaderValue(response, "X-Content-Type-Options", "nosniff");
            VerifyHeaderValue(response, "Strict-Transport-Security", "max-age=31536000");
        }

        private static void VerifyHeaderValue(HttpResponseMessage response, string headerName, string expectedValue)
        {
            var actualValue = response.Headers.GetValues(headerName)?.FirstOrDefault() ?? null;
            Assert.StartsWith(expectedValue, actualValue);
        }
    }
}

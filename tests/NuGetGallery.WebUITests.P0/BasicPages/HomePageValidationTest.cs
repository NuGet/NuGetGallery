// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using NuGetGallery.FunctionalTests.Helpers;
using Xunit;

namespace NuGetGallery.FunctionalTests.WebUITests.BasicPages
{
    /// <summary>
    /// Sends http request to gallery home page checks for the default home page text in the response.
    /// priority : p0
    /// </summary>
    public class HomePageValidationTest
    {
        [Priority(0)]
        [Fact]
        public async Task HomePageContainsExpectedText()
        {
            // Arrange
            using var client = new HttpClient();

            // Act
            var response = await client.GetAsync(UrlHelper.BaseUrl);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            // Check for home page text
            Assert.Contains(Constants.HomePageText, content);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.WebUITests.BasicPages
{
    /// <summary>
    /// Sends a http request to the statistics page and tries to validate the default stats page text and the presence of top package.
    /// Priority : p1
    /// </summary>
    public class StatisticsPageTest
    {
        [Priority(1)]
        [Fact]
        public async Task StatisticsPageContainsExpectedContent()
        {
            // Arrange
            using var client = new HttpClient();
            
            // Act
            var response = await client.GetAsync(UrlHelper.StatsPageUrl);
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            // Check for the presence of the rank element
            Assert.Contains(@"<h2 class=""stats-title-text"">", content);
            
            // Check for default text in stats page
            Assert.Contains(Constants.StatsPageDefaultText, content);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Microsoft.Playwright.Xunit;
using Xunit;

namespace NuGetGallery.FunctionalTests.Playwright.BasicPages
{
	public class HomePageValidationTest : NuGetPageTest
    {
		[Fact]
        [Category("P0Tests")]
        [Category("ReadOnlyModeTests")]
        public async Task HomePageLoads_ContainsExpectedText()
		{
            // Act
            var response = await Page.GotoAsync(UrlHelper.BaseUrl);

            // Assert
            Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)response.Status);
            await Expect(Page.Locator(".what-is-nuget")).ToContainTextAsync(Constants.HomePageText);
		}
	}
}

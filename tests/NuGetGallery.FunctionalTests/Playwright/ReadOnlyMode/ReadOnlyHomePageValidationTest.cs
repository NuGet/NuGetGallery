// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.Playwright.ReadOnlyMode
{
    public class ReadOnlyHomePageValidationTest : NuGetPageTest
    {
        [Fact]
        [Priority(0)]
        [Category("ReadOnlyModeTests")]
        public async Task HomePageLoads_ContainsExpectedText()
        {
            var response = await Page.GotoAsync(UrlHelper.BaseUrl);

            Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)response.Status);
            await Expect(Page.Locator(".what-is-nuget")).ToContainTextAsync(Constants.HomePageText);
        }
    }
}

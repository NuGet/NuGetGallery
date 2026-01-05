// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.Playwright.ReadOnlyMode
{
	public class LogonInReadOnlyModeTest : NuGetPageTest
	{
        [Fact]
        [Category("ReadOnlyModeTests")]
        public async Task LoginPostToReadOnlyServer_Returns503_WithReadOnlyModeError()
        {
            // Act
            var responseTask = Page.WaitForResponseAsync(response => response.Url.Contains("SignIn"));
            await SignInAsync();

            // Assert
            var response = await responseTask;
            await Expect(Page.Locator(".error-title")).ToContainTextAsync(Constants.ReadOnlyModeError);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (HttpStatusCode) response.Status);
		}
	}
}

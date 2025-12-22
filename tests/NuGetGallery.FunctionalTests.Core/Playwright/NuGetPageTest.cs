// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

#nullable enable

namespace NuGetGallery.FunctionalTests.Playwright
{
    public class NuGetPageTest : PageTest
    {
        override public async Task InitializeAsync()
        {
            // Uncomment this to make Playwright run in headed mode (visible browser) for debugging.
            // Environment.SetEnvironmentVariable("HEADED", "1");
            await base.InitializeAsync();
        }

        public async Task SignInAsync(string? email = null, string? password = null)
        {
            await Page.GotoAsync(UrlHelper.LogonPageUrl);

            await Page.Locator("input[name='SignIn.UserNameOrEmail']").FillAsync(email ?? GalleryConfiguration.Instance.Account.Email);
            await Page.Locator("input[name='SignIn.Password']").FillAsync(password ?? GalleryConfiguration.Instance.Account.Password);
            await Page.Locator("input:has-text('Sign in')").ClickAsync();

            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
    }
}

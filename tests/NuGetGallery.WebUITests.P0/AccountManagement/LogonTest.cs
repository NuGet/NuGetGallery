// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NuGetGallery.FunctionalTests.Helpers;
using Xunit;

namespace NuGetGallery.FunctionalTests.WebUITests.AccountManagement
{
    /// <summary>
    /// Tries to login with a POST request with the credentials retrieved from the data source. Validates that the response has the logged in user name.
    /// priority : p0
    /// </summary>
    public class LogonTest
    {
        [Priority(0)]
        [Fact]
        public async Task CanLogonWithValidCredentials()
        {
            // Arrange
            using var client = new HttpClient();
            var cookieContainer = new System.Net.CookieContainer();
            using var handler = new HttpClientHandler { CookieContainer = cookieContainer };
            using var authenticatedClient = new HttpClient(handler);

            // Act
            // First get the logon page to retrieve any anti-forgery tokens
            var logonGetResponse = await authenticatedClient.GetAsync(UrlHelper.LogonPageUrl);
            logonGetResponse.EnsureSuccessStatusCode();

            // Extract form data and tokens from the response
            var logonGetContent = await logonGetResponse.Content.ReadAsStringAsync();

            // Post login credentials
            var formData = AssertAndValidationHelper.GetLogonPostFormData(logonGetContent);
            var logonPostResponse = await authenticatedClient.PostAsync(
                UrlHelper.SignInPageUrl,
                new FormUrlEncodedContent(formData)
            );

            // Assert
            logonPostResponse.EnsureSuccessStatusCode();
            var logonPostContent = await logonPostResponse.Content.ReadAsStringAsync();

            // Check that the logged-in username appears in the response
            Assert.Contains(@"<span class=""dropdown-username"">NugetTestAccount</span>", logonPostContent);
        }
    }
}

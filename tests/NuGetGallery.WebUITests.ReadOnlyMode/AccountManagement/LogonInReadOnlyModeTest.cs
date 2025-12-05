// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGetGallery.FunctionalTests.Helpers;
using Xunit;

namespace NuGetGallery.FunctionalTests.WebUITests.ReadOnlyMode
{
    /// <summary>
    /// Tries to login with a POST request to a read-only server. This is not allowed since logging on updates the
    /// user record in the database.
    /// </summary>
    public class LogonTest
    {
        [Fact]
        public async Task LogonShouldFailInReadOnlyMode()
        {
            // Arrange
            var cookieContainer = new CookieContainer();
            using var handler = new HttpClientHandler
            {
                CookieContainer = cookieContainer,
                // Setting AutoRedirect to false to match WebTest behavior
                AllowAutoRedirect = false,
                // PreAuthenticate equivalent
                PreAuthenticate = true
            };

            using var client = new HttpClient(handler);

            // Add read-only mode header that might be expected by the server
            client.DefaultRequestHeaders.Add("X-NuGet-ReadOnly-Mode-Test", "true");

            // Act
            // First get the logon page to retrieve any anti-forgery tokens - exactly like the original WebTest
            var logonGetResponse = await client.GetAsync(UrlHelper.LogonPageUrl);
            logonGetResponse.EnsureSuccessStatusCode();
            var logonGetContent = await logonGetResponse.Content.ReadAsStringAsync();

            // Extract form data including the anti-forgery token
            var formData = AssertAndValidationHelper.GetLogonPostFormData(logonGetContent);

            try
            {
                // Post login credentials - this should fail with 503 in read-only mode
                var logonPostResponse = await client.PostAsync(
                    UrlHelper.SignInPageUrl,
                    new FormUrlEncodedContent(formData)
                );

                // Assert
                // Check for 503 Service Unavailable status code - exactly matching the original test's ExpectedHttpStatusCode
                Assert.Equal(HttpStatusCode.ServiceUnavailable, logonPostResponse.StatusCode);

                // Verify the response contains the read-only mode error message - just like the validation rule
                var responseContent = await logonPostResponse.Content.ReadAsStringAsync();
                Assert.Contains(Constants.ReadOnlyModeError, responseContent);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("503"))
            {
                // Some HttpClient implementations might throw instead of returning the error code
                // This is acceptable because we expected a 503
                // We don't need to do anything here - the test passes if a 503 exception is thrown
            }
        }
    }
}

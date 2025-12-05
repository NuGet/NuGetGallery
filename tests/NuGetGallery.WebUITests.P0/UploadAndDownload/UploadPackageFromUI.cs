// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGetGallery.FunctionalTests.Helpers;
using Xunit;
using static NuGetGallery.FunctionalTests.Helpers.UploadHelper;

namespace NuGetGallery.FunctionalTests.WebUITests.UploadAndDownload
{
    public class UploadPackageFromUI
    {
        [Fact]
        public Task UploadToSelf()
        {
            return VerifyPackageUploadAsync(null);
        }

        [Fact]
        public Task UploadToOrganizationAsCollaborator()
        {
            return VerifyPackageUploadAsync(GalleryConfiguration.Instance.CollaboratorOrganization.Name);
        }

        [Fact]
        public Task UploadToOrganizationAsAdmin()
        {
            return VerifyPackageUploadAsync(GalleryConfiguration.Instance.AdminOrganization.Name);
        }

        private static async Task VerifyPackageUploadAsync(string owner)
        {
            // Use a cookie-enabled HttpClient to maintain session state
            var cookieContainer = new CookieContainer();
            using var handler = new HttpClientHandler { CookieContainer = cookieContainer };
            using var client = new HttpClient(handler);

            // Login first
            await LoginAsync(client);

            // Then upload each package
            var id = UploadHelper.GetUniquePackageId();
            PackageToUpload[] packages = [new PackageToUpload(id, "1.0.0", owner), new PackageToUpload(id, "2.0.0", owner)];
            foreach (var package in packages)
            {
                var uploadPageResponse = await client.GetAsync(UrlHelper.UploadPageUrl);
                uploadPageResponse.EnsureSuccessStatusCode();
                var uploadPageContent = await uploadPageResponse.Content.ReadAsStringAsync();

                await UploadHelper.UploadAndVerifyPackageAsync(client, package);
            }
        }

        private static async Task LoginAsync(HttpClient client)
        {
            // Get login page to retrieve anti-forgery token
            var loginPageResponse = await client.GetAsync(UrlHelper.LogonPageUrl);
            loginPageResponse.EnsureSuccessStatusCode();
            var loginPageContent = await loginPageResponse.Content.ReadAsStringAsync();

            // Extract form data including token
            var formData = AssertAndValidationHelper.GetLogonPostFormData(loginPageContent);

            // Post login credentials
            var loginResponse = await client.PostAsync(
                UrlHelper.SignInPageUrl,
                new FormUrlEncodedContent(formData));

            loginResponse.EnsureSuccessStatusCode();
        }
    }
}

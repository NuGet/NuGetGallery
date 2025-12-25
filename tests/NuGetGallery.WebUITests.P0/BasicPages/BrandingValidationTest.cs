// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using NuGetGallery.FunctionalTests.Helpers;
using Xunit;

namespace NuGetGallery.FunctionalTests.WebUITests.BasicPages
{
    /// <summary>
    /// Sends http request to gallery home page checks for the configured branding text in the response.
    /// priority : p1
    /// </summary>
    public class BrandingValidationTest
    {
        [Priority(1)]
        [Fact]
        public async Task HomePageContainsExpectedBranding()
        {
            // Arrange
            using var client = new HttpClient();

            // Act
            var response = await client.GetAsync(UrlHelper.BaseUrl);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            if (String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.Message)
                && String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.Url)
                && String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.AboutUrl)
                && String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.PrivacyPolicyUrl)
                && String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.TermsOfUseUrl)
                && String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.TrademarksUrl))
            {
                // Default branding
                Assert.Contains(@"<a href=""https://www.dotnetfoundation.org"">", content);
                Assert.Contains($@"&copy; {DateTime.UtcNow.Year} .NET Foundation", content);
                Assert.Contains($@"<a href=""{UrlHelper.BaseUrl}policies/Terms"">Terms of Use</a>", content);
                Assert.Contains($@"<a href=""{UrlHelper.BaseUrl}policies/Privacy"" id=""footer-privacy-policy-link"">Privacy Policy</a>", content);
            }
            else
            {
                // Custom branding
                if (!String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.Message))
                {
                    Assert.Contains(string.Format(GalleryConfiguration.Instance.Branding.Message, DateTime.UtcNow.Year), content);
                }

                if (!String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.Url))
                {
                    Assert.Contains($@"<a href=""{GalleryConfiguration.Instance.Branding.Url}"">", content);
                }

                if (!String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.AboutUrl))
                {
                    Assert.Contains($@"<a href=""{GalleryConfiguration.Instance.Branding.AboutUrl}"">", content);
                }

                if (!String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.PrivacyPolicyUrl))
                {
                    Assert.Contains($@"<a href=""{GalleryConfiguration.Instance.Branding.PrivacyPolicyUrl}"" id=""footer-privacy-policy-link"">", content);
                }

                if (!String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.TermsOfUseUrl))
                {
                    Assert.Contains($@"<a href=""{GalleryConfiguration.Instance.Branding.TermsOfUseUrl}"">", content);
                }

                if (!String.IsNullOrEmpty(GalleryConfiguration.Instance.Branding.TrademarksUrl))
                {
                    Assert.Contains($@"<a href=""{GalleryConfiguration.Instance.Branding.TrademarksUrl}"">", content);
                }
            }
        }
    }
}

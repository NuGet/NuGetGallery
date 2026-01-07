// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.Playwright.BasicPages
{
	public class BrandingValidationTest : NuGetPageTest
	{
		[Fact]
		[Priority(0)]
		[Category("P0Tests")]
		public async Task HomePage_ContainsBrandingElements()
		{
			// Act
			var response = await Page.GotoAsync(UrlHelper.BaseUrl);

			// Assert
			Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)response.Status);

			var content = await Page.ContentAsync();
			var branding = GalleryConfiguration.Instance.Branding;

			if (string.IsNullOrEmpty(branding.Message)
				&& string.IsNullOrEmpty(branding.Url)
				&& string.IsNullOrEmpty(branding.AboutUrl)
				&& string.IsNullOrEmpty(branding.PrivacyPolicyUrl)
				&& string.IsNullOrEmpty(branding.TermsOfUseUrl)
				&& string.IsNullOrEmpty(branding.TrademarksUrl))
			{
				// Check for default .NET Foundation branding
				Assert.Contains(@"<a href=""https://www.dotnetfoundation.org"">", content);
				Assert.Contains($"&copy; {DateTime.UtcNow.Year} .NET Foundation", content);
				Assert.Contains($@"<a href=""{UrlHelper.BaseUrl}policies/Terms"">Terms of Use</a>", content);
				Assert.Contains($@"<a href=""{UrlHelper.BaseUrl}policies/Privacy"" id=""footer-privacy-policy-link"">Privacy Policy</a>", content);
			}
			else
			{
				// Check for custom branding
				if (!string.IsNullOrEmpty(branding.Message))
				{
					var expectedMessage = string.Format(branding.Message, DateTime.UtcNow.Year);
					Assert.Contains(expectedMessage, content);
				}

				if (!string.IsNullOrEmpty(branding.Url))
				{
					Assert.Contains($@"<a href=""{branding.Url}"">", content);
				}

				if (!string.IsNullOrEmpty(branding.AboutUrl))
				{
					Assert.Contains($@"<a href=""{branding.AboutUrl}"">", content);
				}

				if (!string.IsNullOrEmpty(branding.PrivacyPolicyUrl))
				{
					Assert.Contains($@"<a href=""{branding.PrivacyPolicyUrl}"" id=""footer-privacy-policy-link"">", content);
				}

				if (!string.IsNullOrEmpty(branding.TermsOfUseUrl))
				{
					Assert.Contains($@"<a href=""{branding.TermsOfUseUrl}"">", content);
				}

				if (!string.IsNullOrEmpty(branding.TrademarksUrl))
				{
					Assert.Contains($@"<a href=""{branding.TrademarksUrl}"">", content);
				}
			}
		}
	}
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.Playwright.BasicPages
{
	[Collection(AspirePlaywrightCollection.Definition)]
	public class PackagesPageTest : NuGetPageTest
	{
		[Fact]
		[Priority(1)]
		[Category("P1Tests")]
        [Category("PlaywrightTests")]
        public async Task PackagePage_ContainsPackageIdAndVersion()
		{
			// Arrange
			var packageId = Constants.TestPackageId;
			var latestStableVersion = IsAspireHarness
				? "1.0.0"
				: await ClientSdkHelper.GetLatestStableVersionAsync(packageId);
			var packagePageUrl = UrlHelper.BaseUrl + "/Packages/" + packageId;
			var expectedText = $"{packageId} {latestStableVersion}";

			// Act
			var response = await Page.GotoAsync(packagePageUrl);

			// Assert
			Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)response.Status);
			
			var content = await Page.ContentAsync();
			Assert.Contains(expectedText, content);
		}
	}
}

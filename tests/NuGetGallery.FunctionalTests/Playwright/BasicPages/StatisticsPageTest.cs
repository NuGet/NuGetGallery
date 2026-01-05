// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.Playwright.BasicPages
{
	public class StatisticsPageTest : NuGetPageTest
	{
		[Fact]
		[Priority(1)]
		[Category("P1Tests")]
		public async Task StatisticsPage_ContainsExpectedContent()
		{
			// Act
			var response = await Page.GotoAsync(UrlHelper.StatsPageUrl);

			// Assert
			Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)response.Status);

			// Check for the presence of the stats title, which indicates there is at least one package in the list
			await Expect(Page.Locator("h2.stats-title-text").First).ToBeVisibleAsync();

			// Check for the default text in stats page
			await Expect(Page.Locator("body")).ToContainTextAsync(Constants.StatsPageDefaultText);
		}
	}
}

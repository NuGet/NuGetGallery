// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.Playwright.AccountManagement
{
	public class LogonTest : NuGetPageTest
	{
		[Fact]
		[Priority(0)]
		[Category("P0Tests")]
		public async Task Login_DisplaysLoggedInUsername()
		{
			// Act
			await SignInAsync();

			// Assert
			await Expect(Page.Locator("span.dropdown-username")).ToContainTextAsync(GalleryConfiguration.Instance.Account.Name);
		}
	}
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Xunit;

namespace NuGetGallery.Views.Packages
{
	public class DisplayPackageFacts
	{
		[Fact]
		public void SponsorshipLinksUseJsonEncodedDataAttribute()
		{
			// Arrange & Act
			var template = GetTemplate();

			// Assert
			Assert.Contains("data-sponsorship-url=\"@Json.Encode(sponsorshipUrl)\"", template);
			Assert.DoesNotContain("data-sponsorship-url=\"@Html.Raw(Json.Encode(sponsorshipUrl))\"", template);
		}

		private static string GetTemplate()
		{
			var repoRoot = Directory.GetCurrentDirectory();
			while (repoRoot is not null && !Directory.GetFiles(repoRoot).Any(x => Path.GetFileName(x) == "NuGetGallery.sln"))
			{
				repoRoot = Path.GetDirectoryName(repoRoot);
			}

			Assert.NotNull(repoRoot);
			var templatePath = Path.Combine(repoRoot, @"src\NuGetGallery\Views\Packages\DisplayPackage.cshtml");

			return File.ReadAllText(templatePath);
		}
	}
}

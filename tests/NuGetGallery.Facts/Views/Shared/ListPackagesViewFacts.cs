// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Xunit;

namespace NuGetGallery.Views.Shared
{
	public class ListPackagesViewFacts
	{
		[Fact]
		public void ListPackagesView_ContainsDotNetFrameworkTooltip()
		{
			// Find flags.json
			var current = Directory.GetCurrentDirectory();
			string flagsPath = null;
			while (current != null)
			{
				var candidate = Path.Combine(current, "src", "NuGetGallery", "App_Data", "Files", "Content", "flags.json");
				if (File.Exists(candidate))
				{
					flagsPath = candidate;
					break;
				}
				current = Directory.GetParent(current)?.FullName;
			}
			if (flagsPath == null)
			{
				// Skip if flags.json is not present
				return;
			}
			var flagsJson = File.ReadAllText(flagsPath);
			if (!flagsJson.Contains("\"NuGetGallery.AdvancedFrameworkFiltering\": \"Enabled\""))
			{
				// Skip if flag is not enabled
				return;
			}

			// Find ListPackages.cshtml
			current = Directory.GetCurrentDirectory();
			string viewPath = null;
			while (current != null)
			{
				var candidate = Path.Combine(current, "src", "NuGetGallery", "Views", "Shared", "ListPackages.cshtml");
				if (File.Exists(candidate))
				{
					viewPath = candidate;
					break;
				}
				current = Directory.GetParent(current)?.FullName;
			}
			Assert.False(viewPath == null, "Could not find ListPackages.cshtml in any parent directory.");

			var template = File.ReadAllText(viewPath);

			// Assert
			Assert.Contains("dotnetframework-tooltip", template);
			Assert.Contains("Selecting .NET will show you packages compatible with any of the individual frameworks within the .NET generation.", template);
		}
	}
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.FunctionalTests.XunitExtensions
{
	public class NeedsAppServiceTheoryAttribute : TheoryAttribute
	{
		public NeedsAppServiceTheoryAttribute()
		{
			if (!GalleryConfiguration.Instance.HasAppService)
			{
				Skip = "This test requires the gallery to be hosted in IIS/Azure App Service.";
			}
		}
	}
}

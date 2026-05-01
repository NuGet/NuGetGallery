// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.FunctionalTests.XunitExtensions
{
	public class NeedsStatisticsServiceFactAttribute : FactAttribute
	{
		public NeedsStatisticsServiceFactAttribute()
		{
			if (!GalleryConfiguration.Instance.HasStatisticsService)
			{
				Skip = "This test requires a statistics service.";
			}
		}
	}
}

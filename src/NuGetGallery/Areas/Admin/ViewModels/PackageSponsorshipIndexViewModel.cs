// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery.Areas.Admin.ViewModels
{
	public class PackageSponsorshipIndexViewModel
	{
		public string PackageId { get; set; }
		public Package Package { get; set; }
		public string NewSponsorshipUrl { get; set; }
		public string Message { get; set; }
		public bool IsSuccess { get; set; }
		public IReadOnlyCollection<SponsorshipUrlEntry> SponsorshipUrls { get; set; }
	}
}

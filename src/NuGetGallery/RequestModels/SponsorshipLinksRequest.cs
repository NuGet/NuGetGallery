// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
	public class AddSponsorshipLinkRequest
	{
		[Required]
		public string PackageId { get; set; }
		
		[Required]
		[Url]
		[StringLength(4000)]
		public string Url { get; set; }
	}

	public class RemoveSponsorshipLinkRequest
	{
		[Required]
		public string PackageId { get; set; }
		
		[Required]
		[Url]
		public string Url { get; set; }
	}
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.ViewModels
{
	public class ForceValidationFailureViewModel
	{
		[Display(Name = "Package ID")]
		[Required(ErrorMessage = "Package ID is required")]
		public string PackageId { get; set; }

		[Display(Name = "Package Version (optional - uses latest if not specified)")]
		public string PackageVersion { get; set; }
	}
}

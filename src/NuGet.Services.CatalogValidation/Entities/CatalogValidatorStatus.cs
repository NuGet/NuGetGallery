// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Validation;

namespace NuGet.Services.CatalogValidation.Entities
{
	/// <summary>
	/// Extended ValidatorStatus for CatalogValidation with BatchId support.
	/// </summary>
	public class CatalogValidatorStatus : ValidatorStatus
	{
		/// <summary>
		/// The ID of the batch of packages being validated.
		/// </summary>
		public string BatchId { get; set; }
	}
}

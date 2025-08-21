// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
	/// <summary>
	/// Service for managing sponsorship links for packages.
	/// </summary>
	public interface ISponsorshipLinksService
	{
		/// <summary>
		/// Validates a sponsorship URL using the same validation logic as PackageHelper.
		/// </summary>
		/// <param name="url">The URL to validate</param>
		/// <param name="validatedUrl">The validated and normalized URL if valid</param>
		/// <param name="errorMessage">Error message if validation fails</param>
		/// <returns>True if URL is valid, false otherwise</returns>
		bool ValidateUrl(string url, out string validatedUrl, out string errorMessage);

		/// <summary>
		/// Gets the validated sponsorship URLs for a package registration.
		/// </summary>
		/// <param name="packageRegistration">The package registration</param>
		/// <returns>Collection of validated sponsorship URLs</returns>
		IReadOnlyCollection<string> GetSponsorshipUrls(PackageRegistration packageRegistration);

		/// <summary>
		/// Updates the sponsorship URLs for a package registration.
		/// </summary>
		/// <param name="packageRegistration">The package registration to update</param>
		/// <param name="urls">The new list of sponsorship URLs</param>
		/// <returns>True if all URLs were valid and saved, false otherwise</returns>
		bool UpdateSponsorshipUrls(PackageRegistration packageRegistration, IEnumerable<string> urls);
	}
}

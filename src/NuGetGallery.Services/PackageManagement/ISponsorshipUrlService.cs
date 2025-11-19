// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Services;

namespace NuGetGallery
{
	/// <summary>
	/// Service for managing sponsorship URLs for packages.
	/// </summary>
	public interface ISponsorshipUrlService
	{
		/// <summary>
		/// Gets the trusted sponsorship domains configuration
		/// </summary>
		ITrustedSponsorshipDomains TrustedSponsorshipDomains { get; }

		/// <summary>
		/// Gets all sponsorship URL entries with validation status.
		/// </summary>
		/// <param name="packageRegistration"></param>
		/// <returns>Read-only collection of all sponsorship URL entries</returns>
		IReadOnlyCollection<SponsorshipUrlEntry> GetSponsorshipUrlEntries(PackageRegistration packageRegistration);

		/// <summary>
		/// Adds a sponsorship URL to a package registration and saves changes to the database.
		/// </summary>
		/// <param name="packageRegistration">The package registration to update</param>
		/// <param name="url">The URL to add</param>
		/// <param name="user">The user performing the action</param>
		/// <returns>The validated and normalized URL that was added</returns>
		Task<string> AddSponsorshipUrlAsync(PackageRegistration packageRegistration, string url, User user);

		/// <summary>
		/// Removes a sponsorship URL from a package registration and saves changes to the database.
		/// </summary>
		/// <param name="packageRegistration">The package registration to update</param>
		/// <param name="url">The URL to remove</param>
		/// <param name="user">The user performing the action</param>
		Task RemoveSponsorshipUrlAsync(PackageRegistration packageRegistration, string url, User user);
	}
}

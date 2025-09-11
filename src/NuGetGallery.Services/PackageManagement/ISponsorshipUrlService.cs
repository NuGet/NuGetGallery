// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
	/// <summary>
	/// Service for managing sponsorship URLs for packages.
	/// </summary>
	public interface ISponsorshipUrlService
	{
		/// <summary>
		/// Gets domain-validated sponsorship URLs
		/// </summary>
		/// <param name="packageRegistration"></param>
		/// <returns>Read-only collection of domain-validated sponsorship URLs</returns>
		IReadOnlyCollection<string> GetAcceptedSponsorshipUrls(PackageRegistration packageRegistration);

		/// <summary>
		/// Gets all sponsorship URL entries
		/// </summary>
		/// <param name="packageRegistration"></param>
		/// <returns>Read-only collection of all sponsorship URL entries</returns>
		IReadOnlyCollection<SponsorshipUrlEntry> GetSponsorshipUrlEntries(PackageRegistration packageRegistration);

		/// <summary>
		/// Adds a sponsorship URL to a package registration and saves changes to the database.
		/// </summary>
		/// <param name="packageRegistration">The package registration to update</param>
		/// <param name="url">The URL to add</param>
		Task AddSponsorshipUrlAsync(PackageRegistration packageRegistration, string url);

		/// <summary>
		/// Removes a sponsorship URL from a package registration and saves changes to the database.
		/// </summary>
		/// <param name="packageRegistration">The package registration to update</param>
		/// <param name="url">The URL to remove</param>
		Task RemoveSponsorshipUrlAsync(PackageRegistration packageRegistration, string url);
	}
}

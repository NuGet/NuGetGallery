// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using Newtonsoft.Json;

namespace NuGetGallery
{
	/// <summary>
	/// Service for managing sponsorship URLs for packages.
	/// </summary>
	public class SponsorshipUrlService : ISponsorshipUrlService
	{
		private readonly IEntitiesContext _entitiesContext;

		public SponsorshipUrlService(IEntitiesContext entitiesContext)
		{
			_entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
		}

		public IReadOnlyCollection<string> GetAcceptedSponsorshipUrls(PackageRegistration packageRegistration)
		{
			var sponsorshipUrlEntries = GetSponsorshipUrlEntriesInternal(packageRegistration);
			var acceptedUrls = sponsorshipUrlEntries
				.Where(entry => entry.IsDomainAccepted)
				.Select(entry => entry.Url)
				.ToList()
				.AsReadOnly();

			return acceptedUrls;
		}

		public IReadOnlyCollection<SponsorshipUrlEntry> GetSponsorshipUrlEntries(PackageRegistration packageRegistration)
		{
			return GetSponsorshipUrlEntriesInternal(packageRegistration);
		}

		public async Task AddSponsorshipUrlAsync(PackageRegistration packageRegistration, string url)
		{
			AddSponsorshipUrlInternal(packageRegistration, url);

			// Save changes to database
			await _entitiesContext.SaveChangesAsync();
		}

		public async Task RemoveSponsorshipUrlAsync(PackageRegistration packageRegistration, string url)
		{
			RemoveSponsorshipUrlInternal(packageRegistration, url);

			// Save changes to database
			await _entitiesContext.SaveChangesAsync();
		}

		private void AddSponsorshipUrlInternal(PackageRegistration packageRegistration, string url)
		{
			if (packageRegistration == null)
			{
				throw new ArgumentNullException(nameof(packageRegistration));
			}

			// Validate URL format and domain acceptance
			if (!PackageHelper.ValidateSponsorshipUrl(url, out string validatedUrl, out string errorMessage))
			{
				throw new ArgumentException(errorMessage);
			}

			var existingEntries = GetSponsorshipUrlEntriesInternal(packageRegistration).ToList();

			// Add new URL entry (only persist URL and timestamp, not isDomainAccepted)
			var newEntry = new { Url = validatedUrl, Timestamp = DateTime.UtcNow };
			var entriesToPersist = existingEntries.Select(e => new { e.Url, e.Timestamp }).ToList();
			entriesToPersist.Add(newEntry);

			// Serialize back to JSON
			packageRegistration.SponsorshipUrls = JsonConvert.SerializeObject(entriesToPersist);
		}

		private void RemoveSponsorshipUrlInternal(PackageRegistration packageRegistration, string url)
		{
			if (packageRegistration == null)
			{
				throw new ArgumentNullException(nameof(packageRegistration));
			}

			if (string.IsNullOrWhiteSpace(url))
			{
				throw new ArgumentException("URL cannot be null or empty", nameof(url));
			}

			var existingEntries = GetSponsorshipUrlEntriesInternal(packageRegistration).ToList();
			
			// Find and remove the URL entry
			var entryToRemove = existingEntries.FirstOrDefault(entry => 
				string.Equals(entry.Url, url, StringComparison.OrdinalIgnoreCase));

			if (entryToRemove == null)
			{
				throw new ArgumentException("The specified sponsorship URL was not found for this package.", nameof(url));
			}

			existingEntries.Remove(entryToRemove);
			
			// Serialize back to JSON (only persist URL and timestamp, not isDomainAccepted)
			var entriesToPersist = existingEntries.Select(e => new { e.Url, e.Timestamp }).ToList();
			packageRegistration.SponsorshipUrls = entriesToPersist.Any() 
				? JsonConvert.SerializeObject(entriesToPersist)
				: null;
		}

		/// <summary>
		/// Internal method to deserialize and validate sponsorship URL entries from JSON.
		/// </summary>
		private IReadOnlyCollection<SponsorshipUrlEntry> GetSponsorshipUrlEntriesInternal(PackageRegistration packageRegistration)
		{
			var sponsorshipUrlEntries = new List<SponsorshipUrlEntry>();

			if (packageRegistration?.SponsorshipUrls != null && !string.IsNullOrEmpty(packageRegistration.SponsorshipUrls))
			{
				try
				{
					// Deserialize as JSON array of URL objects
					var urlEntries = JsonConvert.DeserializeObject<List<SponsorshipUrlEntry>>(packageRegistration.SponsorshipUrls);
					if (urlEntries != null)
					{
						// Validate all URLs and populate domain acceptance
						for (int i = 0; i < urlEntries.Count; i++)
						{
							var entry = urlEntries[i];
							if (entry != null && !string.IsNullOrWhiteSpace(entry.Url))
							{
								if (PackageHelper.TryPrepareUrlForRendering(entry.Url, out string validatedUrl))
								{
									// Always populate IsDomainAccepted during deserialization
									var isDomainAccepted = PackageHelper.IsAcceptedSponsorshipDomain(validatedUrl);
									sponsorshipUrlEntries.Add(new SponsorshipUrlEntry(validatedUrl, entry.Timestamp, isDomainAccepted));
								}
								else
								{
									// Log invalid URL but continue processing other URLs
									System.Diagnostics.Trace.TraceWarning($"Invalid sponsorship URL at index {i} for package registration {packageRegistration?.Id}: '{entry.Url}'");
								}
							}
							else
							{
								// Log null or empty URL entry
								System.Diagnostics.Trace.TraceWarning($"Null or empty sponsorship URL entry at index {i} for package registration {packageRegistration?.Id}");
							}
						}
					}
				}
				catch (JsonException ex)
				{
					// If JSON parsing fails, log the error
					System.Diagnostics.Trace.TraceWarning($"Failed to parse sponsorship URLs for package registration {packageRegistration?.Id}: {ex.Message}");
				}
			}

			return sponsorshipUrlEntries.AsReadOnly();
		}
	}
}

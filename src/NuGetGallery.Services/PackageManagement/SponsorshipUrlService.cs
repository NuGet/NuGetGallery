// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using Newtonsoft.Json;
using NuGetGallery.Auditing;
using NuGetGallery.Services;

namespace NuGetGallery
{
	/// <summary>
	/// Service for managing sponsorship URLs for packages.
	/// </summary>
	public class SponsorshipUrlService : ISponsorshipUrlService
	{
		private readonly IEntitiesContext _entitiesContext;
		private readonly IContentObjectService _contentObjectService;
		private readonly IAuditingService _auditingService;

		public ITrustedSponsorshipDomains TrustedSponsorshipDomains => _contentObjectService.TrustedSponsorshipDomains;

		public SponsorshipUrlService(IEntitiesContext entitiesContext, IContentObjectService contentObjectService, IAuditingService auditingService)
		{
			_entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
			_contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
			_auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
		}

		public IReadOnlyCollection<SponsorshipUrlEntry> GetSponsorshipUrlEntries(PackageRegistration packageRegistration)
		{
			return GetSponsorshipUrlEntriesInternal(packageRegistration);
		}

	public async Task<string> AddSponsorshipUrlAsync(PackageRegistration packageRegistration, string url, User user)
	{
		// Validate required parameters before any DB interaction
		if (packageRegistration == null)
		{
			throw new ArgumentNullException(nameof(packageRegistration));
		}
		if (user == null)
		{
			throw new ArgumentNullException(nameof(user));
		}

		// Validate URL format and domain acceptance
		if (!PackageHelper.ValidateSponsorshipUrl(url, _contentObjectService.TrustedSponsorshipDomains, out string validatedUrl, out string errorMessage))
		{
			throw new ArgumentException(errorMessage);
		}

		// Get max links limit before starting transaction
		var maxLinks = _contentObjectService.TrustedSponsorshipDomains.MaxSponsorshipLinks;

		using (new SuspendDbExecutionStrategy())
		using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
		{
			var existingEntries = GetSponsorshipUrlEntriesInternal(packageRegistration).ToList();

			// Check URL count limit
			if (existingEntries.Count >= maxLinks)
			{
				throw new ArgumentException($"You can add a maximum of {maxLinks} sponsorship links.");
			}

				// Capture timestamp once for consistency between database and audit
				var timestamp = DateTime.UtcNow;

				var newEntry = new { Url = validatedUrl, Timestamp = timestamp };
				var entriesToPersist = existingEntries.Select(e => new { e.Url, e.Timestamp }).ToList();
				entriesToPersist.Add(newEntry);

				// Serialize back to JSON
				packageRegistration.SponsorshipUrls = JsonConvert.SerializeObject(entriesToPersist);

				// Save changes to database
				await _entitiesContext.SaveChangesAsync();

				// Create audit record with the same timestamp used in database
				await _auditingService.SaveAuditRecordAsync(
					PackageRegistrationAuditRecord.CreateForAddSponsorshipUrl(packageRegistration, validatedUrl, user.Username, user.IsAdministrator, timestamp));

				transaction.Commit();

				return validatedUrl;
			}
		}

	public async Task RemoveSponsorshipUrlAsync(PackageRegistration packageRegistration, string url, User user)
	{
		// Validate required parameters before any DB interaction
		if (packageRegistration == null)
		{
			throw new ArgumentNullException(nameof(packageRegistration));
		}
		if (user == null)
		{
			throw new ArgumentNullException(nameof(user));
		}
		if (string.IsNullOrWhiteSpace(url))
		{
			throw new ArgumentException("URL cannot be null or empty", nameof(url));
		}

		using (new SuspendDbExecutionStrategy())
		using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
		{
			var existingEntries = GetSponsorshipUrlEntriesInternal(packageRegistration).ToList();

			// Find and remove the URL entry
			var entryToRemove = existingEntries.FirstOrDefault(entry => 
				string.Equals(entry.Url, url, StringComparison.OrdinalIgnoreCase));

			if (entryToRemove == null)
			{
				throw new ArgumentException("The specified sponsorship URL was not found for this package.", nameof(url));
			}

			// Capture removal timestamp for consistency between database and audit
			var removalTimestamp = DateTime.UtcNow;

			existingEntries.Remove(entryToRemove);
			
			// Serialize back to JSON
			var entriesToPersist = existingEntries.Select(e => new { e.Url, e.Timestamp }).ToList();
			packageRegistration.SponsorshipUrls = entriesToPersist.Any() 
				? JsonConvert.SerializeObject(entriesToPersist)
				: null;

			// Save changes to database
			await _entitiesContext.SaveChangesAsync();

			// Create audit record with the same timestamp used for removal processing
			await _auditingService.SaveAuditRecordAsync(
				PackageRegistrationAuditRecord.CreateForRemoveSponsorshipUrl(packageRegistration, url, user.Username, user.IsAdministrator, removalTimestamp));

			transaction.Commit();
		}
	}

		/// <summary>
		/// Internal method to deserialize and validate sponsorship URL entries from JSON.
		/// </summary>
		private IReadOnlyCollection<SponsorshipUrlEntry> GetSponsorshipUrlEntriesInternal(PackageRegistration packageRegistration)
		{
			var sponsorshipUrlEntries = new List<SponsorshipUrlEntry>();

			if (!string.IsNullOrEmpty(packageRegistration?.SponsorshipUrls))
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
									// Use comprehensive domain and path validation from PackageHelper
									var isDomainAccepted = PackageHelper.IsAcceptedSponsorshipDomain(validatedUrl, _contentObjectService.TrustedSponsorshipDomains);
									sponsorshipUrlEntries.Add(new SponsorshipUrlEntry 
									{ 
										Url = validatedUrl, 
										Timestamp = entry.Timestamp, 
										IsDomainAccepted = isDomainAccepted 
									});
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

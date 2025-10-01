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
		var debugMessages = new List<string>();
		debugMessages.Add($"[DEBUG] AddSponsorshipUrlAsync starting for package: {packageRegistration?.Id}, URL: {url}, User: {user?.Username}");
		
		// Validate required parameters before any DB interaction
		if (packageRegistration == null)
		{
			debugMessages.Add("[DEBUG] ArgumentNullException: packageRegistration is null");
			throw new ArgumentNullException(nameof(packageRegistration));
		}
		if (user == null)
		{
			debugMessages.Add("[DEBUG] ArgumentNullException: user is null");
			throw new ArgumentNullException(nameof(user));
		}

		debugMessages.Add("[DEBUG] Accessing TrustedSponsorshipDomains...");
		
		var trustedDomains = _contentObjectService.TrustedSponsorshipDomains;
		debugMessages.Add($"[DEBUG] TrustedSponsorshipDomains - MaxLinks: {trustedDomains?.MaxSponsorshipLinks}");
		
		// Display the actual domains loaded from configuration
		if (trustedDomains is TrustedSponsorshipDomains concreteDomains && concreteDomains.TrustedSponsorshipDomainList != null)
		{
			debugMessages.Add($"[DEBUG] Configured domains count: {concreteDomains.TrustedSponsorshipDomainList.Count}");
			debugMessages.Add($"[DEBUG] Configured domains: {string.Join(", ", concreteDomains.TrustedSponsorshipDomainList)}");
		}
		else
		{
			debugMessages.Add("[DEBUG] Unable to access domain list - service may not be TrustedSponsorshipDomains type or list is null");
		}

		try
		{
			// Validate URL format and domain acceptance
			if (!PackageHelper.ValidateSponsorshipUrl(url, _contentObjectService.TrustedSponsorshipDomains, out string validatedUrl, out string errorMessage))
			{
				debugMessages.Add($"[DEBUG] URL validation failed: {errorMessage}");
				// Create a detailed error message that includes debug info
				var detailedError = $"{errorMessage}\n\nDebug Info:\n{string.Join("\n", debugMessages)}";
				throw new ArgumentException(detailedError);
			}

			// Get max links limit before starting transaction
			var maxLinks = _contentObjectService.TrustedSponsorshipDomains.MaxSponsorshipLinks;
			debugMessages.Add($"[DEBUG] Max links allowed: {maxLinks}");

			// Read existing entries before starting transaction (no DB access needed)
			var existingEntries = GetSponsorshipUrlEntriesInternal(packageRegistration).ToList();
			debugMessages.Add($"[DEBUG] Existing entries count: {existingEntries.Count}");

			// Check URL count limit
			if (existingEntries.Count >= maxLinks)
			{
				debugMessages.Add($"[DEBUG] Max links exceeded: {existingEntries.Count} >= {maxLinks}");
				throw new ArgumentException($"You can add a maximum of {maxLinks} sponsorship links.");
			}

			// Capture timestamp once for consistency between database and audit
			var timestamp = DateTime.UtcNow;

			// Prepare new entry and JSON for persistence
			var newEntry = new { Url = validatedUrl, Timestamp = timestamp };
			var entriesToPersist = existingEntries.Select(e => new { e.Url, e.Timestamp }).ToList();
			entriesToPersist.Add(newEntry);
			var newSponsorshipUrlsJson = JsonConvert.SerializeObject(entriesToPersist);

			debugMessages.Add("[DEBUG] Starting database transaction...");
			using (new SuspendDbExecutionStrategy())
			using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
			{
				try
				{
					// Only modify the entity property inside the transaction
					packageRegistration.SponsorshipUrls = newSponsorshipUrlsJson;
					debugMessages.Add("[DEBUG] Updated package registration with new JSON");

					// Save changes to database
					await _entitiesContext.SaveChangesAsync();
					debugMessages.Add("[DEBUG] Database changes saved successfully");

					// Create audit record with the same timestamp used in database
					debugMessages.Add("[DEBUG] Calling audit service...");
					await _auditingService.SaveAuditRecordAsync(
						PackageRegistrationAuditRecord.CreateForAddSponsorshipUrl(packageRegistration, validatedUrl, user.Username, user.IsAdministrator, timestamp));
					debugMessages.Add("[DEBUG] Audit record saved successfully");

					transaction.Commit();
					debugMessages.Add("[DEBUG] Transaction committed successfully");

					return validatedUrl;
				}
				catch (Exception ex)
				{
					debugMessages.Add($"[ERROR] Exception during transaction: {ex.GetType().Name}: {ex.Message}");
					debugMessages.Add($"[ERROR] Stack trace: {ex.StackTrace}");
					if (ex.InnerException != null)
					{
						debugMessages.Add($"[ERROR] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
					}
					throw;
				}
			}
		}
		catch (Exception ex)
		{
			debugMessages.Add($"[ERROR] Exception in AddSponsorshipUrlAsync: {ex.GetType().Name}: {ex.Message}");
			debugMessages.Add($"[ERROR] Stack trace: {ex.StackTrace}");
			if (ex.InnerException != null)
			{
				debugMessages.Add($"[ERROR] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
			}
			
			// Include debug info in exception message for visibility
			var detailedError = $"{ex.Message}\n\nDebug Info:\n{string.Join("\n", debugMessages)}";
			throw new Exception(detailedError, ex);
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

		// Read existing entries before starting transaction (no DB access needed)
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

		// Remove the entry from in-memory list
		existingEntries.Remove(entryToRemove);
		
		// Prepare JSON for persistence
		var entriesToPersist = existingEntries.Select(e => new { e.Url, e.Timestamp }).ToList();
		var newSponsorshipUrlsJson = entriesToPersist.Any() 
			? JsonConvert.SerializeObject(entriesToPersist)
			: null;

		using (new SuspendDbExecutionStrategy())
		using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
		{
			// Only modify the entity property inside the transaction
			packageRegistration.SponsorshipUrls = newSponsorshipUrlsJson;

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

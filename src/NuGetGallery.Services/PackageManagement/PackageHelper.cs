// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;
using Newtonsoft.Json;

namespace NuGetGallery
{
	public static class PackageHelper
	{
		public static string ParseTags(string tags)
		{
			if (tags == null)
			{
				return null;
			}
			return tags.Replace(',', ' ').Replace(';', ' ').Replace('\t', ' ').Replace("  ", " ");
		}

		public static bool ShouldRenderUrl(string url, bool secureOnly = false)
		{
			if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
			{
				if (secureOnly)
				{
					return uri.IsHttpsProtocol();
				}

				return uri.Scheme == Uri.UriSchemeHttps
					|| uri.Scheme == Uri.UriSchemeHttp;
			}

			return false;
		}

		/// <summary>
		/// If the input uri is http => check if it's a known domain and convert to https.
		/// If the input uri is https => leave as is
		/// If the input uri is not a valid uri or not http/https => return false
		/// </summary>
		public static bool TryPrepareUrlForRendering(string uriString, out string readyUriString, bool rewriteAllHttp = false)
		{
			Uri returnUri = null;
			readyUriString = null;

			if (Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
			{
				if (uri.IsHttpProtocol())
				{
					if (rewriteAllHttp || uri.IsDomainWithHttpsSupport())
					{
						returnUri = uri.ToHttps();
					}
					else
					{
						returnUri = uri;
					}
				}
				else if (uri.IsHttpsProtocol() || uri.IsHttpProtocol())
				{
					returnUri = uri;
				}
			}

			if (returnUri != null)
			{
				readyUriString = returnUri.AbsoluteUri;
				return true;
			}

			return false;
		}

		/// <summary>
		/// Fetches and domain validates sponsorship URLs from PackageRegistration.SponsorshipUrls field
		/// </summary>
		/// <param name="packageRegistration"></param>
		/// <returns>Read-only collection of validated sponsorship URLs from accepted domains</returns>
		public static IReadOnlyCollection<string> GetAcceptedSponsorshipUrls(PackageRegistration packageRegistration)
		{
			var sponsorshipUrlEntries = GetSponsorshipUrlEntries(packageRegistration);
			return sponsorshipUrlEntries
				.Where(entry => entry.IsDomainAccepted)
				.Select(entry => entry.Url)
				.ToList()
				.AsReadOnly();
		}

		/// <summary>
		/// Deserializes and validates sponsorship URL entries from JSON.
		/// Returns a read-only collection of validated URL entries with domain acceptance populated.
		/// </summary>
		/// <param name="packageRegistration"></param>
		/// <returns>Read-only collection of validated sponsorship URL entries</returns>
		public static IReadOnlyCollection<SponsorshipUrlEntry> GetSponsorshipUrlEntries(PackageRegistration packageRegistration)
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
								if (TryPrepareUrlForRendering(entry.Url, out string validatedUrl))
								{
									// Always populate IsDomainAccepted during deserialization
									var isDomainAccepted = IsAcceptedSponsorshipDomain(validatedUrl);
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

		/// <summary>
		/// Checks if a URL belongs to an accepted sponsorship domain.
		/// </summary>
		/// <param name="url">The URL to check</param>
		/// <returns>True if the URL is from an accepted sponsorship domain</returns>
		public static bool IsAcceptedSponsorshipDomain(string url)
		{
			if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
			{
				return false;
			}

			var hostname = uri.Host.ToLowerInvariant();
			var acceptedDomains = new[] {
				"github.com", "www.github.com",
				"patreon.com", "www.patreon.com", 
				"opencollective.com", "www.opencollective.com",
				"ko-fi.com", "www.ko-fi.com",
				"tidelift.com", "www.tidelift.com",
				"liberapay.com", "www.liberapay.com"
			};

			return acceptedDomains.Contains(hostname);
		}

		public static bool IsGitRepositoryType(string repositoryType)
		{
			return ServicesConstants.GitRepository.Equals(repositoryType, StringComparison.OrdinalIgnoreCase);
		}

		public static void ValidateNuGetPackageMetadata(PackageMetadata packageMetadata)
		{
			// TODO: Change this to use DataAnnotations
			if (packageMetadata.Id.Length > NuGet.Services.Entities.Constants.MaxPackageIdLength)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "ID", NuGet.Services.Entities.Constants.MaxPackageIdLength);
			}
			if (packageMetadata.Authors != null && packageMetadata.Authors.Flatten().Length > 4000)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "Authors", "4000");
			}
			if (packageMetadata.Copyright != null && packageMetadata.Copyright.Length > 4000)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "Copyright", "4000");
			}
			if (packageMetadata.Description == null)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyMissing, "Description");
			}
			else if (packageMetadata.Description != null && packageMetadata.Description.Length > 4000)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "Description", "4000");
			}
			if (packageMetadata.IconUrl != null && packageMetadata.IconUrl.AbsoluteUri.Length > 4000)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "IconUrl", "4000");
			}
			if (packageMetadata.LicenseUrl != null && packageMetadata.LicenseUrl.AbsoluteUri.Length > 4000)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "LicenseUrl", "4000");
			}
			if (packageMetadata.ProjectUrl != null && packageMetadata.ProjectUrl.AbsoluteUri.Length > 4000)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "ProjectUrl", "4000");
			}
			if (packageMetadata.Summary != null && packageMetadata.Summary.Length > 4000)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "Summary", "4000");
			}
			if (packageMetadata.ReleaseNotes != null && packageMetadata.ReleaseNotes.Length > 35000)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "ReleaseNotes", "35000");
			}
			if (packageMetadata.Tags != null && packageMetadata.Tags.Length > 4000)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "Tags", "4000");
			}
			if (packageMetadata.Title != null && packageMetadata.Title.Length > 256)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "Title", "256");
			}

			if (packageMetadata.Version != null && packageMetadata.Version.ToFullString().Length > 64)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "Version", "64");
			}

			if (packageMetadata.Language != null && packageMetadata.Language.Length > 20)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "Language", "20");
			}

			if (packageMetadata.Id.Length + (packageMetadata.Version?.ToFullString().Length ?? 0) > 160)
			{
				throw new EntityException(ServicesStrings.NuGetPackageIdVersionCombinedTooLong, "160");
			}

			// Validate dependencies
			if (packageMetadata.GetDependencyGroups() != null)
			{
				var packageDependencies = packageMetadata.GetDependencyGroups().ToList();

				foreach (var dependency in packageDependencies.SelectMany(s => s.Packages))
				{
					// NuGet.Core compatibility - dependency package id can not be > 128 characters
					if (dependency.Id != null && dependency.Id.Length > NuGet.Services.Entities.Constants.MaxPackageIdLength)
					{
						throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "Dependency.Id", NuGet.Services.Entities.Constants.MaxPackageIdLength);
					}

					// NuGet.Core compatibility - dependency versionspec can not be > 256 characters
					if (dependency.VersionRange != null && dependency.VersionRange.ToString().Length > 256)
					{
						throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "Dependency.VersionSpec", "256");
					}
				}

				// NuGet.Core compatibility - flattened dependencies should be <= Int16.MaxValue
				if (packageDependencies.Flatten().Length > Int16.MaxValue)
				{
					throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "Dependencies", Int16.MaxValue);
				}

				// Verify there are no duplicate dependency groups
				if (packageDependencies.Select(pd => pd.TargetFramework).Distinct().Count() != packageDependencies.Count)
				{
					throw new EntityException(ServicesStrings.NuGetPackageDuplicateDependencyGroup);
				}
			}

			// Validate repository metadata	
			if (packageMetadata.RepositoryType != null && packageMetadata.RepositoryType.Length > 100)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "RepositoryType", "100");
			}

			if (packageMetadata.RepositoryUrl != null && packageMetadata.RepositoryUrl.AbsoluteUri.Length > 4000)
			{
				throw new EntityException(ServicesStrings.NuGetPackagePropertyTooLong, "RepositoryUrl", "4000");
			}
		}

		public static string GetSelectListText(Package package)
		{
			var tags = new List<string>();
			if (package.IsLatestSemVer2)
			{
				tags.Add("Latest");
			}

			var deprecation = package.Deprecations.SingleOrDefault();
			if (deprecation != null)
			{
				var deprecationReasons = new List<string>();

				if (deprecation.Status.HasFlag(PackageDeprecationStatus.Legacy))
				{
					deprecationReasons.Add("Legacy");
				}

				if (deprecation.Status.HasFlag(PackageDeprecationStatus.CriticalBugs))
				{
					deprecationReasons.Add("Critical Bugs");
				}

				if (deprecation.Status.HasFlag(PackageDeprecationStatus.Other))
				{
					deprecationReasons.Add("Other");
				}

				if (deprecationReasons.Any())
				{
					tags.Add("Deprecated - " + string.Join(", ", deprecationReasons));
				}
			}

			return NuGetVersionFormatter.ToFullString(package.Version) + (tags.Any() ? $" ({string.Join(", ", tags)})" : string.Empty);
		}
	}
}

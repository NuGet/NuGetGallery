// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using Newtonsoft.Json;

namespace NuGetGallery
{
	public class SponsorshipLinksService : ISponsorshipLinksService
	{
		public bool ValidateUrl(string url, out string validatedUrl, out string errorMessage)
		{
			validatedUrl = null;
			errorMessage = null;

			if (string.IsNullOrWhiteSpace(url))
			{
				errorMessage = "URL is required.";
				return false;
			}

			// Use PackageHelper for URL validation - this ensures consistency with other URL validation in the system
			if (PackageHelper.TryPrepareUrlForRendering(url, out validatedUrl))
			{
				return true;
			}
			else
			{
				errorMessage = "The provided URL is not valid or uses an unsupported protocol. Please use HTTP or HTTPS URLs.";
				return false;
			}
		}

		public IReadOnlyCollection<string> GetSponsorshipUrls(PackageRegistration packageRegistration)
		{
			return PackageHelper.GetValidatedSponsorshipUrls(packageRegistration);
		}

		public bool UpdateSponsorshipUrls(PackageRegistration packageRegistration, IEnumerable<string> urls)
		{
			if (packageRegistration == null)
			{
				throw new ArgumentNullException(nameof(packageRegistration));
			}

			if (urls == null)
			{
				urls = Enumerable.Empty<string>();
			}

			var validatedUrls = new List<string>();

			// Validate all URLs before saving any
			foreach (var url in urls.Where(u => !string.IsNullOrWhiteSpace(u)))
			{
				if (ValidateUrl(url, out string validatedUrl, out string errorMessage))
				{
					validatedUrls.Add(validatedUrl);
				}
				else
				{
					// If any URL is invalid, don't save any changes
					return false;
				}
			}

			// All URLs are valid, save them as JSON
			try
			{
				packageRegistration.SponsorshipUrls = validatedUrls.Any() 
					? JsonConvert.SerializeObject(validatedUrls) 
					: null;
				return true;
			}
			catch (JsonException)
			{
				return false;
			}
		}
	}
}

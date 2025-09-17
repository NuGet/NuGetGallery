// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NuGetGallery.Services
{
	public class TrustedSponsorshipDomains : ITrustedSponsorshipDomains
	{
		public HashSet<string> TrustedSponsorshipDomainList { get; }
		public int MaxSponsorshipLinks { get; }

		[JsonConstructor]
		public TrustedSponsorshipDomains(IEnumerable<string> trustedSponsorshipDomainList, int maxSponsorshipLinks)
		{
			// Handle null by treating it as an empty list for JSON deserialization compatibility
			trustedSponsorshipDomainList = trustedSponsorshipDomainList ?? Enumerable.Empty<string>();

			var trustedSponsorshipDomainListFromFile = new HashSet<string>(trustedSponsorshipDomainList, StringComparer.OrdinalIgnoreCase);
			TrustedSponsorshipDomainList = expandDomainList(trustedSponsorshipDomainListFromFile);
			MaxSponsorshipLinks = maxSponsorshipLinks;
		}

		public bool IsSponsorshipDomainTrusted(string sponsorshipDomain)
		{
			if (sponsorshipDomain == null)
			{
				return false;
			}

			return TrustedSponsorshipDomainList.Contains(sponsorshipDomain);
		}

		private HashSet<string> expandDomainList(HashSet<string> trustedSponsorshipDomainListFromFile)
		{
			var expandedSponsorshipDomainList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var sponsorshipDomain in trustedSponsorshipDomainListFromFile)
			{
				expandedSponsorshipDomainList.Add(sponsorshipDomain);

				var subdomain = ParseSubDomain(sponsorshipDomain);

				if (string.IsNullOrEmpty(subdomain))
				{
					expandedSponsorshipDomainList.Add("www." + sponsorshipDomain);
				} 
				else if (subdomain == "www")
				{
					expandedSponsorshipDomainList.Add(sponsorshipDomain.Substring(subdomain.Length + 1));
				}
			}
			return expandedSponsorshipDomainList;
		}

		private string ParseSubDomain(string domain)
		{
			if (domain.Split('.').Length > 2)
			{
				var lastIndex = domain.LastIndexOf(".");
				var index = domain.LastIndexOf('.', lastIndex - 1);

				return domain.Substring(0, index);
			}

			return null; 
		}
	}
}

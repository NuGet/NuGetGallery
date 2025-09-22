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

		public TrustedSponsorshipDomains()
			: this(trustedSponsorshipDomainList: Enumerable.Empty<string>(), maxSponsorshipLinks: 0)
		{
		}

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
			if (string.IsNullOrEmpty(sponsorshipDomain))
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
				if (string.IsNullOrWhiteSpace(sponsorshipDomain))
					continue;

				var trimmedDomain = sponsorshipDomain.Trim().ToLowerInvariant();

				// Add both the domain and its www variant
				expandedSponsorshipDomainList.Add(trimmedDomain);
				expandedSponsorshipDomainList.Add("www." + trimmedDomain);
			}
			return expandedSponsorshipDomainList;
		}
	}
}

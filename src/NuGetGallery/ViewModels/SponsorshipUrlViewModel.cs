// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
	/// <summary>
	/// View model for displaying sponsorship URLs
	/// </summary>
	public class SponsorshipUrlViewModel
	{
		/// <summary>
		/// The sponsorship URL
		/// </summary>
		public string Url { get; set; }

		/// <summary>
		/// Whether this URL is from an accepted sponsorship domain
		/// </summary>
		public bool IsDomainAccepted { get; set; }

		/// <summary>
		/// Creates a new SponsorshipUrlViewModel
		/// </summary>
		/// <param name="url">The sponsorship URL</param>
		/// <param name="isDomainAccepted">Whether the URL is from an accepted domain</param>
		public SponsorshipUrlViewModel(string url, bool isDomainAccepted = true)
		{
			Url = url;
			IsDomainAccepted = isDomainAccepted;
		}
	}
}

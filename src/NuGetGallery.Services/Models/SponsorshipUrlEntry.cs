// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGetGallery
{
	/// <summary>
	/// Represents a sponsorship URL entry with timestamp and optional compliance information
	/// </summary>
	public class SponsorshipUrlEntry
	{
		/// <summary>
		/// The sponsorship URL
		/// </summary>
		[JsonProperty("url")]
		public string Url { get; set; }

	/// <summary>
	/// The timestamp when this URL was added (UTC)
	/// </summary>
	[JsonProperty("timestamp")]
	public DateTime Timestamp { get; set; }

	/// <summary>
	/// Whether this URL is from an accepted sponsorship domain.
	/// This property is always populated during deserialization.
	/// </summary>
	[JsonProperty("isDomainAccepted")]
	public bool IsDomainAccepted { get; set; }

	/// <summary>
	/// Creates a new SponsorshipUrlEntry with domain acceptance information
	/// </summary>
	/// <param name="url">The sponsorship URL</param>
	/// <param name="timestamp">The timestamp when this URL was added</param>
	/// <param name="isDomainAccepted">Whether the URL is from an accepted domain</param>
	public SponsorshipUrlEntry(string url, DateTime timestamp, bool isDomainAccepted)
	{
		Url = url;
		Timestamp = timestamp;
		IsDomainAccepted = isDomainAccepted;
	}

	/// <summary>
	/// Creates a new SponsorshipUrlEntry with current timestamp
	/// </summary>
	/// <param name="url">The sponsorship URL</param>
	/// <param name="isDomainAccepted">Whether the URL is from an accepted domain</param>
	public SponsorshipUrlEntry(string url, bool isDomainAccepted) : this(url, DateTime.UtcNow, isDomainAccepted)
	{
	}		/// <summary>
		/// Parameterless constructor for JSON deserialization
		/// </summary>
		[JsonConstructor]
		public SponsorshipUrlEntry()
		{
		}
	}
}

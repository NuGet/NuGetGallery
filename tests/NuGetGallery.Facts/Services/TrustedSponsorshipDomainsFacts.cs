// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;
using NuGetGallery.Services;
using Xunit;

namespace NuGetGallery.Services
{
	public class TrustedSponsorshipDomainsFacts
	{
		public class TheConstructorMethod
		{
			[Fact]
			public void InitializesWithExplicitValues()
			{
				// Act
				var domains = new TrustedSponsorshipDomains(new List<string>(), 0);

				// Assert
				Assert.NotNull(domains.TrustedSponsorshipDomainList);
				Assert.Empty(domains.TrustedSponsorshipDomainList);
				Assert.Equal(0, domains.MaxSponsorshipLinks);
			}

		[Fact]
		public void HandlesNullDomainListCorrectly()
		{
			// Act
			var domains = new TrustedSponsorshipDomains(null, 5);

			// Assert
			Assert.NotNull(domains.TrustedSponsorshipDomainList);
			Assert.Empty(domains.TrustedSponsorshipDomainList);
			Assert.Equal(5, domains.MaxSponsorshipLinks);
		}			[Fact]
			public void AutomaticallyExpandsDomainsToIncludeWwwVariants()
			{
				// Arrange
				var domainList = new List<string> { "github.com", "example.org" };

				// Act
				var domains = new TrustedSponsorshipDomains(domainList, 10);

				// Assert
				Assert.Contains("github.com", domains.TrustedSponsorshipDomainList);
				Assert.Contains("www.github.com", domains.TrustedSponsorshipDomainList);
				Assert.Contains("example.org", domains.TrustedSponsorshipDomainList);
				Assert.Contains("www.example.org", domains.TrustedSponsorshipDomainList);
				Assert.Equal(4, domains.TrustedSponsorshipDomainList.Count);
			}

			[Fact]
			public void HandlesWwwDomainsCorrectlyByAddingNonWwwVariant()
			{
				// Arrange - Only non-www domains should be in configuration
				var domainList = new List<string> { "github.com", "example.org" };

				// Act
				var domains = new TrustedSponsorshipDomains(domainList, 10);

				// Assert - Both non-www and www variants should be available
				Assert.Contains("github.com", domains.TrustedSponsorshipDomainList);
				Assert.Contains("www.github.com", domains.TrustedSponsorshipDomainList);
				Assert.Contains("example.org", domains.TrustedSponsorshipDomainList);
				Assert.Contains("www.example.org", domains.TrustedSponsorshipDomainList);
				Assert.Equal(4, domains.TrustedSponsorshipDomainList.Count);
			}
		}

		public class TheIsSponsorshipDomainTrustedMethod
		{
			[Theory]
			[InlineData("github.com", true)]
			[InlineData("patreon.com", true)]
			[InlineData("opencollective.com", true)]
			[InlineData("ko-fi.com", true)]
			[InlineData("tidelift.com", true)]
			[InlineData("liberapay.com", true)]
			[InlineData("untrusted.com", false)]
			[InlineData("malicious.site", false)]
			public void ReturnsTrueForTrustedDomains(string domain, bool expectedResult)
			{
				// Arrange
				var trustedDomains = new List<string>
				{
					"github.com", "patreon.com", "opencollective.com", 
					"ko-fi.com", "tidelift.com", "liberapay.com"
				};
				var domains = new TrustedSponsorshipDomains(trustedDomains, 10);

				// Act
				var result = domains.IsSponsorshipDomainTrusted(domain);

				// Assert
				Assert.Equal(expectedResult, result);
			}

			[Theory]
			[InlineData("GITHUB.COM")]
			[InlineData("GitHub.Com")]
			[InlineData("PATREON.COM")]
			public void IsCaseInsensitive(string domain)
			{
				// Arrange
				var trustedDomains = new List<string> { "github.com", "patreon.com" };
				var domains = new TrustedSponsorshipDomains(trustedDomains, 10);

				// Act
				var result = domains.IsSponsorshipDomainTrusted(domain);

				// Assert
				Assert.True(result);
			}

			[Theory]
			[InlineData(null)]
			[InlineData("")]
			[InlineData("   ")]
			public void ReturnsFalseForNullOrEmptyDomain(string domain)
			{
				// Arrange
				var trustedDomains = new List<string> { "github.com" };
				var domains = new TrustedSponsorshipDomains(trustedDomains, 10);

				// Act
				var result = domains.IsSponsorshipDomainTrusted(domain);

				// Assert
				Assert.False(result);
			}

			[Fact]
			public void IsSponsorshipDomainTrustedHandlesWwwVariants()
		{
			// Arrange
			var trustedDomains = new List<string> { "github.com" }; // Only add github.com
			var domains = new TrustedSponsorshipDomains(trustedDomains, 10);

			// Act & Assert
			// Both original and www variant should be trusted due to automatic expansion
			Assert.True(domains.IsSponsorshipDomainTrusted("github.com"));
			Assert.True(domains.IsSponsorshipDomainTrusted("www.github.com"));
		}
	}

		public class TheJsonSerializationMethod
		{
			[Fact]
			public void SerializesAndDeserializesCorrectly()
			{
				// Arrange
				var originalDomains = new TrustedSponsorshipDomains(
					new List<string> { "github.com", "patreon.com" }, 
					5);

				// Act
				var json = JsonConvert.SerializeObject(originalDomains);
				var deserializedDomains = JsonConvert.DeserializeObject<TrustedSponsorshipDomains>(json);

				// Assert
				// Both objects should have the same expanded domain lists and behavior
				Assert.Equal(originalDomains.TrustedSponsorshipDomainList, deserializedDomains.TrustedSponsorshipDomainList);
				Assert.Equal(originalDomains.MaxSponsorshipLinks, deserializedDomains.MaxSponsorshipLinks);
			}
		
			[Fact]
			public void DeserializesWithZeroMaxLinksWhenMissing()
			{
				// Arrange
				var json = "{\"TrustedSponsorshipDomainList\": [\"github.com\", \"patreon.com\"]}";

				// Act
				var domains = JsonConvert.DeserializeObject<TrustedSponsorshipDomains>(json);

				// Assert
				// The implementation expands domains to include www. variants
				Assert.Equal(4, domains.TrustedSponsorshipDomainList.Count); // github.com, www.github.com, patreon.com, www.patreon.com
				Assert.Equal(0, domains.MaxSponsorshipLinks); // No default value, so it's 0
			}

			[Fact]
			public void DeserializesEmptyListWhenDomainListMissing()
			{
				// Arrange
				var json = "{\"MaxSponsorshipLinks\": 5}";

				// Act
				var domains = JsonConvert.DeserializeObject<TrustedSponsorshipDomains>(json);

				// Assert
				Assert.Empty(domains.TrustedSponsorshipDomainList);
				Assert.Equal(5, domains.MaxSponsorshipLinks);
			}

			[Fact]
			public void DeserializesFromCompletelyEmptyJson()
			{
				// Arrange
				var json = "{}";

				// Act
				var domains = JsonConvert.DeserializeObject<TrustedSponsorshipDomains>(json);

				// Assert
				Assert.NotNull(domains.TrustedSponsorshipDomainList);
				Assert.Empty(domains.TrustedSponsorshipDomainList);
				Assert.Equal(0, domains.MaxSponsorshipLinks);
			}

			[Fact]
			public void DeserializesFromJsonWithNullDomainList()
			{
				// Arrange
				var json = "{\"TrustedSponsorshipDomainList\": null, \"MaxSponsorshipLinks\": 5}";

				// Act
				var domains = JsonConvert.DeserializeObject<TrustedSponsorshipDomains>(json);

				// Assert
				Assert.NotNull(domains.TrustedSponsorshipDomainList);
				Assert.Empty(domains.TrustedSponsorshipDomainList);
				Assert.Equal(5, domains.MaxSponsorshipLinks);
			}
		}
	}
}
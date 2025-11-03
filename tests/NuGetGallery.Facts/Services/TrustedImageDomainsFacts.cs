// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGetGallery.Services
{
	public class TrustedImageDomainsFacts
	{
		public class TheConstructor
		{
			[Fact]
			public void ExpandsDomainsToIncludeWwwVariant()
			{
				// Arrange
				var domains = new[] { "bestpractices.dev" };

				// Act
				var trustedImageDomains = new TrustedImageDomains(domains);

				// Assert
				Assert.True(trustedImageDomains.IsImageDomainTrusted("bestpractices.dev"));
				Assert.True(trustedImageDomains.IsImageDomainTrusted("www.bestpractices.dev"));
			}

			[Fact]
			public void ExpandsWwwDomainsToIncludeNonWwwVariant()
			{
				// Arrange
				var domains = new[] { "www.example.com" };

				// Act
				var trustedImageDomains = new TrustedImageDomains(domains);

				// Assert
				Assert.True(trustedImageDomains.IsImageDomainTrusted("www.example.com"));
				Assert.True(trustedImageDomains.IsImageDomainTrusted(".example.com"));
			}

			[Fact]
			public void HandlesSubdomainsCorrectly()
			{
				// Arrange
				var domains = new[] { "api.example.com" };

				// Act
				var trustedImageDomains = new TrustedImageDomains(domains);

				// Assert
				Assert.True(trustedImageDomains.IsImageDomainTrusted("api.example.com"));
				// Should not add www variant for subdomains other than www
				Assert.False(trustedImageDomains.IsImageDomainTrusted("www.api.example.com"));
			}

			[Fact]
			public void IsCaseInsensitive()
			{
				// Arrange
				var domains = new[] { "bestpractices.dev" };

				// Act
				var trustedImageDomains = new TrustedImageDomains(domains);

				// Assert
				Assert.True(trustedImageDomains.IsImageDomainTrusted("BESTPRACTICES.DEV"));
				Assert.True(trustedImageDomains.IsImageDomainTrusted("WWW.BESTPRACTICES.DEV"));
				Assert.True(trustedImageDomains.IsImageDomainTrusted("BestPractices.Dev"));
			}
		}
	}
}

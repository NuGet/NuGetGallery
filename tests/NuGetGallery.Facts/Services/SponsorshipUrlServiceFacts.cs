// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Services;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery.Services
{
	public class SponsorshipUrlServiceFacts
	{
		private static SponsorshipUrlService CreateService(
			Mock<IEntitiesContext> entitiesContext = null,
			Mock<IContentObjectService> contentObjectService = null,
			Mock<IAuditingService> auditingService = null,
			Mock<ITrustedSponsorshipDomains> trustedSponsorshipDomains = null)
		{
			if (entitiesContext == null)
			{
				entitiesContext = new Mock<IEntitiesContext>();
				
				// Only setup default database mock if we're creating the entities context
				var mockDatabase = new Mock<IDatabase>();
				var mockTransaction = new Mock<IDbContextTransaction>();
				mockDatabase.Setup(x => x.BeginTransaction()).Returns(mockTransaction.Object);
				entitiesContext.Setup(x => x.GetDatabase()).Returns(mockDatabase.Object);
			}
			
			auditingService = auditingService ?? new Mock<IAuditingService>();

			if (contentObjectService == null)
			{
				contentObjectService = new Mock<IContentObjectService>();
				
				if (trustedSponsorshipDomains == null)
				{
					trustedSponsorshipDomains = new Mock<ITrustedSponsorshipDomains>();
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("github.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("www.github.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("patreon.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("www.patreon.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("opencollective.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("www.opencollective.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("ko-fi.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("www.ko-fi.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("tidelift.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("www.tidelift.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("liberapay.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("www.liberapay.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("untrusted.com")).Returns(false);
					trustedSponsorshipDomains.Setup(x => x.MaxSponsorshipLinks).Returns(10);
				}
				
				contentObjectService.Setup(x => x.TrustedSponsorshipDomains).Returns(trustedSponsorshipDomains.Object);
			}

			return new SponsorshipUrlService(entitiesContext.Object, contentObjectService.Object, auditingService.Object);
		}

		private static PackageRegistration CreatePackageRegistration(string id = "TestPackage", string sponsorshipUrls = null)
		{
			return new PackageRegistration
			{
				Id = id,
				SponsorshipUrls = sponsorshipUrls
			};
		}

		private static User CreateUser(string username = "testuser", bool isAdmin = false)
		{
			var user = new User
			{
				Username = username
			};

			if (isAdmin)
			{
				user.Roles = new List<Role>
				{
					new Role { Name = CoreConstants.AdminRoleName }
				};
			}

			return user;
		}

		public class TheGetSponsorshipUrlEntriesMethod
		{
			[Fact]
			public void ReturnsEmptyCollectionWhenNoUrls()
			{
				// Arrange
				var service = CreateService();
				var packageRegistration = CreatePackageRegistration();

				// Act
				var result = service.GetSponsorshipUrlEntries(packageRegistration);

				// Assert
				Assert.NotNull(result);
				Assert.Empty(result);
			}

			[Fact]
			public void ReturnsAllUrlsWithDomainAcceptanceStatus()
			{
				// Arrange
				var sponsorshipData = new[]
				{
					new { url = "https://github.com/sponsors/user", timestamp = DateTime.UtcNow },
					new { url = "https://untrusted.com/sponsor", timestamp = DateTime.UtcNow },
					new { url = "https://patreon.com/user", timestamp = DateTime.UtcNow }
				};
				var sponsorshipJson = JsonConvert.SerializeObject(sponsorshipData);

				var service = CreateService();
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: sponsorshipJson);

				// Act
				var result = service.GetSponsorshipUrlEntries(packageRegistration);

				// Assert
				Assert.Equal(3, result.Count);
				
				var githubEntry = result.First(x => x.Url == "https://github.com/sponsors/user");
				Assert.True(githubEntry.IsDomainAccepted);

				var untrustedEntry = result.First(x => x.Url == "https://untrusted.com/sponsor");
				Assert.False(untrustedEntry.IsDomainAccepted);

				var patreonEntry = result.First(x => x.Url == "https://patreon.com/user");
				Assert.True(patreonEntry.IsDomainAccepted);
			}

			[Fact]
			public void FiltersOutInvalidUrls()
			{
				// Arrange
				var sponsorshipData = new[]
				{
					new { url = "https://github.com/sponsors/user", timestamp = DateTime.UtcNow },
					new { url = "invalid-url", timestamp = DateTime.UtcNow },
					new { url = (string)null, timestamp = DateTime.UtcNow },
					new { url = "https://patreon.com/user", timestamp = DateTime.UtcNow }
				};
				var sponsorshipJson = JsonConvert.SerializeObject(sponsorshipData);

				var service = CreateService();
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: sponsorshipJson);

				// Act
				var result = service.GetSponsorshipUrlEntries(packageRegistration);

				// Assert
				Assert.Equal(2, result.Count);
				Assert.All(result, entry => Assert.True(Uri.TryCreate(entry.Url, UriKind.Absolute, out _)));
			}

			[Theory]
			[InlineData(null, "null sponsorship URLs")]
			[InlineData("", "empty sponsorship URLs")]
			[InlineData("invalid json [}", "malformed JSON")]
			[InlineData("[]", "empty JSON array")]
			[InlineData("null", "JSON null value")]
			public void HandlesInvalidOrEmptyInput(string sponsorshipUrls, string _)
			{
				// Arrange
				var service = CreateService();
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: sponsorshipUrls);

				// Act
				var result = service.GetSponsorshipUrlEntries(packageRegistration);

				// Assert
				Assert.NotNull(result);
				Assert.Empty(result);
			}

			[Fact]
			public void MarksGitHubUrlsWithoutUsernameAsNotAccepted()
			{
				// Arrange
				var sponsorshipData = new[]
				{
					new { url = "https://github.com/sponsors/validuser", timestamp = DateTime.UtcNow },
					new { url = "https://github.com/sponsors", timestamp = DateTime.UtcNow }, // Invalid - no username
					new { url = "https://www.github.com/sponsors/", timestamp = DateTime.UtcNow }, // Invalid - no username
					new { url = "https://patreon.com/user", timestamp = DateTime.UtcNow }
				};
				var sponsorshipJson = JsonConvert.SerializeObject(sponsorshipData);

				var service = CreateService();
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: sponsorshipJson);

				// Act
				var result = service.GetSponsorshipUrlEntries(packageRegistration);

				// Assert
				Assert.Equal(4, result.Count); // All URLs are returned but with correct acceptance status
				
				// Valid GitHub URL should be accepted
				var validGitHubEntry = result.First(e => e.Url == "https://github.com/sponsors/validuser");
				Assert.True(validGitHubEntry.IsDomainAccepted);
				
				// Invalid GitHub URLs should not be accepted
				var invalidGitHubEntry1 = result.First(e => e.Url == "https://github.com/sponsors");
				Assert.False(invalidGitHubEntry1.IsDomainAccepted);
				
				var invalidGitHubEntry2 = result.First(e => e.Url == "https://www.github.com/sponsors/");
				Assert.False(invalidGitHubEntry2.IsDomainAccepted);
				
				// Patreon URL should be accepted
				var patreonEntry = result.First(e => e.Url == "https://patreon.com/user");
				Assert.True(patreonEntry.IsDomainAccepted);
			}

			[Fact]
			public void HandlesJsonWithMixedValidAndInvalidUrls()
			{
				// Arrange
				var sponsorshipData = new[]
				{
					new { url = "https://github.com/sponsors/user", timestamp = DateTime.UtcNow },
					new { url = "", timestamp = DateTime.UtcNow }, // Empty URL
					new { url = "not-a-url", timestamp = DateTime.UtcNow }, // Invalid URL format
					new { url = "https://patreon.com/user", timestamp = DateTime.UtcNow },
					new { url = "javascript:alert('xss')", timestamp = DateTime.UtcNow }, // Dangerous URL scheme
					new { url = "https://opencollective.com/project", timestamp = DateTime.UtcNow }
				};
				var sponsorshipJson = JsonConvert.SerializeObject(sponsorshipData);

				var service = CreateService();
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: sponsorshipJson);

				// Act
				var result = service.GetSponsorshipUrlEntries(packageRegistration);

				// Assert
				Assert.Equal(3, result.Count); // Only valid HTTP(S) URLs should be included
				Assert.All(result, entry => 
				{
					Assert.True(Uri.TryCreate(entry.Url, UriKind.Absolute, out Uri uri));
					Assert.True(uri.Scheme == "https" || uri.Scheme == "http");
				});
			}

			[Fact]
			public void PreservesTimestampsFromOriginalData()
			{
				// Arrange
				var timestamp1 = DateTime.UtcNow.AddDays(-5);
				var timestamp2 = DateTime.UtcNow.AddDays(-2);
				var sponsorshipData = new[]
				{
					new { url = "https://github.com/sponsors/user1", timestamp = timestamp1 },
					new { url = "https://patreon.com/user2", timestamp = timestamp2 }
				};
				var sponsorshipJson = JsonConvert.SerializeObject(sponsorshipData);

				var service = CreateService();
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: sponsorshipJson);

				// Act
				var result = service.GetSponsorshipUrlEntries(packageRegistration);

				// Assert
				Assert.Equal(2, result.Count);
				var githubEntry = result.First(e => e.Url == "https://github.com/sponsors/user1");
				var patreonEntry = result.First(e => e.Url == "https://patreon.com/user2");
				
				Assert.Equal(timestamp1, githubEntry.Timestamp);
				Assert.Equal(timestamp2, patreonEntry.Timestamp);
			}

			[Fact]
			public void HandlesDifferentTrustedDomainVariations()
			{
				// Arrange
				var sponsorshipData = new[]
				{
					new { url = "https://github.com/sponsors/user", timestamp = DateTime.UtcNow },
					new { url = "https://www.github.com/sponsors/user", timestamp = DateTime.UtcNow },
					new { url = "https://ko-fi.com/user", timestamp = DateTime.UtcNow },
					new { url = "https://www.ko-fi.com/user", timestamp = DateTime.UtcNow },
					new { url = "https://tidelift.com/subscription/pkg/npm-package", timestamp = DateTime.UtcNow }
				};
				var sponsorshipJson = JsonConvert.SerializeObject(sponsorshipData);

				var service = CreateService();
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: sponsorshipJson);

				// Act
				var result = service.GetSponsorshipUrlEntries(packageRegistration);

				// Assert
				Assert.Equal(5, result.Count);
				Assert.All(result, entry => Assert.True(entry.IsDomainAccepted));
			}

			[Fact]
			public void NormalizesUrlsToHttps()
			{
				// Arrange
				var sponsorshipData = new[]
				{
					new { url = "http://github.com/sponsors/user", timestamp = DateTime.UtcNow }, // HTTP should be converted to HTTPS
					new { url = "https://patreon.com/user", timestamp = DateTime.UtcNow } // HTTPS should remain as-is
				};
				var sponsorshipJson = JsonConvert.SerializeObject(sponsorshipData);

				var service = CreateService();
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: sponsorshipJson);

				// Act
				var result = service.GetSponsorshipUrlEntries(packageRegistration);

				// Assert
				Assert.Equal(2, result.Count);
				Assert.All(result, entry => Assert.True(entry.Url.StartsWith("https://"), 
					$"Expected HTTPS URL but got: {entry.Url}"));
			}
		}

		public class TheAddSponsorshipUrlAsyncMethod
		{
			[Theory]
			[InlineData(null)]
			[InlineData("")]
			[InlineData("   ")]
			[InlineData("invalid-url")]
			[InlineData("ftp://example.com")]
			[InlineData("https://github.com/sponsors")] // Missing username
			[InlineData("https://www.github.com/sponsors/")] // Missing username with trailing slash
			[InlineData("https://untrusted.com/sponsor")] // Untrusted domain
			public async Task ThrowsArgumentExceptionForInvalidUrl(string invalidUrl)
			{
				// Arrange
				var service = CreateService();
				var packageRegistration = CreatePackageRegistration();
				var user = CreateUser();

				// Act & Assert
				await Assert.ThrowsAsync<ArgumentException>(() => 
					service.AddSponsorshipUrlAsync(packageRegistration, invalidUrl, user));
			}

			[Fact]
			public async Task ThrowsArgumentExceptionForUrlTooLong()
			{
				// Arrange
				var service = CreateService();
				var packageRegistration = CreatePackageRegistration();
				var user = CreateUser();
				var longUrl = "https://github.com/sponsors/" + new string('a', 2100); // Exceeds 2048 limit

				// Act & Assert
				var ex = await Assert.ThrowsAsync<ArgumentException>(() => 
					service.AddSponsorshipUrlAsync(packageRegistration, longUrl, user));
				Assert.Contains("too long", ex.Message);
			}
		}

		public class TheRemoveSponsorshipUrlAsyncMethod
		{
			[Fact]
			public async Task ThrowsArgumentExceptionForNullUrl()
			{
				// Arrange
				var service = CreateService();
				var packageRegistration = CreatePackageRegistration();
				var user = CreateUser();

				// Act & Assert
				await Assert.ThrowsAsync<ArgumentException>(() => 
					service.RemoveSponsorshipUrlAsync(packageRegistration, null, user));
			}

			[Fact]
			public async Task ThrowsArgumentExceptionWhenUrlNotFound()
			{
				// Arrange
				var service = CreateService();
				var packageRegistration = CreatePackageRegistration();
				var user = CreateUser();

				// Act & Assert
				var ex = await Assert.ThrowsAsync<ArgumentException>(() => 
					service.RemoveSponsorshipUrlAsync(packageRegistration, "https://github.com/sponsors/user", user));
				
				Assert.Contains("not found", ex.Message);
			}
		}

	}
}
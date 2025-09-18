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
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("patreon.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("opencollective.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("ko-fi.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("tidelift.com")).Returns(true);
					trustedSponsorshipDomains.Setup(x => x.IsSponsorshipDomainTrusted("liberapay.com")).Returns(true);
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
		}

		public class TheAddSponsorshipUrlAsyncMethod
		{
			[Fact]
			public async Task ThrowsNullReferenceExceptionWhenUserIsNull()
			{
				// Arrange
				var service = CreateService();
				var packageRegistration = CreatePackageRegistration();

				// Act & Assert
				// Note: User validation is handled at controller level, so service expects valid user
				await Assert.ThrowsAsync<NullReferenceException>(() => 
					service.AddSponsorshipUrlAsync(packageRegistration, "https://github.com/sponsors/user", null));
			}

			[Fact]
			public async Task ThrowsNullReferenceExceptionWhenPackageRegistrationIsNull()
			{
				// Arrange
				var service = CreateService();
				var user = CreateUser();

				// Act & Assert
				// Note: Package validation is handled at controller level, so service expects valid package
				await Assert.ThrowsAsync<NullReferenceException>(() => 
					service.AddSponsorshipUrlAsync(null, "https://github.com/sponsors/user", user));
			}

			[Theory]
			[InlineData(null)]
			[InlineData("")]
			[InlineData("   ")]
			[InlineData("invalid-url")]
			[InlineData("ftp://example.com")]
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
			public async Task AddsValidUrlAndCreatesAuditRecord()
			{
				// Arrange
				var mockContext = new Mock<IEntitiesContext>();
				var mockDatabase = new Mock<IDatabase>();
				var mockTransaction = new Mock<IDbContextTransaction>();
				
				mockContext.Setup(x => x.GetDatabase()).Returns(mockDatabase.Object);
				mockDatabase.Setup(x => x.BeginTransaction()).Returns(mockTransaction.Object);
				mockContext.Setup(x => x.SaveChangesAsync()).Returns(Task.FromResult(1));

				var mockAuditingService = new Mock<IAuditingService>();
				var service = CreateService(entitiesContext: mockContext, auditingService: mockAuditingService);
				
				var packageRegistration = CreatePackageRegistration();
				var user = CreateUser();
				var url = "https://github.com/sponsors/user";

				// Act
				var result = await service.AddSponsorshipUrlAsync(packageRegistration, url, user);

				// Assert
				Assert.Equal(url, result);
				Assert.NotNull(packageRegistration.SponsorshipUrls);
				
				// Verify the URL was added to the JSON
				var entries = JsonConvert.DeserializeObject<List<SponsorshipUrlEntry>>(packageRegistration.SponsorshipUrls);
				Assert.Single(entries);
				Assert.Equal(url, entries[0].Url);

				// Verify audit record was created
				mockAuditingService.Verify(x => x.SaveAuditRecordAsync(
					It.Is<PackageRegistrationAuditRecord>(r => 
						r.Action == AuditedPackageRegistrationAction.AddSponsorshipUrl &&
						r.Id == packageRegistration.Id)), Times.Once);

				// Verify transaction handling
				mockContext.Verify(x => x.SaveChangesAsync(), Times.Once);
				mockTransaction.Verify(x => x.Commit(), Times.Once);
			}

			[Fact]
			public async Task AddsUrlToExistingUrls()
			{
				// Arrange
				var existingUrl = new { url = "https://patreon.com/user", timestamp = DateTime.UtcNow.AddDays(-1) };
				var existingJson = JsonConvert.SerializeObject(new[] { existingUrl });

				var mockContext = new Mock<IEntitiesContext>();
				var mockDatabase = new Mock<IDatabase>();
				var mockTransaction = new Mock<IDbContextTransaction>();
				
				mockContext.Setup(x => x.GetDatabase()).Returns(mockDatabase.Object);
				mockDatabase.Setup(x => x.BeginTransaction()).Returns(mockTransaction.Object);
				mockContext.Setup(x => x.SaveChangesAsync()).Returns(Task.FromResult(1));

				var service = CreateService(entitiesContext: mockContext);
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: existingJson);
				var user = CreateUser();
				var newUrl = "https://github.com/sponsors/user";

				// Act
				var result = await service.AddSponsorshipUrlAsync(packageRegistration, newUrl, user);

				// Assert
				Assert.Equal(newUrl, result);
				
				var entries = JsonConvert.DeserializeObject<List<SponsorshipUrlEntry>>(packageRegistration.SponsorshipUrls);
				Assert.Equal(2, entries.Count);
				Assert.Contains(entries, e => e.Url == "https://patreon.com/user");
				Assert.Contains(entries, e => e.Url == newUrl);
			}

			[Fact]
			public async Task AdminCanAddUrlToAnyPackage()
			{
				// Arrange
				var mockContext = new Mock<IEntitiesContext>();
				var mockDatabase = new Mock<IDatabase>();
				var mockTransaction = new Mock<IDbContextTransaction>();
				
				mockContext.Setup(x => x.GetDatabase()).Returns(mockDatabase.Object);
				mockDatabase.Setup(x => x.BeginTransaction()).Returns(mockTransaction.Object);
				mockContext.Setup(x => x.SaveChangesAsync()).Returns(Task.FromResult(1));

				var mockAuditingService = new Mock<IAuditingService>();
				var service = CreateService(entitiesContext: mockContext, auditingService: mockAuditingService);
				
				var packageRegistration = CreatePackageRegistration();
				var adminUser = CreateUser("admin", isAdmin: true);
				var url = "https://github.com/sponsors/user";

				// Act
				var result = await service.AddSponsorshipUrlAsync(packageRegistration, url, adminUser);

				// Assert
				Assert.Equal(url, result);
				Assert.NotNull(packageRegistration.SponsorshipUrls);
				
				// Verify the URL was added to the JSON
				var entries = JsonConvert.DeserializeObject<List<SponsorshipUrlEntry>>(packageRegistration.SponsorshipUrls);
				Assert.Single(entries);
				Assert.Equal(url, entries[0].Url);

				// Verify audit record was created with admin role
				mockAuditingService.Verify(x => x.SaveAuditRecordAsync(
					It.Is<PackageRegistrationAuditRecord>(r => 
						r.Action == AuditedPackageRegistrationAction.AddSponsorshipUrl &&
						r.Id == packageRegistration.Id &&
						r.ActorRole == "Administrator")), Times.Once);
			}
		}

		public class TheRemoveSponsorshipUrlAsyncMethod
		{
			[Fact]
			public async Task ThrowsNullReferenceExceptionWhenUserIsNull()
			{
				// Arrange
				var service = CreateService();
				var packageRegistration = CreatePackageRegistration();

				// Act & Assert
				// Note: User validation is handled at controller level, so service expects valid user
				await Assert.ThrowsAsync<NullReferenceException>(() => 
					service.RemoveSponsorshipUrlAsync(packageRegistration, "https://github.com/sponsors/user", null));
			}

			[Fact]
			public async Task ThrowsNullReferenceExceptionWhenPackageRegistrationIsNull()
			{
				// Arrange
				var service = CreateService();
				var user = CreateUser();

				// Act & Assert
				// Note: Package validation is handled at controller level, so service expects valid package
				await Assert.ThrowsAsync<NullReferenceException>(() => 
					service.RemoveSponsorshipUrlAsync(null, "https://github.com/sponsors/user", user));
			}

			[Theory]
			[InlineData(null)]
			[InlineData("")]
			[InlineData("   ")]
			public async Task ThrowsArgumentExceptionForInvalidUrl(string invalidUrl)
			{
				// Arrange
				var service = CreateService();
				var packageRegistration = CreatePackageRegistration();
				var user = CreateUser();

				// Act & Assert
				await Assert.ThrowsAsync<ArgumentException>(() => 
					service.RemoveSponsorshipUrlAsync(packageRegistration, invalidUrl, user));
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

			[Fact]
			public async Task RemovesUrlAndCreatesAuditRecord()
			{
				// Arrange
				var urlToRemove = "https://github.com/sponsors/user";
				var sponsorshipData = new[]
				{
					new { url = urlToRemove, timestamp = DateTime.UtcNow },
					new { url = "https://patreon.com/user", timestamp = DateTime.UtcNow }
				};
				var existingJson = JsonConvert.SerializeObject(sponsorshipData);

				var mockContext = new Mock<IEntitiesContext>();
				var mockDatabase = new Mock<IDatabase>();
				var mockTransaction = new Mock<IDbContextTransaction>();
				
				mockContext.Setup(x => x.GetDatabase()).Returns(mockDatabase.Object);
				mockDatabase.Setup(x => x.BeginTransaction()).Returns(mockTransaction.Object);
				mockContext.Setup(x => x.SaveChangesAsync()).Returns(Task.FromResult(1));

				var mockAuditingService = new Mock<IAuditingService>();
				var service = CreateService(entitiesContext: mockContext, auditingService: mockAuditingService);
				
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: existingJson);
				var user = CreateUser();

				// Act
				await service.RemoveSponsorshipUrlAsync(packageRegistration, urlToRemove, user);

				// Assert
				var entries = JsonConvert.DeserializeObject<List<SponsorshipUrlEntry>>(packageRegistration.SponsorshipUrls);
				Assert.Single(entries);
				Assert.Equal("https://patreon.com/user", entries[0].Url);

				// Verify audit record was created
				mockAuditingService.Verify(x => x.SaveAuditRecordAsync(
					It.Is<PackageRegistrationAuditRecord>(r => 
						r.Action == AuditedPackageRegistrationAction.RemoveSponsorshipUrl &&
						r.Id == packageRegistration.Id)), Times.Once);

				// Verify transaction handling
				mockContext.Verify(x => x.SaveChangesAsync(), Times.Once);
				mockTransaction.Verify(x => x.Commit(), Times.Once);
			}

			[Fact]
			public async Task RemovesLastUrlSetsJsonToNull()
			{
				// Arrange
				var urlToRemove = "https://github.com/sponsors/user";
				var sponsorshipData = new[] { new { url = urlToRemove, timestamp = DateTime.UtcNow } };
				var existingJson = JsonConvert.SerializeObject(sponsorshipData);

				var mockContext = new Mock<IEntitiesContext>();
				var mockDatabase = new Mock<IDatabase>();
				var mockTransaction = new Mock<IDbContextTransaction>();
				
				mockContext.Setup(x => x.GetDatabase()).Returns(mockDatabase.Object);
				mockDatabase.Setup(x => x.BeginTransaction()).Returns(mockTransaction.Object);
				mockContext.Setup(x => x.SaveChangesAsync()).Returns(Task.FromResult(1));

				var service = CreateService(entitiesContext: mockContext);
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: existingJson);
				var user = CreateUser();

				// Act
				await service.RemoveSponsorshipUrlAsync(packageRegistration, urlToRemove, user);

				// Assert
				Assert.Null(packageRegistration.SponsorshipUrls);
			}

			[Fact]
			public async Task RemovalIsCaseInsensitive()
			{
				// Arrange
				var urlToRemove = "https://github.com/sponsors/user";
				var sponsorshipData = new[] { new { url = urlToRemove, timestamp = DateTime.UtcNow } };
				var existingJson = JsonConvert.SerializeObject(sponsorshipData);

				var mockContext = new Mock<IEntitiesContext>();
				var mockDatabase = new Mock<IDatabase>();
				var mockTransaction = new Mock<IDbContextTransaction>();
				
				mockContext.Setup(x => x.GetDatabase()).Returns(mockDatabase.Object);
				mockDatabase.Setup(x => x.BeginTransaction()).Returns(mockTransaction.Object);
				mockContext.Setup(x => x.SaveChangesAsync()).Returns(Task.FromResult(1));

				var service = CreateService(entitiesContext: mockContext);
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: existingJson);
				var user = CreateUser();

				// Act - remove with different case
				await service.RemoveSponsorshipUrlAsync(packageRegistration, urlToRemove.ToUpperInvariant(), user);

				// Assert
				Assert.Null(packageRegistration.SponsorshipUrls);
			}

			[Fact]
			public async Task AdminCanRemoveUrlFromAnyPackage()
			{
				// Arrange
				var urlToRemove = "https://github.com/sponsors/user";
				var sponsorshipData = new[] { new { url = urlToRemove, timestamp = DateTime.UtcNow } };
				var existingJson = JsonConvert.SerializeObject(sponsorshipData);

				var mockContext = new Mock<IEntitiesContext>();
				var mockDatabase = new Mock<IDatabase>();
				var mockTransaction = new Mock<IDbContextTransaction>();
				
				mockContext.Setup(x => x.GetDatabase()).Returns(mockDatabase.Object);
				mockDatabase.Setup(x => x.BeginTransaction()).Returns(mockTransaction.Object);
				mockContext.Setup(x => x.SaveChangesAsync()).Returns(Task.FromResult(1));
				
				// Ensure the transaction mock can be disposed properly
				mockTransaction.Setup(x => x.Dispose());

				var mockAuditingService = new Mock<IAuditingService>();
				var service = CreateService(entitiesContext: mockContext, auditingService: mockAuditingService);
				
				var packageRegistration = CreatePackageRegistration(sponsorshipUrls: existingJson);
				var adminUser = CreateUser("admin", isAdmin: true);

				// Act
				await service.RemoveSponsorshipUrlAsync(packageRegistration, urlToRemove, adminUser);

				// Assert
				Assert.Null(packageRegistration.SponsorshipUrls);

				// Verify audit record was created with admin role
				mockAuditingService.Verify(x => x.SaveAuditRecordAsync(
					It.Is<PackageRegistrationAuditRecord>(r => 
						r.Action == AuditedPackageRegistrationAction.RemoveSponsorshipUrl &&
						r.Id == packageRegistration.Id &&
						r.ActorRole == "Administrator")), Times.Once);

				// Verify transaction handling
				mockContext.Verify(x => x.SaveChangesAsync(), Times.Once);
				mockTransaction.Verify(x => x.Commit(), Times.Once);
			}
		}

		public class TheTrustedSponsorshipDomainsProperty
		{
			[Fact]
			public void ReturnsConfigurationFromContentObjectService()
			{
				// Arrange
				var mockTrustedDomains = new Mock<ITrustedSponsorshipDomains>();
				var mockContentObjectService = new Mock<IContentObjectService>();
				mockContentObjectService.Setup(x => x.TrustedSponsorshipDomains).Returns(mockTrustedDomains.Object);

				var service = CreateService(contentObjectService: mockContentObjectService);

				// Act
				var result = service.TrustedSponsorshipDomains;

				// Assert
				Assert.Same(mockTrustedDomains.Object, result);
			}
		}
	}
}
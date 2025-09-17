// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using Xunit;

namespace NuGetGallery.Auditing
{
	public class PackageRegistrationAuditRecordSponsorshipFacts
	{
		public class TheCreateForAddSponsorshipUrlMethod
		{
			[Fact]
			public void CreatesCorrectAuditRecord()
			{
				// Arrange
				var packageRegistration = new PackageRegistration { Id = "TestPackage" };
				var url = "https://github.com/sponsors/user";
				var username = "testuser";
				var isAdmin = false;
				var timestamp = DateTime.UtcNow;

				// Act
				var auditRecord = PackageRegistrationAuditRecord.CreateForAddSponsorshipUrl(
					packageRegistration, url, username, isAdmin, timestamp);

				// Assert
				Assert.Equal(AuditedPackageRegistrationAction.AddSponsorshipUrl, auditRecord.Action);
				Assert.Equal("TestPackage", auditRecord.Id);
				Assert.Equal(username, auditRecord.Owner);
				Assert.Equal(isAdmin ? "Administrator" : "PackageOwner", auditRecord.ActorRole);
				Assert.Equal(timestamp, auditRecord.DatabaseTimestamp);
				Assert.Equal(url, auditRecord.SponsorshipUrl);
			}

			[Fact]
			public void ThrowsArgumentNullExceptionWhenPackageRegistrationIsNull()
			{
				// Act & Assert
				Assert.Throws<ArgumentNullException>(() =>
					PackageRegistrationAuditRecord.CreateForAddSponsorshipUrl(
						null, "https://github.com/sponsors/user", "user", false, DateTime.UtcNow));
			}

			[Theory]
			[InlineData(null)]
			[InlineData("")]
			[InlineData("   ")]
			public void ThrowsArgumentExceptionWhenUrlIsInvalid(string invalidUrl)
			{
				// Arrange
				var packageRegistration = new PackageRegistration { Id = "TestPackage" };

				// Act & Assert
				Assert.Throws<ArgumentException>(() =>
					PackageRegistrationAuditRecord.CreateForAddSponsorshipUrl(
						packageRegistration, invalidUrl, "user", false, DateTime.UtcNow));
			}

			[Theory]
			[InlineData(null)]
			[InlineData("")]
			[InlineData("   ")]
			public void ThrowsArgumentExceptionWhenUsernameIsInvalid(string invalidUsername)
			{
				// Arrange
				var packageRegistration = new PackageRegistration { Id = "TestPackage" };

				// Act & Assert
				Assert.Throws<ArgumentException>(() =>
					PackageRegistrationAuditRecord.CreateForAddSponsorshipUrl(
						packageRegistration, "https://github.com/sponsors/user", invalidUsername, false, DateTime.UtcNow));
			}
		}

		public class TheCreateForRemoveSponsorshipUrlMethod
		{
			[Fact]
			public void CreatesCorrectAuditRecord()
			{
				// Arrange
				var packageRegistration = new PackageRegistration { Id = "TestPackage" };
				var url = "https://github.com/sponsors/user";
				var username = "testuser";
				var isAdmin = true;
				var timestamp = DateTime.UtcNow;

				// Act
				var auditRecord = PackageRegistrationAuditRecord.CreateForRemoveSponsorshipUrl(
					packageRegistration, url, username, isAdmin, timestamp);

				// Assert
				Assert.Equal(AuditedPackageRegistrationAction.RemoveSponsorshipUrl, auditRecord.Action);
				Assert.Equal("TestPackage", auditRecord.Id);
				Assert.Equal(username, auditRecord.Owner);
				Assert.Equal(isAdmin ? "Administrator" : "PackageOwner", auditRecord.ActorRole);
				Assert.Equal(timestamp, auditRecord.DatabaseTimestamp);
				Assert.Equal(url, auditRecord.SponsorshipUrl);
			}

			[Fact]
			public void ThrowsArgumentNullExceptionWhenPackageRegistrationIsNull()
			{
				// Act & Assert
				Assert.Throws<ArgumentNullException>(() =>
					PackageRegistrationAuditRecord.CreateForRemoveSponsorshipUrl(
						null, "https://github.com/sponsors/user", "user", false, DateTime.UtcNow));
			}

			[Theory]
			[InlineData(null)]
			[InlineData("")]
			[InlineData("   ")]
			public void ThrowsArgumentExceptionWhenUrlIsInvalid(string invalidUrl)
			{
				// Arrange
				var packageRegistration = new PackageRegistration { Id = "TestPackage" };

				// Act & Assert
				Assert.Throws<ArgumentException>(() =>
					PackageRegistrationAuditRecord.CreateForRemoveSponsorshipUrl(
						packageRegistration, invalidUrl, "user", false, DateTime.UtcNow));
			}

			[Theory]
			[InlineData(null)]
			[InlineData("")]
			[InlineData("   ")]
			public void ThrowsArgumentExceptionWhenUsernameIsInvalid(string invalidUsername)
			{
				// Arrange
				var packageRegistration = new PackageRegistration { Id = "TestPackage" };

				// Act & Assert
				Assert.Throws<ArgumentException>(() =>
					PackageRegistrationAuditRecord.CreateForRemoveSponsorshipUrl(
						packageRegistration, "https://github.com/sponsors/user", invalidUsername, false, DateTime.UtcNow));
			}
		}
	}
}

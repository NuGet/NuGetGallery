// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class UserAuditRecordTests
    {
        [Fact]
        public void Constructor_WithoutAffected_ThrowsForNullUser()
        {
            Assert.Throws<ArgumentNullException>(() => new UserAuditRecord(user: null, action: AuditedUserAction.Login));
        }

        [Fact]
        public void Constructor_WithAffected_SetsProperties()
        {
            var user = new User()
            {
                Username = "a",
                EmailAddress = "b",
                UnconfirmedEmailAddress = "c",
                Roles = new List<Role>() { new Role() { Name = "d" } },
                Credentials = new List<Credential>()
                {
                    new Credential(type: CredentialTypes.Password.V3, value: "e"),
                    new Credential(type: "f", value: "g")
                }
            };

            var record = new UserAuditRecord(user, AuditedUserAction.Login, new Credential(type: "h", value: "i"));

            Assert.Equal("a", record.Username);
            Assert.Equal("b", record.EmailAddress);
            Assert.Equal("c", record.UnconfirmedEmailAddress);
            Assert.Single(record.Roles);
            Assert.Equal("d", record.Roles[0]);
            Assert.Single(record.Credentials);
            Assert.Equal(CredentialTypes.Password.V3, record.Credentials[0].Type);
            Assert.Null(record.Credentials[0].Value);
            Assert.Single(record.AffectedCredential);
            Assert.Equal("h", record.AffectedCredential[0].Type);
            Assert.Null(record.AffectedCredential[0].Value);
        }

        [Fact]
        public void Constructor_WithAffectedPolicies_ThrowsIfPoliciesNull()
        {
            Assert.Throws<ArgumentException>(() => new UserAuditRecord(user: new User(),
                action: AuditedUserAction.SubscribeToPolicies, affectedPolicies: null));
        }

        [Fact]
        public void Constructor_WithAffectedPolicies_ThrowsIfPoliciesEmpty()
        {
            Assert.Throws<ArgumentException>(() => new UserAuditRecord(user: new User(),
                action: AuditedUserAction.SubscribeToPolicies, affectedPolicies: Array.Empty<UserSecurityPolicy>()));
        }

        [Fact]
        public void Constructor_WithAffectedPolicies_SetsProperties()
        {
            // Arrange.
            var user = new User()
            {
                Username = "a",
                EmailAddress = "b",
                UnconfirmedEmailAddress = "c",
                Roles = new List<Role>() { new Role() { Name = "d" } },
                Credentials = new List<Credential>()
                {
                    new Credential(type: CredentialTypes.Password.V3, value: "e"),
                    new Credential(type: "f", value: "g")
                }
            };

            // Act.
            var record = new UserAuditRecord(user, AuditedUserAction.SubscribeToPolicies,
                new UserSecurityPolicy[] { new UserSecurityPolicy("A", "B", "C") } );

            // Assert.

            Assert.Equal("a", record.Username);
            Assert.Equal("b", record.EmailAddress);
            Assert.Equal("c", record.UnconfirmedEmailAddress);
            Assert.Single(record.Roles);
            Assert.Equal("d", record.Roles[0]);
            Assert.Single(record.Credentials);
            Assert.Equal(CredentialTypes.Password.V3, record.Credentials[0].Type);
            Assert.Null(record.Credentials[0].Value);
            Assert.Empty(record.AffectedCredential);
            Assert.Single(record.AffectedPolicies);
            Assert.Equal("A", record.AffectedPolicies[0].Name);
            Assert.Equal("B", record.AffectedPolicies[0].Subscription);
            Assert.Equal("C", record.AffectedPolicies[0].Value);
        }

        [Fact]
        public void Constructor_WithRevocationSource_SetsProperties()
        {
            var user = new User("a");
            var testRevocationSource = "TestRevocationSource";
            var record = new UserAuditRecord(user, AuditedUserAction.RevokeCredential, new Credential(type: "b", value: "c"), testRevocationSource);
            Assert.Single(record.AffectedCredential);
            Assert.Equal(testRevocationSource, record.AffectedCredential[0].RevocationSource);
            Assert.Equal("b", record.AffectedCredential[0].Type);
            Assert.Equal("c", record.AffectedCredential[0].Value);
        }

        [Fact]
        public void GetPath_ReturnsLowerCasedUserName()
        {
            var user = new User()
            {
                Username = "A",
                Roles = new List<Role>(),
                Credentials = new List<Credential>()
            };

            var record = new UserAuditRecord(user, AuditedUserAction.Login);
            var actualPath = record.GetPath();

            Assert.Equal("a", actualPath);
        }
    }
}
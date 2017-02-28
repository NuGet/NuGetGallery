// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
            Assert.Equal(1, record.Roles.Length);
            Assert.Equal("d", record.Roles[0]);
            Assert.Equal(1, record.Credentials.Length);
            Assert.Equal(CredentialTypes.Password.V3, record.Credentials[0].Type);
            Assert.Null(record.Credentials[0].Value);
            Assert.Equal(1, record.AffectedCredential.Length);
            Assert.Equal("h", record.AffectedCredential[0].Type);
            Assert.Null(record.AffectedCredential[0].Value);
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
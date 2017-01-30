// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGetGallery.Infrastructure
{
    public class UserAuditRecordFacts
    {
        [Fact]
        public void FiltersOutUnsupportedCredentials()
        {
            // Arrange
            var credentialBuilder = new CredentialBuilder();
            var credentials = new List<Credential> {
                    credentialBuilder.CreatePasswordCredential("v3"),
                    TestCredentialBuilder.CreatePbkdf2Password("pbkdf2"),
                    TestCredentialBuilder.CreateSha1Password("sha1"),
                    TestCredentialBuilder.CreateV1ApiKey(Guid.NewGuid(), Fakes.ExpirationForApiKeyV1),
                    TestCredentialBuilder.CreateV2ApiKey(Guid.NewGuid(), Fakes.ExpirationForApiKeyV1),
                    credentialBuilder.CreateExternalCredential("MicrosoftAccount", "blarg", "Bloog"),
                    new Credential { Type = "unsupported" }
            };

            var user = new User
            {
                Username = "name",
                Credentials = credentials
            };

            // Act 
            var userAuditRecord = new UserAuditRecord(user, AuditedUserAction.AddCredential);

            // Assert
            var auditRecords = userAuditRecord.Credentials.ToDictionary(c => c.Type);
            Assert.Equal(6, auditRecords.Count);
            Assert.True(auditRecords.ContainsKey(credentials[0].Type));
            Assert.True(auditRecords.ContainsKey(credentials[1].Type));
            Assert.True(auditRecords.ContainsKey(credentials[2].Type));
            Assert.True(auditRecords.ContainsKey(credentials[3].Type));
            Assert.True(auditRecords.ContainsKey(credentials[4].Type));
            Assert.True(auditRecords.ContainsKey(credentials[5].Type));
        }
    }
}

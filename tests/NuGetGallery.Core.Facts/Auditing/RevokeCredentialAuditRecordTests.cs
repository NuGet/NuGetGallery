// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;
using NuGet.Services.Entities;

namespace NuGetGallery.Auditing
{
    public class RevokeCredentialAuditRecordTests
    {
        [Theory]
        [InlineData(AuditedRevokeCredentialAction.RevokeApiKey)]
        public void Constructor_SetsProperties(AuditedRevokeCredentialAction action)
        {
            var credential = new Credential(type: "a", value: "b");
            var userName = "TestUsername";
            credential.User = new User(userName);
            var revocationSource = "TestRevocationSource";
            var leakedUrl = "TestLeakedUrl";
            var requestingUsername = "TestRequestingUsername";

            var record = new RevokeCredentialAuditRecord(
                credential: credential,
                action: action,
                revocationSource: revocationSource,
                leakedUrl: leakedUrl,
                requestingUsername: requestingUsername);

            Assert.Equal(userName, record.Username);
            Assert.Equal(action, record.Action);
            Assert.Equal(revocationSource, record.RevocationSource);
            Assert.Equal(leakedUrl, record.LeakedUrl);
            Assert.Equal(requestingUsername, record.RequestingUsername);
            Assert.Equal("a", record.Credential.Type);
        }

        [Theory]
        [InlineData(AuditedRevokeCredentialAction.RevokeApiKey)]
        public void GetPath_ReturnsLowerCasedId(AuditedRevokeCredentialAction action)
        {
            var credential = new Credential(type: "a", value: "b");
            var userName = "TestUsername";
            credential.User = new User(userName);

            var record = new RevokeCredentialAuditRecord(
                credential: credential,
                action: action,
                revocationSource: "TestRevocationSource",
                leakedUrl: "TestLeakedUrl",
                requestingUsername: "TestRequestingUsername");

            var actualPath = record.GetPath();

            Assert.Equal("testusername", actualPath);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class CredentialAuditRecordTests
    {
        [Fact]
        public void Constructor_ThrowsForNullCredential()
        {
            Assert.Throws<ArgumentNullException>(() => new CredentialAuditRecord(credential: null, removedOrRevoked: true));
        }

        [Fact]
        public void Constructor_ThrowsForRemovalWithNullType()
        {
            var credential = new Credential();

            Assert.Throws<ArgumentNullException>(() => new CredentialAuditRecord(credential, removedOrRevoked: true));
        }

        [Fact]
        public void Constructor_RemovalOfNonPasswordSetsValue()
        {
            var credential = new Credential(type: "a", value: "b");
            var record = new CredentialAuditRecord(credential, removedOrRevoked: true);

            Assert.Equal("b", record.Value);
        }

        [Fact]
        public void Constructor_RemovalOfPasswordDoesNotSetValue()
        {
            var credential = new Credential(type: CredentialTypes.Password.V3, value: "a");
            var record = new CredentialAuditRecord(credential, removedOrRevoked: true);

            Assert.Null(record.Value);
        }

        [Fact]
        public void Constructor_NonRemovalOfNonPasswordDoesNotSetsValue()
        {
            var credential = new Credential(type: "a", value: "b");
            var record = new CredentialAuditRecord(credential, removedOrRevoked: false);

            Assert.Null(record.Value);
        }

        [Theory]
        [InlineData(CredentialTypes.External.MicrosoftAccount)]
        [InlineData(CredentialTypes.External.AzureActiveDirectoryAccount)]
        public void Constructor_ExternalCredentialSetsValue(string externalType)
        {
            var credential = new Credential(type: externalType, value: "b");
            var record = new CredentialAuditRecord(credential, removedOrRevoked: false);

            Assert.Equal("b", record.Value);
        }

        [Fact]
        public void Constructor_NonRemovalOfPasswordDoesNotSetValue()
        {
            var credential = new Credential(type: CredentialTypes.Password.V3, value: "a");
            var record = new CredentialAuditRecord(credential, removedOrRevoked: false);

            Assert.Null(record.Value);
        }

        [Fact]
        public void Constructor_SetsProperties()
        {
            var created = DateTime.MinValue;
            var expires = DateTime.MinValue.AddDays(1);
            var lastUsed = DateTime.MinValue.AddDays(2);
            var credential = new Credential()
            {
                Created = created,
                Description = "a",
                Expires = expires,
                Identity = "b",
                TenantId = "c",
                Key = 1,
                LastUsed = lastUsed,
                Scopes = new List<Scope>() { new Scope(subject: "c", allowedAction: "d") },
                Type = "e",
                Value = "f"
            };
            var record = new CredentialAuditRecord(credential, removedOrRevoked: true);

            Assert.Equal(created, record.Created);
            Assert.Equal("a", record.Description);
            Assert.Equal(expires, record.Expires);
            Assert.Equal("b", record.Identity);
            Assert.Equal("c", record.TenantId);
            Assert.Equal(1, record.Key);
            Assert.Equal(lastUsed, record.LastUsed);
            Assert.Single(record.Scopes);
            var scope = record.Scopes[0];
            Assert.Equal("c", scope.Subject);
            Assert.Equal("d", scope.AllowedAction);
            Assert.Equal("e", record.Type);
            Assert.Equal("f", record.Value);
        }

        [Fact]
        public void Constructor_WithRevocationSource_Properties()
        {
            var testRevocationSource = "TestRevocationSource";
            var credential = new Credential(type: "a", value: "b");
            var record = new CredentialAuditRecord(credential, removedOrRevoked: true, revocationSource: testRevocationSource);

            Assert.Equal(testRevocationSource, record.RevocationSource);
            Assert.Equal("a", record.Type);
            Assert.Equal("b", record.Value);
        }
    }
}
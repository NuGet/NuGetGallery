// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Auditing.AuditedEntities;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class FailedAuthenticatedOperationAuditRecordTests
    {
        [Fact]
        public void Constructor_AcceptsNulls()
        {
            var record = new FailedAuthenticatedOperationAuditRecord(
                usernameOrEmail: null,
                action: AuditedAuthenticatedOperationAction.FailedLoginNoSuchUser,
                attemptedPackage: null,
                attemptedCredential: null);

            Assert.Null(record.UsernameOrEmail);
            Assert.Equal(AuditedAuthenticatedOperationAction.FailedLoginNoSuchUser, record.Action);
            Assert.Null(record.AttemptedPackage);
            Assert.Null(record.AttemptedCredential);
        }

        [Fact]
        public void Constructor_AcceptsEmptyStringUserNameOrEmail()
        {
            var record = new FailedAuthenticatedOperationAuditRecord(
                usernameOrEmail: "",
                action: AuditedAuthenticatedOperationAction.FailedLoginInvalidPassword,
                attemptedPackage: null,
                attemptedCredential: null);

            Assert.Equal("", record.UsernameOrEmail);
        }

        [Fact]
        public void Constructor_SetsProperties()
        {
            var identifier = new AuditedPackageIdentifier(id: "a", version: "1.0.0");
            var credential = new Credential(type: CredentialTypes.Password.V3, value: "b");
            var record = new FailedAuthenticatedOperationAuditRecord(
                usernameOrEmail: "c",
                action: AuditedAuthenticatedOperationAction.PackagePushAttemptByNonOwner,
                attemptedPackage: identifier,
                attemptedCredential: credential);

            Assert.Equal("c", record.UsernameOrEmail);
            Assert.Same(identifier, record.AttemptedPackage);
            Assert.NotNull(record.AttemptedCredential);
            Assert.Equal(credential.Type, record.AttemptedCredential.Type);
            Assert.Null(record.AttemptedCredential.Value);
            Assert.Equal(AuditedAuthenticatedOperationAction.PackagePushAttemptByNonOwner, record.Action);
        }

        [Fact]
        public void GetPath()
        {
            var record = new FailedAuthenticatedOperationAuditRecord(
                usernameOrEmail: null,
                action: AuditedAuthenticatedOperationAction.FailedLoginNoSuchUser,
                attemptedPackage: null,
                attemptedCredential: null);
            var actualResult = record.GetPath();

            Assert.Equal("all", actualResult);
        }
    }
}
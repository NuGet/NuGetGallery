// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class UserSecurityPolicyAuditRecordFacts
    {
        private IEnumerable<UserSecurityPolicy> Policies
        {
            get
            {
                yield return new UserSecurityPolicy("A", "B", "C");
            }
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullIfUsernameIsNull()
        {
            // Act & Assert.
            Assert.Throws<ArgumentNullException>(() => new UserSecurityPolicyAuditRecord(null,
                AuditedSecurityPolicyAction.Create, Policies, true));
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullIfUsernameIsEmpty()
        {
            // Act & Assert.
            Assert.Throws<ArgumentNullException>(() => new UserSecurityPolicyAuditRecord("",
                AuditedSecurityPolicyAction.Create, Policies, true));
        }

        [Fact]
        public void Constructor_ThrowsArgumentExceptionIfAffectedPoliciesIsNull()
        {
            // Act & Assert.
            Assert.Throws<ArgumentException>(() => new UserSecurityPolicyAuditRecord("user",
                AuditedSecurityPolicyAction.Create, null, true));
        }

        [Fact]
        public void Constructor_ThrowsArgumentExceptionIfAffectedPoliciesIsEmpty()
        {
            // Act & Assert.
            Assert.Throws<ArgumentException>(() => new UserSecurityPolicyAuditRecord("user",
                AuditedSecurityPolicyAction.Create, Array.Empty<UserSecurityPolicy>(), true));
        }

        [Fact]
        public void Constructor_SetsPropertiesForNonSuccess()
        {
            // Act.
            var record = new UserSecurityPolicyAuditRecord("D",
                AuditedSecurityPolicyAction.Verify, Policies, success: false, errorMessage: "E");

            // Assert.
            Assert.Equal("D", record.Username);
            Assert.False(record.Success);
            Assert.Equal("E", record.ErrorMessage);
            Assert.Single(record.AffectedPolicies);
            Assert.Equal("A", record.AffectedPolicies[0].Name);
            Assert.Equal("B", record.AffectedPolicies[0].Subscription);
            Assert.Equal("C", record.AffectedPolicies[0].Value);
        }

        [Fact]
        public void Constructor_SetsPropertiesForSuccess()
        {
            // Act.
            var record = new UserSecurityPolicyAuditRecord("D",
                AuditedSecurityPolicyAction.Verify, Policies, success: true);

            // Assert.
            Assert.Equal("D", record.Username);
            Assert.True(record.Success);
            Assert.Null(record.ErrorMessage);
            Assert.Single(record.AffectedPolicies);
            Assert.Equal("A", record.AffectedPolicies[0].Name);
            Assert.Equal("B", record.AffectedPolicies[0].Subscription);
            Assert.Equal("C", record.AffectedPolicies[0].Value);
        }

        [Fact]
        public void GetPath_ReturnsLowercaseUserPath()
        {
            // Act.
            var record = new UserSecurityPolicyAuditRecord("D",
                AuditedSecurityPolicyAction.Verify, Policies, success: true);

            // Assert.
            Assert.Equal("d", record.GetPath());
        }
    }
}

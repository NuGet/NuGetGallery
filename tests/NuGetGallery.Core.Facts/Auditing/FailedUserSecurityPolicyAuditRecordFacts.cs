// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class FailedUserSecurityPolicyAuditRecordFacts
    {
        private IEnumerable<UserSecurityPolicy> Policies
        {
            get
            {
                yield return new UserSecurityPolicy("A", "B", "C");
            }
        }

        [Fact]
        public void CtorThrowsIfUsernameIsNull()
        {
            // Act & Assert.
            Assert.Throws<ArgumentNullException>(() => new FailedUserSecurityPolicyAuditRecord(null, AuditedSecurityPolicyAction.Create, Policies));
        }

        [Fact]
        public void CtorThrowsIfUsernameIsEmpty()
        {
            // Act & Assert.
            Assert.Throws<ArgumentNullException>(() => new FailedUserSecurityPolicyAuditRecord("", AuditedSecurityPolicyAction.Create, Policies));
        }

        [Fact]
        public void CtorThrowsIfPoliciesIsNull()
        {
            // Act & Assert.
            Assert.Throws<ArgumentException>(() => new FailedUserSecurityPolicyAuditRecord("user", AuditedSecurityPolicyAction.Create, null));
        }

        [Fact]
        public void CtorThrowsIfPoliciesIsEmpty()
        {
            // Act & Assert.
            Assert.Throws<ArgumentException>(() => new FailedUserSecurityPolicyAuditRecord("user", AuditedSecurityPolicyAction.Create, new UserSecurityPolicy[0]));
        }
    }
}

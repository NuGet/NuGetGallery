// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        public void CtorThrowsIfUsernameIsNull()
        {
            // Act & Assert.
            Assert.Throws<ArgumentNullException>(() => new UserSecurityPolicyAuditRecord(null, AuditedSecurityPolicyAction.Create, Policies, true));
        }

        [Fact]
        public void CtorThrowsIfUsernameIsEmpty()
        {
            // Act & Assert.
            Assert.Throws<ArgumentNullException>(() => new UserSecurityPolicyAuditRecord("", AuditedSecurityPolicyAction.Create, Policies, true));
        }

        [Fact]
        public void CtorThrowsIfPoliciesIsNull()
        {
            // Act & Assert.
            Assert.Throws<ArgumentException>(() => new UserSecurityPolicyAuditRecord("user", AuditedSecurityPolicyAction.Create, null, true));
        }

        [Fact]
        public void CtorThrowsIfPoliciesIsEmpty()
        {
            // Act & Assert.
            Assert.Throws<ArgumentException>(() => new UserSecurityPolicyAuditRecord("user", AuditedSecurityPolicyAction.Create, new UserSecurityPolicy[0], true));
        }
    }
}

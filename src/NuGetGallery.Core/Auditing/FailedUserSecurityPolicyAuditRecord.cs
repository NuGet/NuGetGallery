// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    /// <summary>
    /// Audit record for failed user security policy evaluations.
    /// </summary>
    public class FailedUserSecurityPolicyAuditRecord : AuditRecord<AuditedSecurityPolicyAction>
    {
        public string Username { get; }

        public AuditedUserSecurityPolicy[] Policies { get; }

        public FailedUserSecurityPolicyAuditRecord(string username,
            AuditedSecurityPolicyAction action,
            IEnumerable<UserSecurityPolicy> policies)
            :base(action)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException(nameof(username));
            }
            if (policies == null || policies.Count() == 0)
            {
                throw new ArgumentException(nameof(policies));
            }

            Username = username;
            Policies = policies.Select(p => new AuditedUserSecurityPolicy(p)).ToArray();
        }

        public override string GetPath()
        {
            return Username.ToLowerInvariant();
        }
    }
}

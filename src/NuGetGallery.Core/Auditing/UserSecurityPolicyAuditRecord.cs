// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    /// <summary>
    /// Audit record for user security policy evaluations.
    /// </summary>
    public class UserSecurityPolicyAuditRecord : AuditRecord<AuditedSecurityPolicyAction>
    {
        public string Username { get; }

        public AuditedUserSecurityPolicy[] AffectedPolicies { get; }

        public bool Success { get; set; }

        public string ErrorMessage { get; }

        public UserSecurityPolicyAuditRecord(string username,
            AuditedSecurityPolicyAction action,
            IEnumerable<UserSecurityPolicy> affectedPolicies,
            bool success, string errorMessage = null)
            :base(action)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException(nameof(username));
            }
            if (affectedPolicies == null || affectedPolicies.Count() == 0)
            {
                throw new ArgumentException(nameof(affectedPolicies));
            }

            Username = username;
            AffectedPolicies = affectedPolicies.Select(p => new AuditedUserSecurityPolicy(p)).ToArray();
            Success = success;
            ErrorMessage = errorMessage;
        }

        public override string GetPath()
        {
            return Username.ToLowerInvariant();
        }
    }
}

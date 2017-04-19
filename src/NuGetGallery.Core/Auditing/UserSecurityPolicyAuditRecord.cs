// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    public abstract class UserSecurityPolicyAuditRecord : AuditRecord<UserSecurityPolicyAuditAction>
    {
        public string Username { get; }

        public AuditedSecurityPolicy SecurityPolicy { get; }

        public string ErrorMessage { get; }

        public UserSecurityPolicyAuditRecord(UserSecurityPolicyAuditAction action, string username,
            SecurityPolicy policy, string errorMessage = null)
            : base(action)
        {
            Username = username;
            SecurityPolicy = AuditedSecurityPolicy.CreateFrom(policy);
            ErrorMessage = errorMessage;
        }

        public override string GetPath()
        {
            return $"{SecurityPolicy.Type}/{Username}".ToLowerInvariant();
        }
    }
}

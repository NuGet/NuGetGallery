// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    public class PackageVerificationKeysPolicyAuditRecord : UserSecurityPolicyAuditRecord
    {
        public AuditedPackageIdentifier PushedPackage { get; }

        public PackageVerificationKeysPolicyAuditRecord(UserSecurityPolicyAuditAction action, string username,
            SecurityPolicy policy)
            : base(action, username, policy)
        {
        }

        public PackageVerificationKeysPolicyAuditRecord(UserSecurityPolicyAuditAction action, string username,
            SecurityPolicy policy, string errorMessage, AuditedPackageIdentifier pushedPackage)
            : base(action, username, policy, errorMessage)
        {
            PushedPackage = pushedPackage;
        }
    }
}

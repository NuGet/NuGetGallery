// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class AuditRecordTests
    {
        [Fact]
        public void SubclassingTypeSet_HasNotChanged()
        {
            // New records need to be added here.
            // Ensure you have sufficient test coverage for emitting this kind of audit records when adding new ones.
            HashSet<string> expectedAuditRecords = new HashSet<string>()
            {
                "NuGetGallery.Auditing.CertificateAuditRecord",
                "NuGetGallery.Auditing.DeleteAccountAuditRecord",
                "NuGetGallery.Auditing.ExternalSecurityTokenAuditRecord",
                "NuGetGallery.Auditing.FailedAuthenticatedOperationAuditRecord",
                "NuGetGallery.Auditing.FeatureFlagsAuditRecord",
                "NuGetGallery.Auditing.FederatedCredentialAuditRecord",
                "NuGetGallery.Auditing.FederatedCredentialPolicyAuditRecord",
                "NuGetGallery.Auditing.PackageAuditRecord",
                "NuGetGallery.Auditing.PackageRegistrationAuditRecord",
                "NuGetGallery.Auditing.ReservedNamespaceAuditRecord",
                "NuGetGallery.Auditing.UserAuditRecord",
                "NuGetGallery.Auditing.UserSecurityPolicyAuditRecord"
            };

            var actualAuditRecordTypeNames = typeof(AuditRecord).Assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(AuditRecord)))
                .Select(type => type.FullName)
                .OrderBy(typeName => typeName)
                .ToArray();

            Assert.Equal(expectedAuditRecords, actualAuditRecordTypeNames);
        }
    }
}

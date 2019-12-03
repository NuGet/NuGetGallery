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
            HashSet<string> expectedAuditRecords = new HashSet<string>()
            {
                "NuGetGallery.Auditing.CertificateAuditRecord",
                "NuGetGallery.Auditing.DeleteAccountAuditRecord",
                "NuGetGallery.Auditing.FailedAuthenticatedOperationAuditRecord",
                "NuGetGallery.Auditing.FeatureFlagsAuditRecord",
                "NuGetGallery.Auditing.PackageAuditRecord",
                "NuGetGallery.Auditing.PackageRegistrationAuditRecord",
                "NuGetGallery.Auditing.ReservedNamespaceAuditRecord",
                "NuGetGallery.Auditing.UserAuditRecord",
                "NuGetGallery.Auditing.UserSecurityPolicyAuditRecord",
            };

            var actualAuditRecordTypeNames = typeof(AuditRecord).Assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(AuditRecord)))
                .Select(type => type.FullName)
                .OrderBy(typeName => typeName)
                .ToArray();

            Assert.True(expectedAuditRecords.SequenceEqual(actualAuditRecordTypeNames),
                $"Audit record types have been {(actualAuditRecordTypeNames.Length > expectedAuditRecords.Count ? "added" : "removed")}.  " +
                $"Please evaluate this change against known {nameof(AuditingService)} implementations.");
        }
    }
}
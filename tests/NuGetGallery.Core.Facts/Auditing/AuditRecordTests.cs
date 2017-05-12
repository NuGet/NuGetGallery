// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class AuditRecordTests
    {
        [Fact]
        public void SubclassingTypeSet_HasNotChanged()
        {
            var actualAuditRecordTypeNames = typeof(AuditRecord).Assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(AuditRecord)))
                .Select(type => type.FullName)
                .OrderBy(typeName => typeName)
                .ToArray();

            var expectedAuditRecordTypeCount = 5;

            Assert.True(expectedAuditRecordTypeCount == actualAuditRecordTypeNames.Length,
                $"Audit record types have been {(actualAuditRecordTypeNames.Length > expectedAuditRecordTypeCount ? "added" : "removed")}.  " +
                $"Please evaluate this change against known {nameof(AuditingService)} implementations.");
            Assert.Equal("NuGetGallery.Auditing.FailedAuthenticatedOperationAuditRecord", actualAuditRecordTypeNames[0]);
            Assert.Equal("NuGetGallery.Auditing.PackageAuditRecord", actualAuditRecordTypeNames[1]);
            Assert.Equal("NuGetGallery.Auditing.PackageRegistrationAuditRecord", actualAuditRecordTypeNames[2]);
            Assert.Equal("NuGetGallery.Auditing.UserAuditRecord", actualAuditRecordTypeNames[3]);
            Assert.Equal("NuGetGallery.Auditing.UserSecurityPolicyAuditRecord", actualAuditRecordTypeNames[4]);
        }
    }
}
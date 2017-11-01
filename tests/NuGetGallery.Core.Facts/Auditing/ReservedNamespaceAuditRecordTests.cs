// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class ReservedNamespaceAuditRecordTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            var prefix = new ReservedNamespace("microsoft.", isSharedNamespace: false, isPrefix: true);
            var registrationsList = new List<PackageRegistration>
            {
                new PackageRegistration { Id = "Microsoft.Package1" },
                new PackageRegistration { Id = "Microsoft.AspNet.Package2" },
                new PackageRegistration { Id = "Microsoft.Package2" }
            };

            var owner = new User("microsoft");

            var record = new ReservedNamespaceAuditRecord(prefix,
                AuditedReservedNamespaceAction.AddOwner,
                owner.Username, 
                registrations: registrationsList);

            Assert.Equal(prefix.Value, record.Value);
            Assert.NotNull(record.AffectedReservedNamespace);
            Assert.NotNull(record.AffectedRegistrations);
            Assert.NotNull(record.AffectedOwner);
            Assert.Equal(prefix.Value, record.AffectedReservedNamespace.Value);
            Assert.Equal(prefix.IsSharedNamespace, record.AffectedReservedNamespace.IsSharedNamespace);
            Assert.Equal(prefix.IsPrefix, record.AffectedReservedNamespace.IsPrefix);
            Assert.Equal(AuditedReservedNamespaceAction.AddOwner, record.Action);
            Assert.Equal(registrationsList.Count, record.AffectedRegistrations.Length);
            Assert.Equal(owner.Username, record.AffectedOwner);
        }

        [Fact]
        public void GetPath_ReturnsLowerCasedId()
        {
            var record = new ReservedNamespaceAuditRecord(
                new ReservedNamespace() { Value = "MicroSoft." },
                AuditedReservedNamespaceAction.ReserveNamespace);

            var actualPath = record.GetPath();

            Assert.Equal("microsoft.", actualPath);
        }
    }
}
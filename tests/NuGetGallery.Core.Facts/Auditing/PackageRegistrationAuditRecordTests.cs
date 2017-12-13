// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Auditing
{
    public class PackageRegistrationAuditRecordTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            var record = new PackageRegistrationAuditRecord(
                new PackageRegistration() { Id = "a" },
                AuditedPackageRegistrationAction.AddOwner,
                owner: "b");

            Assert.Equal("a", record.Id);
            Assert.NotNull(record.RegistrationRecord);
            Assert.Equal("a", record.RegistrationRecord.Id);
            Assert.Equal("b", record.Owner);
            Assert.Equal(AuditedPackageRegistrationAction.AddOwner, record.Action);
        }

        [Fact]
        public void GetPath_ReturnsLowerCasedId()
        {
            var record = new PackageRegistrationAuditRecord(
                new PackageRegistration() { Id = "A" },
                AuditedPackageRegistrationAction.AddOwner,
                owner: "b");

            var actualPath = record.GetPath();

            Assert.Equal("a", actualPath);
        }
    }
}
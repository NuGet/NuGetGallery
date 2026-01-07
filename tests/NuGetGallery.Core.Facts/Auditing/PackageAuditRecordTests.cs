// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class PackageAuditRecordTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            var record = new PackageAuditRecord(
                new Package()
                {
                    Hash = "a",
                    PackageRegistration = new PackageRegistration() { Id = "b" },
                    Version = "1.0.0"
                },
                AuditedPackageAction.Create,
                reason: "c");

            Assert.Equal("b", record.Id);
            Assert.Equal("1.0.0", record.Version);
            Assert.Equal("a", record.Hash);
            Assert.NotNull(record.PackageRecord);
            Assert.Equal("a", record.PackageRecord.Hash);
            Assert.Equal("1.0.0", record.PackageRecord.Version);
            Assert.NotNull(record.RegistrationRecord);
            Assert.Equal("b", record.RegistrationRecord.Id);
            Assert.Equal("c", record.Reason);
            Assert.Equal(AuditedPackageAction.Create, record.Action);
        }

        [Fact]
        public void GetPath_ReturnsNormalizedPackageIdAndVersion()
        {
            var record = new PackageAuditRecord(
                new Package()
                {
                    Hash = "a",
                    PackageRegistration = new PackageRegistration() { Id = "B" },
                    Version = "1.0.0+c"
                },
                AuditedPackageAction.Create,
                reason: "d");

            var actualResult = record.GetPath();

            Assert.Equal("b/1.0.0", actualResult);
        }
    }
}
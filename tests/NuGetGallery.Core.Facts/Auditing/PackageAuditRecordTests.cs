// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        [Fact]
        public void RecordIsObfuscated()
        {
            // Arrange
            var record = new PackageAuditRecord(
                new Package()
                {
                    Hash = "a",
                    PackageRegistration = new PackageRegistration() { Id = "b" },
                    Version = "1.0.0",
                    FlattenedAuthors = "one,two",
                    UserKey = 100
                },
                AuditedPackageAction.Create,
                reason: "Just because.");

            // Act 
            var obfuscatedRecord = record.Obfuscate() as PackageAuditRecord;

            // Assert
            Assert.NotNull(obfuscatedRecord);

            Assert.Equal("b", record.Id);
            Assert.Equal("1.0.0", record.Version);
            Assert.Equal("a", record.Hash);
            Assert.NotNull(record.PackageRecord);
            Assert.Equal("a", record.PackageRecord.Hash);
            Assert.Equal("1.0.0", record.PackageRecord.Version);
            Assert.NotNull(record.RegistrationRecord);
            Assert.Equal("b", record.RegistrationRecord.Id);
            Assert.Equal("Just because.", record.Reason);
            Assert.Equal(100, record.PackageRecord.UserKey);
            Assert.Equal("one,two", record.PackageRecord.FlattenedAuthors);
            Assert.Equal(AuditedPackageAction.Create, record.Action);

            Assert.Equal("b", obfuscatedRecord.Id);
            Assert.Equal("1.0.0", obfuscatedRecord.Version);
            Assert.Equal("a", obfuscatedRecord.Hash);
            Assert.NotNull(obfuscatedRecord.PackageRecord);
            Assert.Equal("a", obfuscatedRecord.PackageRecord.Hash);
            Assert.Equal("1.0.0", obfuscatedRecord.PackageRecord.Version);
            Assert.NotNull(obfuscatedRecord.RegistrationRecord);
            Assert.Equal("b", obfuscatedRecord.RegistrationRecord.Id);
            Assert.Equal("Just because.", obfuscatedRecord.Reason);
            Assert.Equal(-1, obfuscatedRecord.PackageRecord.UserKey);
            Assert.Equal(string.Empty, obfuscatedRecord.PackageRecord.FlattenedAuthors);
            Assert.Equal(AuditedPackageAction.Create, record.Action);
        }
    }
}
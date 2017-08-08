// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGetGallery
{
    public class PackageDeletionRecordFacts
    {
        [Fact]
        public void PackageConstructor_ThrowsIfPackageNull()
        {
            Assert.Throws<ArgumentException>(() => new PackageDeletionRecord(null));
        }

        [Fact]
        public void PackageConstructor_ThrowsIfPackageRegistrationNull()
        {
            var package = new Package
            {
                PackageRegistration = null,
                NormalizedVersion = "1.0.0"
            };

            Assert.Throws<ArgumentException>(() => new PackageDeletionRecord(package));
        }

        [Fact]
        public void PackageConstructor_UsesNormalizedVersion()
        {
            var package = new Package
            {
                PackageRegistration = new PackageRegistration { Id = "TestPackage" },
                Version = "wrong",
                NormalizedVersion = "correct"
            };

            var deletionRecord = new PackageDeletionRecord(package);

            Assert.Equal(package.PackageRegistration.Id, deletionRecord.Id);
            Assert.Equal(package.NormalizedVersion, deletionRecord.NormalizedVersion);
            Assert.NotEqual(package.Version, deletionRecord.NormalizedVersion);
        }

        [Fact]
        public void PackageConstructor_UsesDateTimeUtcNow()
        {
            var package = new Package
            {
                PackageRegistration = new PackageRegistration { Id = "TestPackage" },
                NormalizedVersion = "1.0.0"
            };

            var now = DateTime.UtcNow;
            var deletionRecord = new PackageDeletionRecord(package);

            Assert.Equal(package.PackageRegistration.Id, deletionRecord.Id);
            Assert.Equal(package.NormalizedVersion, deletionRecord.NormalizedVersion);
            Assert.True(now <= deletionRecord.DeletedTimestamp, "The deletion record's timestamp for a package must be set when the record is created!");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void StringConstructor_ThrowsIfIdInvalid(string id)
        {
            Assert.Throws<ArgumentException>(() => new PackageDeletionRecord(id, "1.0.0", DateTime.UtcNow));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void StringConstructor_ThrowsIfVersionInvalid(string version)
        {
            Assert.Throws<ArgumentException>(() => new PackageDeletionRecord("testpackage", version, DateTime.UtcNow));
        }

        [Fact]
        public void StringConstructor_ConstructsFromValues()
        {
            var id = "TestPackage";
            var version = "1.2.3";
            var timestamp = new DateTime(2010, 12, 1);

            var deletionRecord = new PackageDeletionRecord(id, version, timestamp);

            Assert.Equal(id, deletionRecord.Id);
            Assert.Equal(version, deletionRecord.NormalizedVersion);
            Assert.Equal(timestamp, deletionRecord.DeletedTimestamp);
        }
    }
}

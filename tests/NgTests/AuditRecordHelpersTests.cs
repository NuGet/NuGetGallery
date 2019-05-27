// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NgTests
{
    public class AuditRecordHelpersTests
    {
        private readonly ILogger _logger;

        public AuditRecordHelpersTests(ITestOutputHelper testOutputHelper)
        {
            _logger = new TestLogger(testOutputHelper);
        }

        [Fact]
        public async Task GetDeletionAuditRecords_HandlesEmptyStorage()
        {
            var auditingStorage = new MemoryStorage();
            var records = await DeletionAuditEntry.GetAsync(auditingStorage, CancellationToken.None, logger: _logger);
            Assert.Empty(records);
        }

        [Fact]
        public async Task GetDeletionAuditRecords_WithoutFilter()
        {
            // Arrange
            var auditingStorage = new MemoryStorage();

            var addedAuditEntries = AddDummyAuditRecords(auditingStorage);

            // Act
            var auditEntries = await DeletionAuditEntry.GetAsync(auditingStorage, CancellationToken.None, logger: _logger);

            // Assert
            Assert.Equal(addedAuditEntries.Item1, auditEntries.Count());

            for (var i = 0; i < addedAuditEntries.Item1; i++)
            {
                Assert.Contains(auditEntries,
                    entry =>
                        entry.PackageId == addedAuditEntries.Item2[i] &&
                        entry.PackageVersion == addedAuditEntries.Item3[i] &&
                        entry.TimestampUtc.HasValue &&
                        entry.TimestampUtc.Value.Ticks == addedAuditEntries.Item4[i].Ticks);
            }
        }

        [Fact]
        public async Task GetDeletionAuditRecords_FiltersOnPackage_EntryExists()
        {
            // Arrange
            var auditingStorage = new MemoryStorage();

            var targetPackageIdentity = new PackageIdentity("targetPackage", NuGetVersion.Parse("3.2.1"));

            AddAuditRecordToMemoryStorage(auditingStorage, targetPackageIdentity);

            // Act
            var auditEntries = await DeletionAuditEntry.GetAsync(CreateStorageFactory(auditingStorage, targetPackageIdentity), CancellationToken.None, targetPackageIdentity, logger: _logger);

            // Assert
            Assert.Equal(1, auditEntries.Count());

            var auditEntry = auditEntries.ElementAt(0);

            Assert.Equal(targetPackageIdentity.Id, auditEntry.PackageId);
            Assert.Equal(targetPackageIdentity.Version.ToString(), auditEntry.PackageVersion);
        }

        [Fact]
        public async Task GetDeletionAuditRecords_FiltersOnPackage_EntryMissing()
        {
            // Arrange
            var auditingStorage = new MemoryStorage();

            var targetPackageIdentity = new PackageIdentity("targetPackage", NuGetVersion.Parse("3.2.1"));

            // Act
            var auditEntries = await DeletionAuditEntry.GetAsync(CreateStorageFactory(auditingStorage, targetPackageIdentity), CancellationToken.None, targetPackageIdentity, logger: _logger);

            // Assert
            Assert.Empty(auditEntries);
        }

        [Fact]
        public async Task GetDeletionAuditRecords_FiltersOnMinTimestamp_EntryExists()
        {
            // Arrange
            var auditingStorage = new MemoryStorage();

            var minTimestamp = DefaultAuditRecordTimeStamp.Add(new TimeSpan(1, 0, 0));

            AddAuditRecordToMemoryStorage(auditingStorage, package: null, timestamp: minTimestamp);
            AddDummyAuditRecords(auditingStorage);

            // Act
            var auditEntries = await DeletionAuditEntry.GetAsync(auditingStorage, CancellationToken.None, minTime: minTimestamp, logger: _logger);

            // Assert
            Assert.Equal(1, auditEntries.Count());

            var auditEntry = auditEntries.ElementAt(0);

            Assert.True(auditEntry.TimestampUtc.HasValue);
            Assert.True(auditEntry.TimestampUtc.Value.Ticks >= minTimestamp.Ticks);
        }

        [Fact]
        public async Task GetDeletionAuditRecords_FiltersOnMinTimestamp_EntryMissing()
        {
            // Arrange
            var auditingStorage = new MemoryStorage();

            var minTimestamp = DefaultAuditRecordTimeStamp.Add(new TimeSpan(1, 0, 0));

            AddDummyAuditRecords(auditingStorage);

            // Act
            var auditEntries = await DeletionAuditEntry.GetAsync(auditingStorage, CancellationToken.None, minTime: minTimestamp, logger: _logger);

            // Assert
            Assert.Empty(auditEntries);
        }

        [Fact]
        public async Task GetDeletionAuditRecords_FiltersOnMaxTimestamp_EntryExists()
        {
            // Arrange
            var auditingStorage = new MemoryStorage();

            var maxTimestamp = DefaultAuditRecordTimeStamp.Subtract(new TimeSpan(1, 0, 0));

            AddAuditRecordToMemoryStorage(auditingStorage, package: null, timestamp: maxTimestamp);
            AddDummyAuditRecords(auditingStorage);

            // Act
            var auditEntries = await DeletionAuditEntry.GetAsync(auditingStorage, CancellationToken.None, maxTime: maxTimestamp, logger: _logger);

            // Assert
            Assert.Equal(1, auditEntries.Count());

            var auditEntry = auditEntries.ElementAt(0);

            Assert.True(auditEntry.TimestampUtc.HasValue);
            Assert.True(auditEntry.TimestampUtc.Value.Ticks <= maxTimestamp.Ticks);
        }

        [Fact]
        public async Task GetDeletionAuditRecords_FiltersOnMaxTimestamp_EntryMissing()
        {
            // Arrange
            var auditingStorage = new MemoryStorage();

            var maxTimestamp = DefaultAuditRecordTimeStamp.Subtract(new TimeSpan(1, 0, 0));

            AddDummyAuditRecords(auditingStorage);

            // Act
            var auditEntries = await DeletionAuditEntry.GetAsync(auditingStorage, CancellationToken.None, maxTime: maxTimestamp, logger: _logger);

            // Assert
            Assert.Empty(auditEntries);
        }

        [Fact]
        public async Task GetDeletionAuditRecords_FiltersOnAll_EntryExists()
        {
            // Arrange
            var auditingStorage = new MemoryStorage();

            var targetPackageIdentity = new PackageIdentity("targetPackage", NuGetVersion.Parse("3.2.1"));
            var minTimestamp = DefaultAuditRecordTimeStamp.Subtract(new TimeSpan(2, 0, 0));
            var maxTimestamp = DefaultAuditRecordTimeStamp.Subtract(new TimeSpan(1, 0, 0));

            AddAuditRecordToMemoryStorage(auditingStorage, targetPackageIdentity, timestamp: minTimestamp.Add(new TimeSpan((maxTimestamp.Ticks - minTimestamp.Ticks) / 2)));
            AddDummyAuditRecords(auditingStorage, (count) => targetPackageIdentity.Id, (count) => targetPackageIdentity.Version.ToNormalizedString());

            // Act
            var auditEntries = await DeletionAuditEntry.GetAsync(CreateStorageFactory(auditingStorage, targetPackageIdentity), CancellationToken.None, targetPackageIdentity, minTimestamp, maxTimestamp, logger: _logger);

            // Assert
            Assert.Equal(1, auditEntries.Count());

            var auditEntry = auditEntries.ElementAt(0);

            Assert.Equal(targetPackageIdentity.Id, auditEntry.PackageId);
            Assert.Equal(targetPackageIdentity.Version.ToString(), auditEntry.PackageVersion);
            Assert.True(auditEntry.TimestampUtc.HasValue);
            Assert.True(auditEntry.TimestampUtc.Value.Ticks >= minTimestamp.Ticks);
            Assert.True(auditEntry.TimestampUtc.Value.Ticks <= maxTimestamp.Ticks);
        }

        [Fact]
        public async Task GetDeletionAuditRecords_FiltersOnAll_EntryMissing()
        {
            // Arrange
            var auditingStorage = new MemoryStorage();

            var targetPackageIdentity = new PackageIdentity("targetPackage", NuGetVersion.Parse("3.2.1"));
            var minTimestamp = DefaultAuditRecordTimeStamp.Subtract(new TimeSpan(2, 0, 0));
            var maxTimestamp = DefaultAuditRecordTimeStamp.Subtract(new TimeSpan(1, 0, 0));

            AddDummyAuditRecords(auditingStorage, (count) => targetPackageIdentity.Id, (count) => targetPackageIdentity.Version.ToNormalizedString());

            // Act
            var auditEntries = await DeletionAuditEntry.GetAsync(CreateStorageFactory(auditingStorage, targetPackageIdentity), CancellationToken.None, targetPackageIdentity, minTimestamp, maxTimestamp, logger: _logger);

            // Assert
            Assert.Empty(auditEntries);
        }

        private DateTime DefaultAuditRecordTimeStamp = DateTime.Parse("2017-03-31T11:57:35Z");

        private StorageFactory CreateStorageFactory(Storage storage, PackageIdentity targetPackage)
        {
            var auditingStorageFactoryMock = new Mock<StorageFactory>();

            auditingStorageFactoryMock
                .Setup(a => a.Create(It.Is<string>(n => n == $"{targetPackage.Id.ToLower()}/{targetPackage.Version.ToNormalizedString().ToLower()}")))
                .Returns(storage);

            return auditingStorageFactoryMock.Object;
        }

        private Tuple<int, List<string>, List<string>, List<DateTime>> AddDummyAuditRecords(MemoryStorage storage)
        {
            return AddDummyAuditRecords(
                storage,
                (count) => $"packageId{count}",
                (count) => $"packageVersion{count}");
        }

        private Tuple<int, List<string>, List<string>, List<DateTime>> AddDummyAuditRecords(
            MemoryStorage storage,
            Func<int, string> getPackageId,
            Func<int, string> getPackageVersion)
        {
            var packageIds = new List<string>();
            var packageVersions = new List<string>();
            var timestamps = new List<DateTime>();
            var count = 0;

            foreach (var fileSuffix in DeletionAuditEntry.FileNameSuffixes)
            {
                var packageId = getPackageId(count);
                var packageVersion = getPackageVersion(count);
                var timestamp = DefaultAuditRecordTimeStamp.Add(new TimeSpan(0, 0, count));

                AddAuditRecordToMemoryStorage(storage, packageId, packageVersion, timestamp, fileSuffix);

                packageIds.Add(packageId);
                packageVersions.Add(packageVersion);
                timestamps.Add(timestamp);
                count++;
            }

            Assert.Equal(count, packageIds.Count());
            Assert.Equal(count, packageVersions.Count());
            Assert.Equal(count, timestamps.Count());

            return Tuple.Create(count, packageIds, packageVersions, timestamps);
        }

        private void AddAuditRecordToMemoryStorage(MemoryStorage storage)
        {
            AddAuditRecordToMemoryStorage(storage, null, null, null);
        }

        private void AddAuditRecordToMemoryStorage(MemoryStorage storage, PackageIdentity package = null, DateTime? timestamp = null, string fileSuffix = null)
        {
            if (package == null)
            {
                package = new PackageIdentity("package", NuGetVersion.Parse("1.0.0"));
            }

            AddAuditRecordToMemoryStorage(storage, package.Id, package.Version.ToString(), timestamp, fileSuffix);
        }

        private void AddAuditRecordToMemoryStorage(MemoryStorage storage, string packageId = "package", string packageVersion = "1.0.0", DateTime? timestamp = null, string fileSuffix = null)
        {
            var auditTimestamp = timestamp ?? DefaultAuditRecordTimeStamp;

            if (fileSuffix == null)
            {
                fileSuffix = DeletionAuditEntry.FileNameSuffixes[0];
            }

            var auditRecord = new Uri(storage.BaseAddress, $"package/{packageId}/{packageVersion}/{auditTimestamp.ToFileTimeUtc()}{fileSuffix}");
            storage.Content.TryAdd(auditRecord, MakeDeleteAuditRecord(packageId, packageVersion, auditTimestamp));
            storage.ListMock.TryAdd(auditRecord, new StorageListItem(auditRecord, auditTimestamp));
        }

        private StringStorageContent MakeDeleteAuditRecord(string packageId, string packageVersion, DateTime timestamp)
        {
            return new StringStorageContent(TestCatalogEntries.DeleteAuditRecordForOtherPackage100
                .Replace("OtherPackage", packageId)
                .Replace("1.0.0", packageVersion)
                .Replace("2015-01-01T01:01:01.0748028Z", timestamp.ToString()));
        }
    }
}

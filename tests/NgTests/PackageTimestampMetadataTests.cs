// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;
using Xunit;

namespace NgTests.Validation
{
    public class PackageTimestampMetadataTests
    {
        public static IEnumerable<object[]> Last_PicksLatest_data
        {
            get
            {
                yield return new object[]
                {
                    true,
                    DateTime.MinValue,
                    DateTime.MaxValue,
                    null
                };

                yield return new object[]
                {
                    true,
                    DateTime.MaxValue,
                    DateTime.MinValue,
                    null
                };

                yield return new object[]
                {
                    false,
                    null,
                    null,
                    DateTime.MinValue
                };

                yield return new object[]
                {
                    false,
                    null,
                    null,
                    null
                };
            }
        }

        [Theory]
        [MemberData(nameof(Last_PicksLatest_data))]
        public void Last_PicksLatest(bool exists, DateTime? created, DateTime? lastEdited, DateTime? deleted)
        {
            // Act
            PackageTimestampMetadata package;

            if (exists)
            {
                package = PackageTimestampMetadata.CreateForPackageExistingOnFeed(created.Value, lastEdited.Value);
            }
            else
            {
                package = PackageTimestampMetadata.CreateForPackageMissingFromFeed(deleted);
            }

            // Assert
            Assert.True(created == null || package.Last >= created);
            Assert.True(lastEdited == null || package.Last >= lastEdited);
            Assert.True(deleted == null || package.Last >= deleted);
        }

        [Fact]
        public async Task FromCatalogEntry_HandlesCreatedLastEdited()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            var client = await CreateDummyClient(catalogStorage);

            var expectedTimestamp = DateTime.Parse("2015-01-01T00:00:00");
            var commitTimeStamp1 = DateTime.ParseExact(
                "2015-10-12T10:08:54.1506742Z",
                CatalogConstants.CommitTimeStampFormat,
                DateTimeFormatInfo.CurrentInfo,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var commitTimeStamp2 = DateTime.ParseExact(
                "2015-10-12T10:08:55.3335317Z",
                CatalogConstants.CommitTimeStampFormat,
                DateTimeFormatInfo.CurrentInfo,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            var tasks = new Task<PackageTimestampMetadata>[]
            {
                PackageTimestampMetadata.FromCatalogEntry(
                    client,
                    new CatalogIndexEntry(
                        new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/listedpackage.1.0.0.json"),
                        CatalogConstants.NuGetPackageDetails,
                        "9a37734f-1960-4c07-8934-c8bc797e35c1",
                        commitTimeStamp1,
                        new PackageIdentity("ListedPackage", new NuGetVersion("1.0.0")))),
                PackageTimestampMetadata.FromCatalogEntry(
                    client,
                    new CatalogIndexEntry(
                        new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/unlistedpackage.1.0.0.json"),
                        CatalogConstants.NuGetPackageDetails,
                        "9a37734f-1960-4c07-8934-c8bc797e35c1",
                        commitTimeStamp1,
                        new PackageIdentity("UnlistedPackage", new NuGetVersion("1.0.0")))),
                PackageTimestampMetadata.FromCatalogEntry(
                    client,
                    new CatalogIndexEntry(
                        new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.55/listedpackage.1.0.1.json"),
                        CatalogConstants.NuGetPackageDetails,
                        "8a9e7694-73d4-4775-9b7a-20aa59b9773e",
                        commitTimeStamp2,
                        new PackageIdentity("ListedPackage", new NuGetVersion("1.0.1"))))
            };

            // Act
            var entries = await Task.WhenAll(tasks);

            // Assert
            foreach (var entry in entries)
            {
                Assert.True(entry.Exists);
                Assert.Equal(expectedTimestamp.Ticks, entry.Created.Value.Ticks);
                Assert.Equal(expectedTimestamp.Ticks, entry.LastEdited.Value.Ticks);
                Assert.Null(entry.Deleted);
                Assert.Equal(expectedTimestamp.Ticks, entry.Last.Value.Ticks);
            }
        }

        [Fact]
        public async Task FromCatalogEntry_HandlesDeleted_NotNull()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            var client = await CreateDummyClient(catalogStorage);

            var expectedTimestamp = DateTime.Parse("2015-01-01T01:01:01.0748028");

            var uri = new Uri(catalogStorage.BaseAddress, "data/2015.10.13.06.40.07/otherpackage.1.0.0.json");
            var catalogIndexEntry = new CatalogIndexEntry(
                uri,
                CatalogConstants.NuGetPackageDelete,
                "afc8c1f4-486e-4142-b3ec-cf5841eb8883",
                DateTime.ParseExact(
                    "2015-10-13T06:40:07.7850657Z",
                    CatalogConstants.CommitTimeStampFormat,
                    DateTimeFormatInfo.CurrentInfo,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                new PackageIdentity("OtherPackage", new NuGetVersion("1.0.0")));

            // Act
            var entry = await PackageTimestampMetadata.FromCatalogEntry(client, catalogIndexEntry);

            // Assert
            Assert.False(entry.Exists);
            Assert.Null(entry.Created);
            Assert.Null(entry.LastEdited);
            Assert.Equal(expectedTimestamp.Ticks, entry.Deleted.Value.Ticks);
            Assert.Equal(expectedTimestamp.Ticks, entry.Last.Value.Ticks);
        }

        private async Task<CollectorHttpClient> CreateDummyClient(MemoryStorage catalogStorage)
        {
            var mockServer = new MockServerHttpClientHandler();

            mockServer.SetAction("/", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            await mockServer.AddStorageAsync(catalogStorage);

            return new CollectorHttpClient(mockServer);
        }
    }
}
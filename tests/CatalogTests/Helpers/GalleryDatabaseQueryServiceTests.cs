// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGet.Services.Metadata.Catalog.Helpers;
using Xunit;
using CatalogConstants = NuGet.Services.Metadata.Catalog.Constants;

namespace CatalogTests.Helpers
{
    public class GalleryDatabaseQueryServiceTests
    {
        public class TheBuildDB2CatalogSqlQueryMethod
        {
            [Fact]
            public void SelectsTopWithTies()
            {
                // Arrange
                var cursor = Db2CatalogCursor.ByCreated(DateTime.UtcNow, 10);

                // Act
                var queryString = GalleryDatabaseQueryService.BuildDb2CatalogSqlQuery(cursor);

                // Assert
                Assert.Contains($"SELECT TOP {cursor.Top} WITH TIES", queryString);
            }

            [Fact]
            public void OrdersByCursorColumnName_Created()
            {
                // Arrange
                var cursor = Db2CatalogCursor.ByCreated(DateTime.UtcNow, 20);

                // Act
                var queryString = GalleryDatabaseQueryService.BuildDb2CatalogSqlQuery(cursor);

                // Assert
                Assert.EndsWith($"ORDER BY P_EXT.[{cursor.ColumnName}], P_EXT.[{Db2CatalogProjectionColumnNames.Key}]", queryString);
            }

            [Fact]
            public void OrdersByCursorColumnName_LastEdited()
            {
                // Arrange
                var cursor = Db2CatalogCursor.ByLastEdited(DateTime.UtcNow, 20);

                // Act
                var queryString = GalleryDatabaseQueryService.BuildDb2CatalogSqlQuery(cursor);

                // Assert
                Assert.EndsWith($"ORDER BY P_EXT.[{cursor.ColumnName}], P_EXT.[{Db2CatalogProjectionColumnNames.Key}]", queryString);
            }

            [Fact]
            public void SelectsFromPackagesTable()
            {
                // Arrange
                var cursor = Db2CatalogCursor.ByCreated(DateTime.UtcNow, 20);

                // Act
                var queryString = GalleryDatabaseQueryService.BuildDb2CatalogSqlQuery(cursor);

                // Assert
                Assert.Contains("FROM [dbo].[Packages] AS P", queryString);
            }

            [Fact]
            public void JoinsWithPackageRegistrationsTable()
            {
                // Arrange
                var cursor = Db2CatalogCursor.ByCreated(DateTime.UtcNow, 20);

                // Act
                var queryString = GalleryDatabaseQueryService.BuildDb2CatalogSqlQuery(cursor);

                // Assert
                Assert.Contains("INNER JOIN [dbo].[PackageRegistrations] AS PR ON P.[PackageRegistrationKey] = PR.[Key]", queryString);
            }

            [Fact]
            public void JoinsWithPackageVulnerabilitiesTables()
            {
                // Arrange
                var cursor = Db2CatalogCursor.ByCreated(DateTime.UtcNow, 20);

                // Act
                var queryString = GalleryDatabaseQueryService.BuildDb2CatalogSqlQuery(cursor);

                // Assert
                Assert.Contains("LEFT JOIN [dbo].[VulnerablePackageVersionRangePackages] AS VPVRP ON VPVRP.[Package_Key] = P_EXT.[Key]", queryString);
                Assert.Contains("LEFT JOIN [dbo].[VulnerablePackageVersionRanges] AS VPVR ON VPVR.[Key] = VPVRP.[VulnerablePackageVersionRange_Key]", queryString);
                Assert.Contains("LEFT JOIN [dbo].[PackageVulnerabilities] AS PV ON PV.[Key] = VPVR.[VulnerabilityKey]", queryString);
            }

            [Fact]
            public void OnlyConsidersPackagesWithAvailableStatus()
            {
                // Arrange
                var cursor = Db2CatalogCursor.ByCreated(DateTime.UtcNow, 20);

                // Act
                var queryString = GalleryDatabaseQueryService.BuildDb2CatalogSqlQuery(cursor);

                // Assert
                Assert.Contains($"WHERE P.[PackageStatusKey] = {(int)PackageStatus.Available}", queryString);
            }
        }

        public class TheOrderPackagesByKeyDateMethod
        {
            [Fact]
            public void OrdersBySelectedPropertyDescending()
            {
                // Arrange
                var utcNow = DateTime.UtcNow;
                var firstCreatedPackage = new FeedPackageDetails(
                        new Uri("https://unittest.org/packages/Package.Id/1.0.1"),
                        createdDate: utcNow.AddDays(-2),
                        lastEditedDate: utcNow,
                        publishedDate: utcNow.AddDays(-2),
                        packageId: "Package.Id",
                        packageNormalizedVersion: "1.0.1",
                        packageFullVersion: "1.0.1");
                var firstEditedPackage = new FeedPackageDetails(
                        new Uri("https://unittest.org/packages/Package.Id/1.0.0"),
                        createdDate: utcNow.AddDays(-1),
                        lastEditedDate: utcNow.AddHours(-8),
                        publishedDate: utcNow.AddDays(-1),
                        packageId: "Package.Id",
                        packageNormalizedVersion: "1.0.0",
                        packageFullVersion: "1.0.0");

                var packages = new List<FeedPackageDetails>
                {
                    firstCreatedPackage,
                    firstEditedPackage
                };

                // Act
                var orderedByCreatedDate = GalleryDatabaseQueryService.OrderPackagesByKeyDate(packages, p => p.CreatedDate, CatalogConstants.MaxPageSize);
                var orderedByLastEditedDate = GalleryDatabaseQueryService.OrderPackagesByKeyDate(packages, p => p.LastEditedDate, CatalogConstants.MaxPageSize);

                // Assert
                Assert.Equal(firstCreatedPackage.CreatedDate, orderedByCreatedDate.First().Key);
                Assert.Single(orderedByCreatedDate.First().Value);
                Assert.Contains(firstCreatedPackage, orderedByCreatedDate.First().Value);

                Assert.Equal(firstEditedPackage.LastEditedDate, orderedByLastEditedDate.First().Key);
                Assert.Single(orderedByLastEditedDate.First().Value);
                Assert.Contains(firstEditedPackage, orderedByLastEditedDate.First().Value);
            }

            [Fact]
            public void WillSkipLastTimestampIfItWouldResultInPageOverflow()
            {
                // Arrange
                const int top = 20;
                var utcNow = DateTime.UtcNow;
                var packages = new List<FeedPackageDetails>();

                // Ensure the top 19 timestamps have a package each.
                for (int i = 1; i < top; i++)
                {
                    packages.Add(new FeedPackageDetails(
                        new Uri($"https://unittest.org/packages/Package.Id{i}/1.0.{i}"),
                        createdDate: utcNow.AddDays(-i),
                        lastEditedDate: utcNow.AddDays(-i),
                        publishedDate: utcNow.AddDays(-i),
                        packageId: $"Package.Id{i}",
                        packageNormalizedVersion: $"1.0.{i}",
                        packageFullVersion: $"1.0.{i}"));
                }

                // Ensure the last timestamp has enough packages to have it exceed the max page size.
                // This simulates TOP 20 WITH TIES behavior encountering bulk changes to these packages
                var lastTimestamp = utcNow.AddHours(-8);
                for (int i = 0; i < CatalogConstants.MaxPageSize - top + 2; i++)
                {
                    packages.Add(new FeedPackageDetails(
                        new Uri($"https://unittest.org/packages/BatchUpdatedPackage.Id/1.0.{i}"),
                        createdDate: utcNow.AddHours(-9),
                        lastEditedDate: lastTimestamp,
                        publishedDate: utcNow.AddHours(-9),
                        packageId: "BatchUpdatedPackage.Id",
                        packageNormalizedVersion: $"1.0.{i}",
                        packageFullVersion: $"1.0.{i}"));
                }

                // Act
                var orderedByLastEditedDate = GalleryDatabaseQueryService.OrderPackagesByKeyDate(packages, p => p.LastEditedDate, CatalogConstants.MaxPageSize);

                // Assert
                Assert.Equal(top - 1, orderedByLastEditedDate.Count);
                Assert.DoesNotContain(lastTimestamp, orderedByLastEditedDate.Keys);
                Assert.True(orderedByLastEditedDate.Values.Sum(v => v.Count) <= CatalogConstants.MaxPageSize);
            }

            [Fact]
            public void WillSkipAnyTimestampThatWouldResultInPageOverflow()
            {
                // Arrange
                const int top = 20;
                var utcNow = DateTime.UtcNow;
                var packages = new List<FeedPackageDetails>();

                // Ensure the top 10 timestamps have a package each.
                for (int i = 10; i < top; i++)
                {
                    packages.Add(new FeedPackageDetails(
                        new Uri($"https://unittest.org/packages/Package.Id{i}/1.0.{i}"),
                        createdDate: utcNow.AddDays(-i),
                        lastEditedDate: utcNow.AddDays(-i),
                        publishedDate: utcNow.AddDays(-i),
                        packageId: $"Package.Id{i}",
                        packageNormalizedVersion: $"1.0.{i}",
                        packageFullVersion: $"1.0.{i}"));
                }

                // Ensure the next timestamp has enough packages to have it reach the max page size.
                // This simulates encountering bulk changes at timestamp 11.
                var timestampForBulkChanges = utcNow.AddHours(-8);
                for (int i = 0; i < CatalogConstants.MaxPageSize - top + 10; i++)
                {
                    packages.Add(new FeedPackageDetails(
                        new Uri($"https://unittest.org/packages/BatchUpdatedPackage.Id/1.0.{i}"),
                        createdDate: utcNow.AddHours(-9),
                        lastEditedDate: timestampForBulkChanges,
                        publishedDate: utcNow.AddHours(-9),
                        packageId: "BatchUpdatedPackage.Id",
                        packageNormalizedVersion: $"1.0.{i}",
                        packageFullVersion: $"1.0.{i}"));
                }

                // Add some more timestamps with a package each to the end of the top 20 resultset.
                for (int i = top - 10 + 1; i < top; i++)
                {
                    packages.Add(new FeedPackageDetails(
                        new Uri($"https://unittest.org/packages/Another.Package.Id{i}/1.0.{i}"),
                        createdDate: utcNow.AddDays(-i),
                        lastEditedDate: utcNow.AddMinutes(-i),
                        publishedDate: utcNow.AddDays(-i),
                        packageId: $"Another.Package.Id{i}",
                        packageNormalizedVersion: $"1.0.{i}",
                        packageFullVersion: $"1.0.{i}"));
                }

                // Act
                var orderedByLastEditedDate = GalleryDatabaseQueryService.OrderPackagesByKeyDate(packages, p => p.LastEditedDate, CatalogConstants.MaxPageSize);

                // Assert
                Assert.Equal(top - 10 + 1, orderedByLastEditedDate.Count);
                Assert.Contains(timestampForBulkChanges, orderedByLastEditedDate.Keys);
                Assert.True(orderedByLastEditedDate.Values.Sum(v => v.Count) <= CatalogConstants.MaxPageSize);
            }

            [Fact]
            public void WillNotSkipSingleTimestampIfThatWouldResultInPageOverflow()
            {
                // Arrange
                var utcNow = DateTime.UtcNow;
                var packages = new List<FeedPackageDetails>();

                // Ensure the first timestamp has enough packages to overflow.
                var timestampForBulkChanges = utcNow.AddHours(-8);
                for (int i = 0; i < CatalogConstants.MaxPageSize + 1; i++)
                {
                    packages.Add(new FeedPackageDetails(
                        new Uri($"https://unittest.org/packages/BatchUpdatedPackage.Id{i}/1.0.{i}"),
                        createdDate: utcNow.AddDays(-i),
                        lastEditedDate: timestampForBulkChanges,
                        publishedDate: utcNow.AddDays(-i),
                        packageId: $"BatchUpdatedPackage.Id{i}",
                        packageNormalizedVersion: $"1.0.{i}",
                        packageFullVersion: $"1.0.{i}"));
                }

                // Add 19 more timestamps to simulate top 20.
                for (int i = 0; i < 19; i++)
                {
                    packages.Add(new FeedPackageDetails(
                        new Uri($"https://unittest.org/packages/Package.Id/1.0.{i}"),
                        createdDate: utcNow.AddHours(-9),
                        lastEditedDate: utcNow.AddMinutes(-i),
                        publishedDate: utcNow.AddHours(-9),
                        packageId: "Package.Id",
                        packageNormalizedVersion: $"1.0.{i}",
                        packageFullVersion: $"1.0.{i}"));
                }

                // Act
                var orderedByLastEditedDate = GalleryDatabaseQueryService.OrderPackagesByKeyDate(packages, p => p.LastEditedDate, CatalogConstants.MaxPageSize);

                // Assert
                Assert.Single(orderedByLastEditedDate);
                Assert.Equal(timestampForBulkChanges, orderedByLastEditedDate.Single().Key);
                Assert.True(orderedByLastEditedDate.Values.Sum(v => v.Count) >= CatalogConstants.MaxPageSize);
            }
        }
    }
}
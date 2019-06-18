// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Metadata.Catalog.Helpers;
using Xunit;
using Constants = NuGet.Services.Metadata.Catalog.Constants;

namespace CatalogTests.Helpers
{
    public class Db2CatalogProjectionTests
    {
        [Fact]
        public void UnpublishedDateDidNotChange()
        {
            var expected = new DateTime(1900, 1, 1, 0, 0, 0);
            Assert.Equal(expected, Constants.UnpublishedDate);
        }

        public class TheConstructor
        {
            [Fact]
            public void ThrowsForNullArgument()
            {
                Assert.Throws<ArgumentNullException>(() => new Db2CatalogProjection(null));
            }
        }

        public class TheReadFeedPackageDetailsFromDataReaderMethod
        {
            private const string PackageContentUrlFormat = "https://unittest.org/packages/{id-lower}/{version-lower}.nupkg";
            private readonly PackageContentUriBuilder _packageContentUriBuilder;
            private readonly Db2CatalogProjection _db2catalogProjection;

            public TheReadFeedPackageDetailsFromDataReaderMethod()
            {
                _packageContentUriBuilder = new PackageContentUriBuilder(PackageContentUrlFormat);
                _db2catalogProjection = new Db2CatalogProjection(_packageContentUriBuilder);
            }

            [Fact]
            public void ThrowsForNullArgument()
            {
                Assert.Throws<ArgumentNullException>(() => _db2catalogProjection.ReadFeedPackageDetailsFromDataReader(null));
            }

            [Theory]
            [InlineData(true, true)]
            [InlineData(true, false)]
            [InlineData(false, true)]
            [InlineData(false, false)]
            public void PerformsCorrectProjections(bool listed, bool hideLicenseReport)
            {
                // Arrange
                const string packageId = "Package.Id";
                const string normalizedPackageVersion = "1.0.0";
                const string fullPackageVersion = "1.0.0.0";
                const string licenseNames = "MIT";
                const string licenseReportUrl = "https://unittest.org/licenses/MIT";
                const bool requiresLicenseAcceptance = true;

                var utcNow = DateTime.UtcNow;
                var createdDate = utcNow.AddDays(-1);
                var lastEditedDate = utcNow.AddMinutes(-5);
                var publishedDate = createdDate;
                var expectedContentUri = _packageContentUriBuilder.Build(packageId, normalizedPackageVersion);
                var expectedPublishedDate = listed ? publishedDate : Constants.UnpublishedDate;
                var expectedLicenseNames = hideLicenseReport ? null : licenseNames;
                var expectedLicenseReportUrl = hideLicenseReport ? null : licenseReportUrl;
                var expectedRequiresLicenseAcceptance = true;

                var dataRecordMock = MockDataReader(
                    packageId,
                    normalizedPackageVersion,
                    fullPackageVersion,
                    createdDate,
                    lastEditedDate,
                    publishedDate,
                    listed,
                    hideLicenseReport,
                    licenseNames,
                    licenseReportUrl,
                    requiresLicenseAcceptance);

                // Act
                var projection = _db2catalogProjection.ReadFeedPackageDetailsFromDataReader(dataRecordMock.Object);

                // Assert
                Assert.Equal(packageId, projection.PackageId);
                Assert.Equal(normalizedPackageVersion, projection.PackageNormalizedVersion);
                Assert.Equal(fullPackageVersion, projection.PackageFullVersion);
                Assert.Equal(createdDate, projection.CreatedDate);
                Assert.Equal(lastEditedDate, projection.LastEditedDate);
                Assert.Equal(expectedPublishedDate, projection.PublishedDate);
                Assert.Equal(expectedContentUri, projection.ContentUri);
                Assert.Equal(expectedLicenseNames, projection.LicenseNames);
                Assert.Equal(expectedLicenseReportUrl, projection.LicenseReportUrl);
                Assert.Equal(expectedRequiresLicenseAcceptance, projection.RequiresLicenseAcceptance);
                Assert.Null(projection.DeprecationInfo);
            }

            private static Mock<DbDataReader> MockDataReader(
                string packageId,
                string normalizedPackageVersion,
                string fullPackageVersion,
                DateTime createdDate,
                DateTime lastEditedDate,
                DateTime publishedDate,
                bool listed,
                bool hideLicenseReport,
                string licenseNames,
                string licenseReportUrl,
                bool requiresLicenseAcceptance)
            {
                const int ordinalCreated = 3;
                const int ordinalLastEdited = 4;
                const int ordinalPublished = 5;
                const int ordinalListed = 6;
                const int ordinalHideLicenseReport = 7;
                const int ordinalRequiresLicenseAcceptance = 10;

                var dataReaderMock = new Mock<DbDataReader>(MockBehavior.Strict);

                dataReaderMock.SetupGet(m => m[Db2CatalogProjectionColumnNames.PackageId]).Returns(packageId);
                dataReaderMock.SetupGet(m => m[Db2CatalogProjectionColumnNames.NormalizedVersion]).Returns(normalizedPackageVersion);
                dataReaderMock.SetupGet(m => m[Db2CatalogProjectionColumnNames.FullVersion]).Returns(fullPackageVersion);

                dataReaderMock.SetupGet(m => m[Db2CatalogProjectionColumnNames.LicenseNames]).Returns(licenseNames);
                dataReaderMock.SetupGet(m => m[Db2CatalogProjectionColumnNames.LicenseReportUrl]).Returns(licenseReportUrl);

                dataReaderMock.Setup(m => m.GetOrdinal(Db2CatalogProjectionColumnNames.Listed)).Returns(ordinalListed);
                dataReaderMock.Setup(m => m.GetBoolean(ordinalListed)).Returns(listed);

                dataReaderMock.Setup(m => m.GetOrdinal(Db2CatalogProjectionColumnNames.HideLicenseReport)).Returns(ordinalHideLicenseReport);
                dataReaderMock.Setup(m => m.GetBoolean(ordinalHideLicenseReport)).Returns(hideLicenseReport);

                dataReaderMock.SetupGet(m => m[Db2CatalogProjectionColumnNames.Created]).Returns(createdDate);
                dataReaderMock.Setup(m => m.GetOrdinal(Db2CatalogProjectionColumnNames.Created)).Returns(ordinalCreated);
                dataReaderMock.Setup(m => m.GetDateTime(ordinalCreated)).Returns(createdDate);

                dataReaderMock.SetupGet(m => m[Db2CatalogProjectionColumnNames.LastEdited]).Returns(lastEditedDate);
                dataReaderMock.Setup(m => m.GetOrdinal(Db2CatalogProjectionColumnNames.LastEdited)).Returns(ordinalLastEdited);
                dataReaderMock.Setup(m => m.GetDateTime(ordinalLastEdited)).Returns(lastEditedDate);

                dataReaderMock.SetupGet(m => m[Db2CatalogProjectionColumnNames.Published]).Returns(publishedDate);
                dataReaderMock.Setup(m => m.GetOrdinal(Db2CatalogProjectionColumnNames.Published)).Returns(ordinalPublished);
                dataReaderMock.Setup(m => m.GetDateTime(ordinalPublished)).Returns(publishedDate);

                dataReaderMock.SetupGet(m => m[Db2CatalogProjectionColumnNames.RequiresLicenseAcceptance]).Returns(requiresLicenseAcceptance);
                dataReaderMock.Setup(m => m.GetOrdinal(Db2CatalogProjectionColumnNames.RequiresLicenseAcceptance)).Returns(ordinalRequiresLicenseAcceptance);
                dataReaderMock.Setup(m => m.GetBoolean(ordinalRequiresLicenseAcceptance)).Returns(requiresLicenseAcceptance);

                // Simulate that these columns do not exist in the resultset.
                dataReaderMock.Setup(m => m.GetOrdinal(Db2CatalogProjectionColumnNames.DeprecationStatus)).Throws<IndexOutOfRangeException>();

                return dataReaderMock;
            }
        }

        public class TheReadDeprecationInfoFromDataReaderMethod
        {
            private const string PackageContentUrlFormat = "https://unittest.org/packages/{id-lower}/{version-lower}.nupkg";
            private readonly PackageContentUriBuilder _packageContentUriBuilder;
            private readonly Db2CatalogProjection _db2catalogProjection;

            public TheReadDeprecationInfoFromDataReaderMethod()
            {
                _packageContentUriBuilder = new PackageContentUriBuilder(PackageContentUrlFormat);
                _db2catalogProjection = new Db2CatalogProjection(_packageContentUriBuilder);
            }

            [Fact]
            public void ThrowsForNullArgument()
            {
                Assert.Throws<ArgumentNullException>(() => _db2catalogProjection.ReadDeprecationInfoFromDataReader(null));
            }

            [Theory]
            [InlineData(null, null)]
            [InlineData(null, "1.0.0")]
            [InlineData("alternate.package.id", null)]
            [InlineData("alternate.package.id", "1.0.0")]
            public void PerformsCorrectProjections(string alternatePackageId, string alternatePackageVersionRange)
            {
                foreach (var packageDeprecationStatus in Enum.GetValues(typeof(PackageDeprecationStatus)).Cast<PackageDeprecationStatus>())
                {
                    VerifyDeprecationProjections(
                        packageDeprecationStatus,
                        alternatePackageId,
                        alternatePackageVersionRange);
                }
            }

            private void VerifyDeprecationProjections(
                PackageDeprecationStatus status,
                string alternatePackageId,
                string alternatePackageVersionRange)
            {
                // Arrange
                string customMessage = null;

                Mock<DbDataReader> dataReaderMock;
                if (status == PackageDeprecationStatus.NotDeprecated)
                {
                    dataReaderMock = MockDataReader(status);
                }
                else
                {
                    customMessage = "custom message";

                    dataReaderMock = MockDataReader(
                        status,
                        customMessage,
                        alternatePackageId,
                        alternatePackageVersionRange);
                }

                // Act
                var projection = _db2catalogProjection.ReadDeprecationInfoFromDataReader(dataReaderMock.Object);

                // Assert
                if (status == PackageDeprecationStatus.NotDeprecated)
                {
                    Assert.Null(projection);
                }
                else
                {
                    Assert.NotNull(projection);

                    foreach (var deprecationStatusFlag in Enum.GetValues(typeof(PackageDeprecationStatus))
                        .Cast<PackageDeprecationStatus>()
                        .Except(new[] { PackageDeprecationStatus.NotDeprecated }))
                    {
                        if (status.HasFlag(deprecationStatusFlag))
                        {
                            Assert.Contains(deprecationStatusFlag.ToString(), projection.Reasons);
                        }
                        else
                        {
                            Assert.DoesNotContain(deprecationStatusFlag.ToString(), projection.Reasons);
                        }
                    }

                    Assert.Equal(customMessage, projection.Message);
                    Assert.Equal(alternatePackageId, projection.AlternatePackageId);

                    if (alternatePackageId != null && alternatePackageVersionRange == null)
                    {
                        Assert.Equal(Db2CatalogProjection.AlternatePackageVersionWildCard, projection.AlternatePackageRange);
                    }
                    else if (alternatePackageId == null)
                    {
                        Assert.Null(projection.AlternatePackageRange);
                    }
                    else
                    {
                        Assert.Equal(
                            $"[{alternatePackageVersionRange}, {alternatePackageVersionRange}]",
                            projection.AlternatePackageRange);
                    }
                }
            }

            private static Mock<DbDataReader> MockDataReader(
                PackageDeprecationStatus deprecationStatus,
                string message = null,
                string alternatePackageId = null,
                string alternatePackageVersionRange = null)
            {
                var dataReaderMock = new Mock<DbDataReader>(MockBehavior.Strict);

                if (deprecationStatus == PackageDeprecationStatus.NotDeprecated)
                {
                    // Simulate that these columns do not exist in the resultset.
                    dataReaderMock.Setup(m => m.GetOrdinal(Db2CatalogProjectionColumnNames.DeprecationStatus)).Throws<IndexOutOfRangeException>();
                }
                else
                {
                    const int ordinalDeprecationStatus = 7;
                    dataReaderMock.Setup(m => m.GetOrdinal(Db2CatalogProjectionColumnNames.DeprecationStatus)).Returns(ordinalDeprecationStatus);
                    dataReaderMock.Setup(m => m.IsDBNull(ordinalDeprecationStatus)).Returns(false);
                    dataReaderMock.Setup(m => m.GetInt32(ordinalDeprecationStatus)).Returns((int)deprecationStatus);

                    const int ordinalDeprecationMessage = 8;
                    dataReaderMock.Setup(m => m.GetOrdinal(Db2CatalogProjectionColumnNames.DeprecationMessage)).Returns(ordinalDeprecationMessage);
                    dataReaderMock.Setup(m => m.IsDBNull(ordinalDeprecationMessage)).Returns(message == null);
                    dataReaderMock.Setup(m => m.GetString(ordinalDeprecationMessage)).Returns(message);

                    const int ordinalAlternatePackageId = 9;
                    dataReaderMock.Setup(m => m.GetOrdinal(Db2CatalogProjectionColumnNames.AlternatePackageId)).Returns(ordinalAlternatePackageId);
                    dataReaderMock.Setup(m => m.IsDBNull(ordinalAlternatePackageId)).Returns(alternatePackageId == null);
                    dataReaderMock.Setup(m => m.GetString(ordinalAlternatePackageId)).Returns(alternatePackageId);

                    const int ordinalAlternatePackageVersionRange = 10;
                    dataReaderMock.Setup(m => m.GetOrdinal(Db2CatalogProjectionColumnNames.AlternatePackageVersion)).Returns(ordinalAlternatePackageVersionRange);
                    dataReaderMock.Setup(m => m.IsDBNull(ordinalAlternatePackageVersionRange)).Returns(alternatePackageVersionRange == null);
                    dataReaderMock.Setup(m => m.GetString(ordinalAlternatePackageVersionRange)).Returns(alternatePackageVersionRange);
                }

                return dataReaderMock;
            }
        }
    }
}

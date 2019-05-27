// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using Moq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using Xunit;

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

        public class TheFromDataRecordMethod
        {
            private const string PackageContentUrlFormat = "https://unittest.org/packages/{id-lower}/{version-lower}.nupkg";
            private readonly PackageContentUriBuilder _packageContentUriBuilder;
            private readonly Db2CatalogProjection _db2catalogProjection;

            public TheFromDataRecordMethod()
            {
                _packageContentUriBuilder = new PackageContentUriBuilder(PackageContentUrlFormat);
                _db2catalogProjection = new Db2CatalogProjection(_packageContentUriBuilder);
            }

            [Fact]
            public void ThrowsForNullArgument()
            {
                Assert.Throws<ArgumentNullException>(() => _db2catalogProjection.FromDataRecord(null));
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
                const string licenseNames = "MIT";
                const string licenseReportUrl = "https://unittest.org/licenses/MIT";

                var utcNow = DateTime.UtcNow;
                var createdDate = utcNow.AddDays(-1);
                var lastEditedDate = utcNow.AddMinutes(-5);
                var publishedDate = createdDate;
                var expectedContentUri = _packageContentUriBuilder.Build(packageId, normalizedPackageVersion);
                var expectedPublishedDate = listed ? publishedDate : Constants.UnpublishedDate;
                var expectedLicenseNames = hideLicenseReport ? null : licenseNames;
                var expectedLicenseReportUrl = hideLicenseReport ? null : licenseReportUrl;

                var dataRecordMock = MockDataRecord(
                    packageId,
                    normalizedPackageVersion,
                    createdDate,
                    lastEditedDate,
                    publishedDate,
                    listed,
                    hideLicenseReport,
                    licenseNames,
                    licenseReportUrl);

                // Act
                var projection = _db2catalogProjection.FromDataRecord(dataRecordMock.Object);

                // Assert
                Assert.Equal(packageId, projection.PackageId);
                Assert.Equal(normalizedPackageVersion, projection.PackageVersion);
                Assert.Equal(createdDate, projection.CreatedDate);
                Assert.Equal(lastEditedDate, projection.LastEditedDate);
                Assert.Equal(expectedPublishedDate, projection.PublishedDate);
                Assert.Equal(expectedContentUri, projection.ContentUri);
                Assert.Equal(expectedLicenseNames, projection.LicenseNames);
                Assert.Equal(expectedLicenseReportUrl, projection.LicenseReportUrl);
            }

            private static Mock<IDataRecord> MockDataRecord(
                string packageId,
                string normalizedPackageVersion,
                DateTime createdDate,
                DateTime lastEditedDate,
                DateTime publishedDate,
                bool listed,
                bool hideLicenseReport,
                string licenseNames,
                string licenseReportUrl)
            {
                const int ordinalCreated = 2;
                const int ordinalLastEdited = 3;
                const int ordinalPublished = 4;
                const int ordinalListed = 5;
                const int ordinalHideLicenseReport = 6;

                var dataRecordMock = new Mock<IDataRecord>(MockBehavior.Strict);

                dataRecordMock.SetupGet(m => m["Id"]).Returns(packageId);
                dataRecordMock.SetupGet(m => m["NormalizedVersion"]).Returns(normalizedPackageVersion);

                dataRecordMock.SetupGet(m => m["LicenseNames"]).Returns(licenseNames);
                dataRecordMock.SetupGet(m => m["LicenseReportUrl"]).Returns(licenseReportUrl);

                dataRecordMock.Setup(m => m.GetOrdinal("Listed")).Returns(ordinalListed);
                dataRecordMock.Setup(m => m.GetBoolean(ordinalListed)).Returns(listed);

                dataRecordMock.Setup(m => m.GetOrdinal("HideLicenseReport")).Returns(ordinalHideLicenseReport);
                dataRecordMock.Setup(m => m.GetBoolean(ordinalHideLicenseReport)).Returns(hideLicenseReport);

                dataRecordMock.SetupGet(m => m["Created"]).Returns(createdDate);
                dataRecordMock.Setup(m => m.GetOrdinal("Created")).Returns(ordinalCreated);
                dataRecordMock.Setup(m => m.GetDateTime(ordinalCreated)).Returns(createdDate);

                dataRecordMock.SetupGet(m => m["LastEdited"]).Returns(lastEditedDate);
                dataRecordMock.Setup(m => m.GetOrdinal("LastEdited")).Returns(ordinalLastEdited);
                dataRecordMock.Setup(m => m.GetDateTime(ordinalLastEdited)).Returns(lastEditedDate);

                dataRecordMock.SetupGet(m => m["Published"]).Returns(publishedDate);
                dataRecordMock.Setup(m => m.GetOrdinal("Published")).Returns(ordinalPublished);
                dataRecordMock.Setup(m => m.GetDateTime(ordinalPublished)).Returns(publishedDate);

                return dataRecordMock;
            }
        }
    }
}

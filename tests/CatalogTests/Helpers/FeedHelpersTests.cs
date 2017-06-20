// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;

namespace CatalogTests.Helpers
{
    public class FeedHelpersTests
    {
        private const string _baseUri = "http://unit.test";

        [Fact]
        public async Task GetCatalogPropertiesAsync_ThrowsForNullStorage()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => FeedHelpers.GetCatalogPropertiesAsync(storage: null, cancellationToken: CancellationToken.None));

            Assert.Equal("storage", exception.ParamName);
        }

        [Fact]
        public async Task GetCatalogPropertiesAsync_ThrowsIfCancelled()
        {
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => FeedHelpers.GetCatalogPropertiesAsync(
                    Mock.Of<IStorage>(),
                    new CancellationToken(canceled: true)));
        }

        [Fact]
        public async Task GetCatalogPropertiesAsync_ReturnsNullPropertiesIfStorageReturnsNull()
        {
            var storage = CreateStorageMock(json: null);

            var catalogProperties = await FeedHelpers.GetCatalogPropertiesAsync(
                storage.Object,
                CancellationToken.None);

            Assert.NotNull(catalogProperties);
            Assert.Null(catalogProperties.LastCreated);
            Assert.Null(catalogProperties.LastDeleted);
            Assert.Null(catalogProperties.LastEdited);

            storage.Verify();
        }

        [Fact]
        public async Task GetCatalogPropertiesAsync_ReturnsNullPropertiesIfPropertiesMissing()
        {
            var storage = CreateStorageMock(json: "{}");

            var catalogProperties = await FeedHelpers.GetCatalogPropertiesAsync(
                storage.Object,
                CancellationToken.None);

            Assert.NotNull(catalogProperties);
            Assert.Null(catalogProperties.LastCreated);
            Assert.Null(catalogProperties.LastDeleted);
            Assert.Null(catalogProperties.LastEdited);

            storage.Verify();
        }

        [Fact]
        public async Task GetCatalogPropertiesAsync_ReturnsAllPropertiesIfAllPropertiesSet()
        {
            var datetimeOffset = CreateDateTimeOffset(TimeSpan.Zero);
            var json = $"{{\"nuget:lastCreated\":\"{(datetimeOffset.ToString("O"))}\"," +
                $"\"nuget:lastDeleted\":\"{(datetimeOffset.ToString("O"))}\"," +
                $"\"nuget:lastEdited\":\"{(datetimeOffset.ToString("O"))}\"}}";
            var storage = CreateStorageMock(json);

            var catalogProperties = await FeedHelpers.GetCatalogPropertiesAsync(
                storage.Object,
                CancellationToken.None);

            Assert.NotNull(catalogProperties);
            Assert.NotNull(catalogProperties.LastCreated);
            Assert.NotNull(catalogProperties.LastDeleted);
            Assert.NotNull(catalogProperties.LastEdited);
            Assert.Equal(datetimeOffset, catalogProperties.LastCreated.Value);
            Assert.Equal(datetimeOffset, catalogProperties.LastDeleted.Value);
            Assert.Equal(datetimeOffset, catalogProperties.LastEdited.Value);

            storage.Verify();
        }

        [Fact]
        public async Task GetCatalogPropertiesAsync_ReturnsDateTimeWithFractionalHourOffsetInUtc()
        {
            var lastCreated = CreateDateTimeOffset(new TimeSpan(hours: 5, minutes: 30, seconds: 0));
            var json = CreateCatalogJson("nuget:lastCreated", lastCreated);

            await VerifyPropertyAsync(json, catalogProperties =>
                {
                    Assert.NotNull(catalogProperties.LastCreated);
                    Assert.Equal(lastCreated, catalogProperties.LastCreated.Value);
                    Assert.Equal(DateTimeKind.Utc, catalogProperties.LastCreated.Value.Kind);
                });
        }

        [Fact]
        public async Task GetCatalogPropertiesAsync_ReturnsDateTimeWithPositiveOffsetInUtc()
        {
            var lastDeleted = CreateDateTimeOffset(TimeSpan.FromHours(1));
            var json = CreateCatalogJson("nuget:lastDeleted", lastDeleted);

            await VerifyPropertyAsync(json, catalogProperties =>
                {
                    Assert.NotNull(catalogProperties.LastDeleted);
                    Assert.Equal(lastDeleted, catalogProperties.LastDeleted.Value);
                    Assert.Equal(DateTimeKind.Utc, catalogProperties.LastDeleted.Value.Kind);
                });
        }

        [Fact]
        public async Task GetCatalogPropertiesAsync_ReturnsDateTimeWithNegativeOffsetInUtc()
        {
            var lastEdited = CreateDateTimeOffset(TimeSpan.FromHours(-1));
            var json = CreateCatalogJson("nuget:lastEdited", lastEdited);

            await VerifyPropertyAsync(json, catalogProperties =>
                {
                    Assert.NotNull(catalogProperties.LastEdited);
                    Assert.Equal(lastEdited, catalogProperties.LastEdited.Value);
                    Assert.Equal(DateTimeKind.Utc, catalogProperties.LastEdited.Value.Kind);
                });
        }

        [Fact]
        public async Task GetCatalogPropertiesAsync_ReturnUtcDateTimeInUtc()
        {
            var lastCreated = DateTimeOffset.UtcNow;
            var json = CreateCatalogJson("nuget:lastCreated", lastCreated);

            await VerifyPropertyAsync(json, catalogProperties =>
                {
                    Assert.NotNull(catalogProperties.LastCreated);
                    Assert.Equal(lastCreated, catalogProperties.LastCreated.Value);
                    Assert.Equal(DateTimeKind.Utc, catalogProperties.LastCreated.Value.Kind);
                });
        }

        public static IEnumerable<object[]> GetPackages_GetsAllPackages_data
        {
            get
            {
                yield return new object[]
                {
                    ODataPackages
                };
            }
        }

        [Theory]
        [MemberData(nameof(GetPackages_GetsAllPackages_data))]
        public async Task GetPackages_GetsAllPackages(IEnumerable<ODataPackage> oDataPackages)
        {
            // Act
            var feedPackages = await TestGetPackagesAsync(oDataPackages);

            // Assert
            foreach (var oDataPackage in oDataPackages)
            {
                Assert.Contains(feedPackages,
                    (feedPackage) =>
                    {
                        return ArePackagesEqual(feedPackage, oDataPackage);
                    });
            }
        }

        public static IEnumerable<object[]> GetPackagesInOrder_GetsAllPackagesInOrder_data
        {
            get
            {
                var oDataPackages = ODataPackages;

                var keyDateFuncs = new Func<FeedPackageDetails, DateTime>[]
                {
                    package => package.CreatedDate,
                    package => package.LastEditedDate,
                    package => package.PublishedDate,
                };

                return keyDateFuncs.Select(p => new object[] { oDataPackages, p });
            }
        }

        [Theory]
        [MemberData(nameof(GetPackagesInOrder_GetsAllPackagesInOrder_data))]
        public async Task GetPackagesInOrder_GetsAllPackagesInOrder(IEnumerable<ODataPackage> oDataPackages, Func<FeedPackageDetails, DateTime> keyDateFunc)
        {
            //// Act
            var feedPackagesInOrder = await TestGetPackagesInOrder(oDataPackages, keyDateFunc);

            //// Assert
            // All OData packages must exist in the result.
            foreach (var oDataPackage in oDataPackages)
            {
                Assert.Contains(feedPackagesInOrder.SelectMany(p => p.Value),
                    (feedPackage) =>
                    {
                        return ArePackagesEqual(feedPackage, oDataPackage);
                    });
            }

            // The packages must be in order and grouped by timestamp.
            var currentTimestamp = DateTime.MinValue;
            foreach (var feedPackages in feedPackagesInOrder)
            {
                Assert.True(currentTimestamp < feedPackages.Key);

                currentTimestamp = feedPackages.Key;
                foreach (var feedPackage in feedPackages.Value)
                {
                    var packageKeyDate = keyDateFunc(feedPackage);
                    Assert.Equal(packageKeyDate.Ticks, currentTimestamp.Ticks);
                }
            }
        }

        private Task<IList<FeedPackageDetails>> TestGetPackagesAsync(IEnumerable<ODataPackage> oDataPackages)
        {
            return FeedHelpers.GetPackages(
                new HttpClient(GetMessageHandlerForPackages(oDataPackages)),
                new Uri(_baseUri + "/test"));
        }

        private Task<SortedList<DateTime, IList<FeedPackageDetails>>> TestGetPackagesInOrder(
            IEnumerable<ODataPackage> oDataPackages,
            Func<FeedPackageDetails, DateTime> keyDateFunc)
        {
            return FeedHelpers.GetPackagesInOrder(
                new HttpClient(GetMessageHandlerForPackages(oDataPackages)),
                new Uri(_baseUri + "/test"),
                keyDateFunc);
        }

        private HttpMessageHandler GetMessageHandlerForPackages(IEnumerable<ODataPackage> oDataPackages)
        {
            var mockServer = new MockServerHttpClientHandler();

            mockServer.SetAction("/test", (request) =>
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            ODataFeedHelper.ToODataFeed(oDataPackages, new Uri(_baseUri), "Packages"))
                    });
                });

            return mockServer;
        }

        private bool ArePackagesEqual(FeedPackageDetails feedPackage, ODataPackage oDataPackage)
        {
            return
                feedPackage.ContentUri.ToString() == $"{_baseUri}/package/{oDataPackage.Id}/{NuGetVersion.Parse(oDataPackage.Version).ToNormalizedString()}" &&
                feedPackage.CreatedDate.Ticks == oDataPackage.Created.Ticks &&
                feedPackage.LastEditedDate.Ticks == oDataPackage.LastEdited.Value.Ticks &&
                feedPackage.PublishedDate.Ticks == oDataPackage.Published.Ticks &&
                feedPackage.LicenseNames == oDataPackage.LicenseNames &&
                feedPackage.LicenseReportUrl == oDataPackage.LicenseReportUrl;
        }

        private static IEnumerable<ODataPackage> ODataPackages
        {
            get
            {
                return new List<ODataPackage>
                {
                    new ODataPackage
                    {
                        Id = "listedPackage",
                        Version = "1.0.0",
                        Listed = true,

                        Created = new DateTime(2017, 4, 6, 15, 10, 0),
                        LastEdited = new DateTime(2017, 4, 6, 15, 10, 1),
                        Published = new DateTime(2017, 4, 6, 15, 10, 0),

                        LicenseNames = "ABCD",
                        LicenseReportUrl = "https://unit.test/license"
                    },

                    new ODataPackage
                    {
                        Id = "unlistedPackage",
                        Version = "2.0.1",
                        Listed = false,

                        Created = new DateTime(2017, 4, 6, 15, 12, 0),
                        LastEdited = new DateTime(2017, 4, 6, 15, 13, 1),
                        Published = Convert.ToDateTime("1900-01-01T00:00:00Z"),

                        LicenseNames = "ABCD",
                        LicenseReportUrl = "https://unit.test/license"
                    },

                    new ODataPackage
                    {
                        Id = "listedPackage",
                        Version = "2.1.1",
                        Listed = true,

                        Created = new DateTime(2017, 4, 6, 15, 13, 3),
                        LastEdited = new DateTime(2017, 4, 6, 15, 14, 1),
                        Published = new DateTime(2017, 4, 6, 15, 13, 0),

                        LicenseNames = "ABCD",
                        LicenseReportUrl = "https://unit.test/license"
                    },

                    new ODataPackage
                    {
                        Id = "unlistedPackage",
                        Version = "3.0.4",
                        Listed = false,

                        Created = new DateTime(2017, 4, 6, 15, 15, 3),
                        LastEdited = new DateTime(2017, 4, 6, 15, 16, 4),
                        Published = Convert.ToDateTime("1900-01-01T00:00:00Z"),

                        LicenseNames = "ABCD",
                        LicenseReportUrl = "https://unit.test/license"
                    },

                    new ODataPackage
                    {
                        Id = "packageWithPrerelease",
                        Version = "2.2.2-abcdef",
                        Listed = true,

                        Created = new DateTime(2017, 4, 6, 16, 30, 0),
                        LastEdited = new DateTime(2017, 4, 6, 17, 45, 1),
                        Published = new DateTime(2017, 4, 6, 16, 30, 0),

                        LicenseNames = "ABCD",
                        LicenseReportUrl = "https://unit.test/license"
                    },

                    new ODataPackage
                    {
                        Id = "packageWithNormalized",
                        Version = "4.4.4.0",
                        Listed = true,

                        Created = new DateTime(2017, 4, 6, 20, 13, 25),
                        LastEdited = new DateTime(2017, 4, 6, 20, 37, 47),
                        Published = new DateTime(2017, 4, 6, 20, 13, 25),

                        LicenseNames = "ABCD",
                        LicenseReportUrl = "https://unit.test/license"
                    },

                    new ODataPackage
                    {
                        Id = "packageWithNormalizedPrerelease",
                        Version = "5.6.3.0-laskdfj224",
                        Listed = true,

                        Created = new DateTime(2017, 4, 7, 3, 13, 25),
                        LastEdited = new DateTime(2017, 4, 7, 4, 52, 55),
                        Published = new DateTime(2017, 4, 6, 3, 13, 25),

                        LicenseNames = "ABCD",
                        LicenseReportUrl = "https://unit.test/license"
                    },

                    new ODataPackage
                    {
                        Id = "listedPackageWithDuplicate",
                        Version = "1.0.1",
                        Listed = true,

                        Created = new DateTime(2017, 5, 1, 0, 0, 0),
                        LastEdited = new DateTime(2017, 5, 1, 0, 0, 0),
                        Published = new DateTime(2017, 5, 1, 0, 0, 0),

                        LicenseNames = "ABCD",
                        LicenseReportUrl = "https://unit.test/license"
                    },

                    new ODataPackage
                    {
                        Id = "listedPackageWithDuplicate",
                        Version = "1.0.2",
                        Listed = true,

                        Created = new DateTime(2017, 5, 1, 0, 0, 0),
                        LastEdited = new DateTime(2017, 5, 1, 0, 0, 0),
                        Published = new DateTime(2017, 5, 1, 0, 0, 0),

                        LicenseNames = "ABCD",
                        LicenseReportUrl = "https://unit.test/license"
                    }
                };
            }
        }

        private static string CreateCatalogJson(string propertyName, DateTimeOffset propertyValue)
        {
            return $"{{\"{propertyName}\":\"{(propertyValue.ToString("O"))}\"}}";
        }

        private static DateTimeOffset CreateDateTimeOffset(TimeSpan offset)
        {
            var datetime = new DateTime(DateTime.Now.Ticks, DateTimeKind.Unspecified);

            return new DateTimeOffset(datetime, offset);
        }

        private static Mock<IStorage> CreateStorageMock(string json)
        {
            var storage = new Mock<IStorage>(MockBehavior.Strict);

            storage.Setup(x => x.ResolveUri(It.IsNotNull<string>()))
                .Returns(new Uri("https://unit.test"))
                .Verifiable();
            storage.Setup(x => x.LoadString(It.IsNotNull<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(json)
                .Verifiable();

            return storage;
        }

        private static async Task VerifyPropertyAsync(string json, Action<CatalogProperties> propertyVerifier)
        {
            var storage = CreateStorageMock(json);

            var catalogProperties = await FeedHelpers.GetCatalogPropertiesAsync(
                storage.Object,
                CancellationToken.None);

            Assert.NotNull(catalogProperties);
            propertyVerifier(catalogProperties);
            storage.Verify();
        }
    }
}
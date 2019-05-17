// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;

namespace CatalogTests.Helpers
{
    public class FeedHelpersTests
    {
        private const string _baseUri = "http://unit.test";

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

            foreach (var feedPackage in feedPackages)
            {
                VerifyDateTimesAreInUtc(feedPackage);
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

                    VerifyDateTimesAreInUtc(feedPackage);
                }
            }
        }

        [Fact]
        public async Task DownloadMetadata2CatalogAsync_WhenPackageCatalogItemCreatorIsNull_Throws()
        {
            IPackageCatalogItemCreator creator = null;

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => FeedHelpers.DownloadMetadata2CatalogAsync(
                    creator,
                    new SortedList<DateTime, IList<FeedPackageDetails>>(),
                    Mock.Of<IStorage>(),
                    DateTime.MinValue,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    maxDegreeOfParallelism: 1,
                    createdPackages: null,
                    updateCreatedFromEdited: false,
                    cancellationToken: CancellationToken.None,
                    telemetryService: Mock.Of<ITelemetryService>(),
                    logger: Mock.Of<ILogger>()));

            Assert.Equal("packageCatalogItemCreator", exception.ParamName);
        }

        [Fact]
        public async Task DownloadMetadata2CatalogAsync_WhenPackagesIsNull_Throws()
        {
            SortedList<DateTime, IList<FeedPackageDetails>> packages = null;

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => FeedHelpers.DownloadMetadata2CatalogAsync(
                    Mock.Of<IPackageCatalogItemCreator>(),
                    packages,
                    Mock.Of<IStorage>(),
                    DateTime.MinValue,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    maxDegreeOfParallelism: 1,
                    createdPackages: null,
                    updateCreatedFromEdited: false,
                    cancellationToken: CancellationToken.None,
                    telemetryService: Mock.Of<ITelemetryService>(),
                    logger: Mock.Of<ILogger>()));

            Assert.Equal("packages", exception.ParamName);
        }

        [Fact]
        public async Task DownloadMetadata2CatalogAsync_WhenStorageIsNull_Throws()
        {
            IStorage storage = null;

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => FeedHelpers.DownloadMetadata2CatalogAsync(
                    Mock.Of<IPackageCatalogItemCreator>(),
                    new SortedList<DateTime, IList<FeedPackageDetails>>(),
                    storage,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    maxDegreeOfParallelism: 1,
                    createdPackages: null,
                    updateCreatedFromEdited: false,
                    cancellationToken: CancellationToken.None,
                    telemetryService: Mock.Of<ITelemetryService>(),
                    logger: Mock.Of<ILogger>()));

            Assert.Equal("storage", exception.ParamName);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public async Task DownloadMetadata2CatalogAsync_WhenMaxDegreeOfParallelismIsOutOfRange_Throws(int maxDegreeOfParallelism)
        {
            var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => FeedHelpers.DownloadMetadata2CatalogAsync(
                    Mock.Of<IPackageCatalogItemCreator>(),
                    new SortedList<DateTime, IList<FeedPackageDetails>>(),
                    Mock.Of<IStorage>(),
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    maxDegreeOfParallelism,
                    createdPackages: false,
                    updateCreatedFromEdited: false,
                    cancellationToken: CancellationToken.None,
                    telemetryService: Mock.Of<ITelemetryService>(),
                    logger: Mock.Of<ILogger>()));

            Assert.Equal("maxDegreeOfParallelism", exception.ParamName);
            Assert.StartsWith($"The argument must be within the range from 1 (inclusive) to {int.MaxValue} (inclusive).", exception.Message);
        }

        [Fact]
        public async Task DownloadMetadata2CatalogAsync_WhenTelemetryServiceIsNull_Throws()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => FeedHelpers.DownloadMetadata2CatalogAsync(
                    Mock.Of<IPackageCatalogItemCreator>(),
                    new SortedList<DateTime, IList<FeedPackageDetails>>(),
                    Mock.Of<IStorage>(),
                    DateTime.MinValue,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    maxDegreeOfParallelism: 1,
                    createdPackages: null,
                    updateCreatedFromEdited: false,
                    cancellationToken: CancellationToken.None,
                    telemetryService: null,
                    logger: Mock.Of<ILogger>()));

            Assert.Equal("telemetryService", exception.ParamName);
        }

        [Fact]
        public async Task DownloadMetadata2CatalogAsync_WhenLoggerIsNull_Throws()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => FeedHelpers.DownloadMetadata2CatalogAsync(
                    Mock.Of<IPackageCatalogItemCreator>(),
                    new SortedList<DateTime, IList<FeedPackageDetails>>(),
                    Mock.Of<IStorage>(),
                    DateTime.MinValue,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    maxDegreeOfParallelism: 1,
                    createdPackages: null,
                    updateCreatedFromEdited: false,
                    cancellationToken: CancellationToken.None,
                    telemetryService: Mock.Of<ITelemetryService>(),
                    logger: null));

            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public async Task DownloadMetadata2CatalogAsync_WhenCancellationTokenIsCancelled_Throws()
        {
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => FeedHelpers.DownloadMetadata2CatalogAsync(
                    Mock.Of<IPackageCatalogItemCreator>(),
                    new SortedList<DateTime, IList<FeedPackageDetails>>(),
                    Mock.Of<IStorage>(),
                    DateTime.MinValue,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    maxDegreeOfParallelism: 1,
                    createdPackages: null,
                    updateCreatedFromEdited: false,
                    cancellationToken: new CancellationToken(canceled: true),
                    telemetryService: Mock.Of<ITelemetryService>(),
                    logger: Mock.Of<ILogger>()));
        }

        [Fact]
        public async Task DownloadMetadata2CatalogAsync_WhenCreatedPackagesIsNull_WithNoPackages_ReturnsDateTimeMinValue()
        {
            using (var test = new DownloadMetadata2CatalogAsyncTest())
            {
                test.CreatedPackages = null;

                var result = await test.DownloadMetadata2CatalogAsync();

                Assert.Equal(DateTime.MinValue, result);
            }
        }

        [Fact]
        public async Task DownloadMetadata2CatalogAsync_WhenCreatedPackagesIsFalse_WithNoPackages_ReturnsLastEdited()
        {
            using (var test = new DownloadMetadata2CatalogAsyncTest())
            {
                test.CreatedPackages = false;

                var result = await test.DownloadMetadata2CatalogAsync();

                Assert.Equal(test.LastEdited, result);
            }
        }

        [Fact]
        public async Task DownloadMetadata2CatalogAsync_WhenCreatedPackagesIsTrue_WithNoPackages_ReturnsLastCreated()
        {
            using (var test = new DownloadMetadata2CatalogAsyncTest())
            {
                test.CreatedPackages = true;

                var result = await test.DownloadMetadata2CatalogAsync();

                Assert.Equal(test.LastCreated, result);
            }
        }

        public enum PackageDeprecationItemState
        {
            NotDeprecated,
            DeprecatedWithSingleReason,
            DeprecatedWithMessage,
            DeprecatedWithAlternate
        }

        public static IEnumerable<object[]> DownloadMetadata2CatalogAsync_WithOnePackage_UpdatesStorage_Data
        {
            get
            {
                foreach (var createdPackages in new[] { false, true })
                {
                    foreach (var updateCreatedFromEdited in new[] { false, true })
                    {
                        foreach (var deprecationState in Enum.GetValues(typeof(PackageDeprecationItemState)).Cast<PackageDeprecationItemState>())
                        {
                            yield return new object[] { createdPackages, updateCreatedFromEdited, deprecationState };
                        }
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(DownloadMetadata2CatalogAsync_WithOnePackage_UpdatesStorage_Data))]
        public async Task DownloadMetadata2CatalogAsync_WithOnePackage_UpdatesStorage(bool createdPackages, bool updateCreatedFromEdited, PackageDeprecationItemState deprecationState)
        {
            // Arrange
            using (var test = new DownloadMetadata2CatalogAsyncTest())
            {
                test.CreatedPackages = createdPackages;
                test.UpdateCreatedFromEdited = updateCreatedFromEdited;
                test.Packages.Add(test.UtcNow, new List<FeedPackageDetails>()
                {
                    test.FeedPackageDetails
                });

                NupkgMetadata nupkgMetadata = GetNupkgMetadata("Newtonsoft.Json.9.0.2-beta1.nupkg");

                var deprecationItem = GetPackageDeprecationItemFromState(deprecationState);
                var packageCatalogItem = new PackageCatalogItem(
                    nupkgMetadata,
                    test.FeedPackageDetails.CreatedDate,
                    test.FeedPackageDetails.LastEditedDate,
                    test.FeedPackageDetails.PublishedDate,
                    deprecation: deprecationItem);

                test.PackageCatalogItemCreator.Setup(x => x.CreateAsync(
                        It.Is<FeedPackageDetails>(details => details == test.FeedPackageDetails),
                        It.Is<DateTime>(timestamp => timestamp == test.UtcNow),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(packageCatalogItem);

                test.Storage.SetupGet(x => x.BaseAddress)
                    .Returns(test.CatalogBaseUri);

                var blobs = new List<CatalogBlob>();

                test.Storage.Setup(x => x.SaveAsync(
                        It.IsNotNull<Uri>(),
                        It.IsNotNull<StorageContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, StorageContent, CancellationToken>((uri, content, token) =>
                    {
                        blobs.Add(new CatalogBlob(uri, content));
                    })
                    .Returns(Task.FromResult(0));

                test.Storage.Setup(x => x.LoadStringAsync(
                        It.Is<Uri>(uri => uri == test.CatalogIndexUri),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(CatalogTestData.GetBeforeIndex(test.CatalogIndexUri).ToString());

                test.TelemetryService.Setup(x => x.TrackCatalogIndexWriteDuration(
                    It.Is<TimeSpan>(duration => duration > TimeSpan.Zero),
                    It.Is<Uri>(uri => uri == test.CatalogIndexUri)));

                // Act
                var result = await test.DownloadMetadata2CatalogAsync();

                // Assert
                Assert.Equal(test.UtcNow, result);

                Assert.Equal(3, blobs.Count);

                var catalogPackageDetailsUri = new Uri($"{test.CatalogBaseUri}data/{packageCatalogItem.TimeStamp.ToString("yyyy.MM.dd.HH.mm.ss")}/newtonsoft.json.9.0.2-beta1.json");
                var catalogPageUri = new Uri($"{test.CatalogBaseUri}page0.json");

                // Verify package details blob
                Assert.Equal(catalogPackageDetailsUri, blobs[0].Uri);
                Assert.IsType<StringStorageContent>(blobs[0].Content);

                var stringContent = (StringStorageContent)blobs[0].Content;

                Assert.Equal("no-store", stringContent.CacheControl);
                Assert.Equal("application/json", stringContent.ContentType);

                var expectedContent = CatalogTestData.GetPackageDetails(
                    catalogPackageDetailsUri,
                    packageCatalogItem.CommitId,
                    packageCatalogItem.TimeStamp,
                    packageCatalogItem.CreatedDate.Value,
                    packageCatalogItem.LastEditedDate.Value,
                    packageCatalogItem.PublishedDate.Value,
                    deprecationItem);

                var actualContent = JObject.Parse(stringContent.Content);

                Assert.Equal(expectedContent.ToString(), actualContent.ToString());

                // Verify page blob
                Assert.Equal(catalogPageUri, blobs[1].Uri);
                Assert.IsType<JTokenStorageContent>(blobs[1].Content);

                var jtokenContent = (JTokenStorageContent)blobs[1].Content;

                expectedContent = CatalogTestData.GetPage(
                    catalogPageUri,
                    packageCatalogItem.CommitId,
                    packageCatalogItem.TimeStamp,
                    test.CatalogIndexUri,
                    catalogPackageDetailsUri);

                Assert.Equal("no-store", jtokenContent.CacheControl);
                Assert.Equal("application/json", jtokenContent.ContentType);
                Assert.Equal(expectedContent.ToString(), jtokenContent.Content.ToString());

                // Verify index blob
                Assert.Equal(test.CatalogIndexUri, blobs[2].Uri);
                Assert.IsType<JTokenStorageContent>(blobs[2].Content);

                jtokenContent = (JTokenStorageContent)blobs[2].Content;

                var lastEdited = createdPackages ? test.LastEdited : test.UtcNow;
                var lastCreated = updateCreatedFromEdited ? lastEdited : (createdPackages ? test.UtcNow : test.LastCreated);

                expectedContent = CatalogTestData.GetAfterIndex(
                    test.CatalogIndexUri,
                    packageCatalogItem.CommitId,
                    packageCatalogItem.TimeStamp,
                    lastCreated,
                    test.LastDeleted,
                    lastEdited,
                    catalogPageUri);

                Assert.Equal("no-store", jtokenContent.CacheControl);
                Assert.Equal("application/json", jtokenContent.ContentType);
                Assert.Equal(expectedContent.ToString(), jtokenContent.Content.ToString());
            }
        }

        private static PackageDeprecationItem GetPackageDeprecationItemFromState(PackageDeprecationItemState deprecationState)
        {
            if (deprecationState == PackageDeprecationItemState.NotDeprecated)
            {
                return null;
            }

            var reasons = new[] { "first", "second" };
            string message = null;
            string altId = null;
            string altVersion = null;
            if (deprecationState == PackageDeprecationItemState.DeprecatedWithSingleReason)
            {
                reasons = reasons.Take(1).ToArray();
            }

            if (deprecationState == PackageDeprecationItemState.DeprecatedWithMessage)
            {
                message = "this is the message";
            }

            if (deprecationState == PackageDeprecationItemState.DeprecatedWithAlternate)
            {
                altId = "theId";
                altVersion = "[2.4.5, 2.4.5]";
            }

            return new PackageDeprecationItem(reasons, message, altId, altVersion);
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
                feedPackage.PackageId == oDataPackage.Id &&
                feedPackage.PackageVersion == oDataPackage.Version &&
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

        private static NupkgMetadata GetNupkgMetadata(string resourceName)
        {
            using (var stream = TestHelper.GetStream(resourceName))
            {
                return Utils.GetNupkgMetadata(stream, packageHash: null);
            }
        }

        private static void VerifyDateTimesAreInUtc(FeedPackageDetails feedPackage)
        {
            Assert.Equal(DateTimeKind.Utc, feedPackage.CreatedDate.Kind);
            Assert.Equal(DateTimeKind.Utc, feedPackage.LastEditedDate.Kind);
            Assert.Equal(DateTimeKind.Utc, feedPackage.PublishedDate.Kind);
        }

        private sealed class DownloadMetadata2CatalogAsyncTest : IDisposable
        {
            private bool _isDisposed;

            internal DateTime UtcNow { get; }
            internal DateTime LastCreated { get; }
            internal DateTime LastEdited { get; }
            internal DateTime LastDeleted { get; }
            internal bool? CreatedPackages { get; set; }
            internal bool UpdateCreatedFromEdited { get; set; }
            internal Mock<IPackageCatalogItemCreator> PackageCatalogItemCreator { get; }
            internal SortedList<DateTime, IList<FeedPackageDetails>> Packages { get; }
            internal Mock<IStorage> Storage { get; }
            internal Mock<ITelemetryService> TelemetryService { get; }
            internal Mock<ILogger> Logger { get; }
            internal FeedPackageDetails FeedPackageDetails { get; }
            internal Uri CatalogBaseUri { get; }
            internal Uri CatalogIndexUri { get; }

            internal DownloadMetadata2CatalogAsyncTest()
            {
                UtcNow = DateTime.UtcNow;
                LastCreated = UtcNow.AddHours(-3);
                LastEdited = UtcNow.AddHours(-2);
                LastDeleted = UtcNow.AddHours(-1);

                Packages = new SortedList<DateTime, IList<FeedPackageDetails>>();

                PackageCatalogItemCreator = new Mock<IPackageCatalogItemCreator>(MockBehavior.Strict);
                Storage = new Mock<IStorage>(MockBehavior.Strict);
                TelemetryService = new Mock<ITelemetryService>(MockBehavior.Strict);
                Logger = new Mock<ILogger>();

                CatalogBaseUri = new Uri("https://nuget.test/v3-catalog0/");
                CatalogIndexUri = new Uri(CatalogBaseUri, "index.json");

                Storage.Setup(x => x.ResolveUri(
                        It.Is<string>(relativeUri => relativeUri == "index.json")))
                    .Returns(CatalogIndexUri);

                FeedPackageDetails = new FeedPackageDetails(
                    new Uri("https://nuget.test/packages/a"),
                    UtcNow.AddMinutes(-45),
                    UtcNow.AddMinutes(-30),
                    UtcNow.AddMinutes(-15),
                    packageId: "a",
                    packageVersion: "1.0.0");
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    PackageCatalogItemCreator.VerifyAll();
                    Storage.VerifyAll();
                    TelemetryService.VerifyAll();
                    Logger.VerifyAll();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }

            internal Task<DateTime> DownloadMetadata2CatalogAsync()
            {
                const int maxDegreeOfParallelism = 1;

                return FeedHelpers.DownloadMetadata2CatalogAsync(
                  PackageCatalogItemCreator.Object,
                  Packages,
                  Storage.Object,
                  LastCreated,
                  LastEdited,
                  LastDeleted,
                  maxDegreeOfParallelism,
                  CreatedPackages,
                  UpdateCreatedFromEdited,
                  CancellationToken.None,
                  TelemetryService.Object,
                  Logger.Object);
            }
        }

        private sealed class CatalogBlob
        {
            internal Uri Uri { get; }
            internal StorageContent Content { get; }

            internal CatalogBlob(Uri uri, StorageContent content)
            {
                Uri = uri;
                Content = content;
            }
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;

namespace NgTests
{
    public class Feed2CatalogTests
    {
        private const string _auditRecordDateTimeFormat = "yyyy-MM-ddTHH:mm:ss.FFFZ";
        private const string _catalogCommitTimestampDateTimeFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ";
        private const string _catalogDateTimeFormat = "yyyy-MM-ddTHH:mm:ss.FFFZ";
        private const string _catalogUrlDateTimeFormat = "yyyy.MM.dd.HH.mm.ss";
        private const string _feedBaseUri = "http://unit.test";
        private const string _feedUrlSuffix = "&$top=20&$select=Id,NormalizedVersion,Created,LastEdited,Published,LicenseNames,LicenseReportUrl&semVerLevel=2.0.0";
        private const string _feedUrlDateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fff0000Z";

        private const int _top = 1;

        private bool _isDisposed;
        private DateTime _catalogLastDeleted;
        private DateTime _catalogLastCreated;
        private DateTime _catalogLastEdited;
        private DateTime _feedLastCreated;
        private DateTime _feedLastEdited;
        private DateTimeOffset _timestamp;
        private bool _hasFirstRunOnceAsyncBeenCalledBefore;
        private int _lastFeedEntriesCount;
        private int _catalogBatchesProcessed;
        private readonly List<PackageOperation> _packageOperations;
        private readonly Random _random;
        private readonly MemoryStorage _auditingStorage;
        private readonly MemoryStorage _catalogStorage;
        private TestableFeed2CatalogJob _job;
        private readonly Uri _baseUri;
        private bool _skipCreatedPackagesProcessing;
        private readonly MockServerHttpClientHandler _server;

        public Feed2CatalogTests()
        {
            _catalogLastDeleted = Constants.DateTimeMinValueUtc;
            _catalogLastCreated = Constants.DateTimeMinValueUtc;
            _catalogLastEdited = Constants.DateTimeMinValueUtc;

            _server = new MockServerHttpClientHandler();
            _random = new Random();
            _packageOperations = new List<PackageOperation>();
            _baseUri = new Uri(_feedBaseUri);

            _catalogStorage = new MemoryStorage(_baseUri);
            _auditingStorage = new MemoryStorage(_baseUri);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                foreach (var operation in _packageOperations.OfType<PackageCreationOrEdit>())
                {
                    operation.Package.Dispose();
                }

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RunInternal_WithNoCatalogAndNoActivity_DoesNotCreateCatalog(bool skipCreatedPackagesProcessing)
        {
            InitializeTest(skipCreatedPackagesProcessing);

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunInternal_WithNoCatalogAndCreatedPackageInFeed_CreatesCatalog()
        {
            InitializeTest();

            AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunInternal_WithNoCatalogAndCreatedPackageInFeedAndWithCreatedPackagesSkipped_DoesNotUpdateCatalog()
        {
            InitializeTest(skipCreatedPackagesProcessing: true);

            AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RunInternal_WithCatalogAndNoActivity_DoesNotUpdateCatalog(bool skipCreatedPackagesProcessing)
        {
            InitializeTest(skipCreatedPackagesProcessing);

            AddCreatedPackageToFeed();

            // Create the catalog.
            await RunInternalAndVerifyAsync(CancellationToken.None);

            // Nothing new in the feed this time.
            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Fact]
        // This test verifies current, flawed behavior.
        // https://github.com/NuGet/NuGetGallery/issues/2841
        public async Task RunInternal_WithPackagesWithSameCreatedTimeInFeedAndWhenProcessedInDifferentCatalogBatches_SkipsSecondEntry()
        {
            InitializeTest();

            var package1 = AddCreatedPackageToFeed();
            var package2 = AddCreatedPackageToFeed();

            package2.ODataPackage.Created = package1.ODataPackage.Created;

            // Remove the "package2" argument if/when the bug is fixed.
            await RunInternalAndVerifyAsync(CancellationToken.None, package2);
        }

        [Fact]
        // This test verifies current, flawed behavior.
        // https://github.com/NuGet/NuGetGallery/issues/2841
        public async Task RunInternal_WithPackagesWithSameLastEditedTimeInFeedAndWhenProcessedInDifferentCatalogBatches_SkipsSecondEntry()
        {
            InitializeTest();

            var package1 = AddCreatedPackageToFeed();
            var package2 = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);

            package1 = AddEditedPackageToFeed(package1);
            package2 = AddEditedPackageToFeed(package2);

            package2.ODataPackage.LastEdited = package1.ODataPackage.LastEdited;

            // Remove the "package2" argument if/when the bug is fixed.
            await RunInternalAndVerifyAsync(CancellationToken.None, package2);
        }

        [Fact]
        public async Task RunInternal_WithCreatedPackagesInFeedAtDifferentTimes_UpdatesCatalog()
        {
            InitializeTest();

            AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);

            AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunInternal_WithCreatedPackagesInFeedAtDifferentTimesAndWithCreatedPackagesSkipped_DoesNotUpdateCatalog()
        {
            InitializeTest(skipCreatedPackagesProcessing: true);

            AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);

            AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RunInternal_WithCreatedPackageAndEditedPackageInFeedAtDifferentTimes_UpdatesCatalog(bool skipCreatedPackagesProcessing)
        {
            InitializeTest(skipCreatedPackagesProcessing);

            var package = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);

            AddEditedPackageToFeed(package);

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RunInternal_WithCreatedPackageAndEditedPackageInFeedAtSameTime_UpdatesCatalog(bool skipCreatedPackagesProcessing)
        {
            InitializeTest(skipCreatedPackagesProcessing);

            var package = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);

            AddEditedPackageToFeed(package);

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunInternal_WithEditedPackagesAndWithCreatedPackagesSkipped_UpdatesCatalog()
        {
            InitializeTest(skipCreatedPackagesProcessing: true);

            var package = CreatePackageCreationOrEdit();

            package = AddEditedPackageToFeed(package);

            await RunInternalAndVerifyAsync(CancellationToken.None);

            package = AddEditedPackageToFeed(package);

            await RunInternalAndVerifyAsync(CancellationToken.None);

            AddEditedPackageToFeed(package);

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunInternal_WithCreatedPackageThenDeletedPackage_UpdatesCatalog()
        {
            InitializeTest();

            var package = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);

            AddDeletedPackage(package);

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunInternal_WithMultipleDeletedPackagesWithDifferentPackageIdentities_ProcessesAllDeletions()
        {
            InitializeTest();

            var package1 = AddCreatedPackageToFeed();
            var package2 = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);

            AddDeletedPackage(package1);
            AddDeletedPackage(package2);

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunInternal_WithMultipleDeletedPackagesWithSamePackageIdentity_PutsEachPackageInSeparateCommit()
        {
            InitializeTest();

            var package = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);

            var deletionTime = DateTimeOffset.UtcNow;

            AddDeletedPackage(package, deletionTime.UtcDateTime, isSoftDelete: true);
            AddDeletedPackage(package, deletionTime.UtcDateTime, isSoftDelete: false);

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunInternal_WithMultipleCreatedPackages_ProcessesAllCreations()
        {
            InitializeTest();

            AddCreatedPackageToFeed();
            AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunInternal_WithMultipleEditedPackages_ProcessesAllEdits()
        {
            InitializeTest();

            var package1 = AddCreatedPackageToFeed();
            var package2 = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(CancellationToken.None);

            AddEditedPackageToFeed(package1);
            AddEditedPackageToFeed(package2);

            await RunInternalAndVerifyAsync(CancellationToken.None);
        }

        [Fact]
        public async Task CreatesNewCatalogFromCreatedAndEditedPackages()
        {
            // Arrange
            var catalogStorage = new MemoryStorage();
            var auditingStorage = new MemoryStorage();
            auditingStorage.Content.TryAdd(
                new Uri(auditingStorage.BaseAddress, "package/OtherPackage/1.0.0/2015-01-01T00:01:01-deleted.audit.v1.json"),
                new StringStorageContent(TestCatalogEntries.DeleteAuditRecordForOtherPackage100));

            var mockServer = new MockServerHttpClientHandler();

            mockServer.SetAction(" / ", GetRootActionAsync);
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$orderby=Created" + _feedUrlSuffix, GetCreatedPackages);
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'2015-01-01T00:00:00.0000000Z'&$orderby=Created" + _feedUrlSuffix, GetEmptyPackages);

            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$orderby=LastEdited" + _feedUrlSuffix, GetEditedPackages);
            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'2015-01-01T00:00:00.0000000Z'&$orderby=LastEdited" + _feedUrlSuffix, GetEmptyPackages);

            mockServer.SetAction("/package/ListedPackage/1.0.0", request => GetStreamContentActionAsync(request, "Packages\\ListedPackage.1.0.0.zip"));
            mockServer.SetAction("/package/ListedPackage/1.0.1", request => GetStreamContentActionAsync(request, "Packages\\ListedPackage.1.0.1.zip"));
            mockServer.SetAction("/package/UnlistedPackage/1.0.0", request => GetStreamContentActionAsync(request, "Packages\\UnlistedPackage.1.0.0.zip"));
            mockServer.SetAction("/package/TestPackage.SemVer2/1.0.0-alpha.1", request => GetStreamContentActionAsync(request, "Packages\\TestPackage.SemVer2.1.0.0-alpha.1.nupkg"));

            // Act
            var feed2catalogTestJob = new TestableFeed2CatalogJob(
                mockServer,
                _feedBaseUri,
                catalogStorage,
                auditingStorage,
                skipCreatedPackagesProcessing: false,
                startDate: null,
                timeout: TimeSpan.FromMinutes(5),
                top: 20,
                verbose: true);
            await feed2catalogTestJob.RunOnce(CancellationToken.None);

            // Assert
            Assert.Equal(6, catalogStorage.Content.Count);

            // Ensure catalog has index.json
            var catalogIndex = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("index.json"));
            Assert.NotNull(catalogIndex.Key);
            Assert.Contains("\"nuget:lastCreated\":\"2015-01-01T00:00:00Z\"", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastDeleted\":\"0001-01-01T00:00:00Z", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastEdited\":\"2015-01-01T00:00:00Z\"", catalogIndex.Value.GetContentString());

            // Ensure catalog has page0.json
            var pageZero = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("page0.json"));
            Assert.NotNull(pageZero.Key);
            Assert.Contains("\"parent\":\"http://tempuri.org/index.json\",", pageZero.Value.GetContentString());

            Assert.Contains("/listedpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"ListedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            Assert.Contains("/listedpackage.1.0.1.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"ListedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.1\"", pageZero.Value.GetContentString());

            Assert.Contains("/unlistedpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"UnlistedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            Assert.Contains("/testpackage.semver2.1.0.0-alpha.1.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"TestPackage.SemVer2\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0-alpha.1+githash\"", pageZero.Value.GetContentString());

            // Check individual package entries
            var package1 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage.1.0.0.json"));
            Assert.NotNull(package1.Key);
            Assert.Contains("\"PackageDetails\",", package1.Value.GetContentString());
            Assert.Contains("\"id\": \"ListedPackage\",", package1.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package1.Value.GetContentString());

            var package2 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage.1.0.1.json"));
            Assert.NotNull(package2.Key);
            Assert.Contains("\"PackageDetails\",", package2.Value.GetContentString());
            Assert.Contains("\"id\": \"ListedPackage\",", package2.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.1\",", package2.Value.GetContentString());

            var package3 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage.1.0.0.json"));
            Assert.NotNull(package3.Key);
            Assert.Contains("\"PackageDetails\",", package3.Value.GetContentString());
            Assert.Contains("\"id\": \"UnlistedPackage\",", package3.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package3.Value.GetContentString());

            var package4 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/testpackage.semver2.1.0.0-alpha.1.json"));
            Assert.NotNull(package4.Key);
            Assert.Contains("\"PackageDetails\",", package4.Value.GetContentString());
            Assert.Contains("\"id\": \"TestPackage.SemVer2\",", package4.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0-alpha.1+githash\",", package4.Value.GetContentString());

            // Ensure catalog does not have the deleted "OtherPackage" as a fresh catalog should not care about deletes
            var package5 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage.1.0.0.json"));
            Assert.Null(package5.Key);
        }

        [Fact]
        public async Task AppendsDeleteToExistingCatalog()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackages();
            var auditingStorage = new MemoryStorage();

            var firstAuditingRecord = new Uri(auditingStorage.BaseAddress, $"package/OtherPackage/1.0.0/{Guid.NewGuid()}-deleted.audit.v1.json");
            var secondAuditingRecord = new Uri(auditingStorage.BaseAddress, $"package/AnotherPackage/1.0.0/{Guid.NewGuid()}-deleted.audit.v1.json");

            auditingStorage.Content.TryAdd(firstAuditingRecord, new StringStorageContent(TestCatalogEntries.DeleteAuditRecordForOtherPackage100));
            auditingStorage.Content.TryAdd(secondAuditingRecord, new StringStorageContent(TestCatalogEntries.DeleteAuditRecordForOtherPackage100.Replace("OtherPackage", "AnotherPackage")));
            auditingStorage.ListMock.TryAdd(secondAuditingRecord, new StorageListItem(secondAuditingRecord, new DateTime(2010, 1, 1)));

            var mockServer = new MockServerHttpClientHandler();

            mockServer.SetAction(" / ", GetRootActionAsync);
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$orderby=Created" + _feedUrlSuffix, GetCreatedPackages);
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'2015-01-01T00:00:00.0000000Z'&$orderby=Created" + _feedUrlSuffix, GetEmptyPackages);

            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$orderby=LastEdited" + _feedUrlSuffix, GetEditedPackages);
            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'2015-01-01T00:00:00.0000000Z'&$orderby=LastEdited" + _feedUrlSuffix, GetEmptyPackages);

            mockServer.SetAction("/package/ListedPackage/1.0.0", request => GetStreamContentActionAsync(request, "Packages\\ListedPackage.1.0.0.zip"));
            mockServer.SetAction("/package/ListedPackage/1.0.1", request => GetStreamContentActionAsync(request, "Packages\\ListedPackage.1.0.1.zip"));
            mockServer.SetAction("/package/UnlistedPackage/1.0.0", request => GetStreamContentActionAsync(request, "Packages\\UnlistedPackage.1.0.0.zip"));

            // Act
            var feed2catalogTestJob = new TestableFeed2CatalogJob(
                mockServer,
                _feedBaseUri,
                catalogStorage,
                auditingStorage,
                skipCreatedPackagesProcessing: false,
                startDate: null,
                timeout: TimeSpan.FromMinutes(5),
                top: 20,
                verbose: true);
            await feed2catalogTestJob.RunOnce(CancellationToken.None);

            // Assert
            Assert.Equal(6, catalogStorage.Content.Count);

            // Ensure catalog has index.json
            var catalogIndex = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("index.json"));
            Assert.NotNull(catalogIndex.Key);
            Assert.Contains("\"nuget:lastCreated\":\"2015-01-01T00:00:00Z\"", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastDeleted\":\"2015-01-01T01:01:01", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastEdited\":\"2015-01-01T00:00:00", catalogIndex.Value.GetContentString());

            // Ensure catalog has page0.json
            var pageZero = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("page0.json"));
            Assert.NotNull(pageZero.Key);
            Assert.Contains("\"parent\":\"http://tempuri.org/index.json\",", pageZero.Value.GetContentString());

            Assert.Contains("/listedpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"ListedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            Assert.Contains("/listedpackage.1.0.1.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"ListedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.1\"", pageZero.Value.GetContentString());

            Assert.Contains("/unlistedpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"UnlistedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            Assert.Contains("/otherpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"OtherPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            // Check individual package entries
            var package1 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage.1.0.0.json"));
            Assert.NotNull(package1.Key);
            Assert.Contains("\"PackageDetails\",", package1.Value.GetContentString());
            Assert.Contains("\"id\": \"ListedPackage\",", package1.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package1.Value.GetContentString());

            var package2 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage.1.0.1.json"));
            Assert.NotNull(package2.Key);
            Assert.Contains("\"PackageDetails\",", package2.Value.GetContentString());
            Assert.Contains("\"id\": \"ListedPackage\",", package2.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.1\",", package2.Value.GetContentString());

            var package3 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage.1.0.0.json"));
            Assert.NotNull(package3.Key);
            Assert.Contains("\"PackageDetails\",", package3.Value.GetContentString());
            Assert.Contains("\"id\": \"UnlistedPackage\",", package3.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package3.Value.GetContentString());

            // Ensure catalog has the delete of "OtherPackage"
            var package4 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage.1.0.0.json"));
            Assert.NotNull(package4.Key);
            Assert.Contains("\"PackageDelete\",", package4.Value.GetContentString());
            Assert.Contains("\"id\": \"OtherPackage\",", package4.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package4.Value.GetContentString());
        }

        [Fact]
        public async Task AppendsDeleteAndReinsertToExistingCatalog()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackages();
            var auditingStorage = new MemoryStorage();
            auditingStorage.Content.TryAdd(
                new Uri(auditingStorage.BaseAddress, "package/OtherPackage/1.0.0/2015-01-01T00:01:01-deleted.audit.v1.json"),
                new StringStorageContent(TestCatalogEntries.DeleteAuditRecordForOtherPackage100));

            var mockServer = new MockServerHttpClientHandler();

            mockServer.SetAction(" / ", GetRootActionAsync);
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$orderby=Created" + _feedUrlSuffix, GetCreatedPackages);
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'2015-01-01T00:00:00.0000000Z'&$orderby=Created" + _feedUrlSuffix, GetCreatedPackagesSecondRequest);
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'2015-01-01T01:01:03.0000000Z'&$orderby=Created" + _feedUrlSuffix, GetEmptyPackages);

            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$orderby=LastEdited" + _feedUrlSuffix, GetEditedPackages);
            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'2015-01-01T00:00:00.0000000Z'&$orderby=LastEdited" + _feedUrlSuffix, GetEmptyPackages);

            mockServer.SetAction("/package/ListedPackage/1.0.0", request => GetStreamContentActionAsync(request, "Packages\\ListedPackage.1.0.0.zip"));
            mockServer.SetAction("/package/ListedPackage/1.0.1", request => GetStreamContentActionAsync(request, "Packages\\ListedPackage.1.0.1.zip"));
            mockServer.SetAction("/package/UnlistedPackage/1.0.0", request => GetStreamContentActionAsync(request, "Packages\\UnlistedPackage.1.0.0.zip"));
            mockServer.SetAction("/package/OtherPackage/1.0.0", request => GetStreamContentActionAsync(request, "Packages\\OtherPackage.1.0.0.zip"));

            // Act
            var feed2catalogTestJob = new TestableFeed2CatalogJob(
                mockServer,
                _feedBaseUri,
                catalogStorage,
                auditingStorage,
                skipCreatedPackagesProcessing: false,
                startDate: null,
                timeout: TimeSpan.FromMinutes(5),
                top: 20,
                verbose: true);
            await feed2catalogTestJob.RunOnce(CancellationToken.None);

            // Assert
            Assert.Equal(7, catalogStorage.Content.Count);

            // Ensure catalog has index.json
            var catalogIndex = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("index.json"));
            Assert.NotNull(catalogIndex.Key);
            Assert.Contains("\"nuget:lastCreated\":\"2015-01-01T01:01:03Z\"", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastDeleted\":\"2015-01-01T01:01:01", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastEdited\":\"2015-01-01T00:00:00", catalogIndex.Value.GetContentString());

            // Ensure catalog has page0.json
            var pageZero = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("page0.json"));
            Assert.NotNull(pageZero.Key);
            Assert.Contains("\"parent\":\"http://tempuri.org/index.json\",", pageZero.Value.GetContentString());

            Assert.Contains("/listedpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"ListedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            Assert.Contains("/listedpackage.1.0.1.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"ListedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.1\"", pageZero.Value.GetContentString());

            Assert.Contains("/unlistedpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"UnlistedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            Assert.Contains("/otherpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"OtherPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            // Check individual package entries
            var package1 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage.1.0.0.json"));
            Assert.NotNull(package1.Key);
            Assert.Contains("\"PackageDetails\",", package1.Value.GetContentString());
            Assert.Contains("\"id\": \"ListedPackage\",", package1.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package1.Value.GetContentString());

            var package2 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage.1.0.1.json"));
            Assert.NotNull(package2.Key);
            Assert.Contains("\"PackageDetails\",", package2.Value.GetContentString());
            Assert.Contains("\"id\": \"ListedPackage\",", package2.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.1\",", package2.Value.GetContentString());

            var package3 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage.1.0.0.json"));
            Assert.NotNull(package3.Key);
            Assert.Contains("\"PackageDetails\",", package3.Value.GetContentString());
            Assert.Contains("\"id\": \"UnlistedPackage\",", package3.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package3.Value.GetContentString());

            // Ensure catalog has the delete of "OtherPackage"
            var package4 = catalogStorage.Content.FirstOrDefault(pair =>
                pair.Key.PathAndQuery.EndsWith("/otherpackage.1.0.0.json")
                && pair.Value.GetContentString().Contains("\"PackageDelete\""));
            Assert.NotNull(package4.Key);
            Assert.Contains("\"PackageDelete\",", package4.Value.GetContentString());
            Assert.Contains("\"id\": \"OtherPackage\",", package4.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package4.Value.GetContentString());

            // Ensure catalog has the insert of "OtherPackage"
            var package5 = catalogStorage.Content.FirstOrDefault(pair =>
                pair.Key.PathAndQuery.EndsWith("/otherpackage.1.0.0.json")
                && pair.Value.GetContentString().Contains("\"PackageDetails\""));
            Assert.NotNull(package5.Key);
            Assert.Contains("\"PackageDetails\",", package5.Value.GetContentString());
            Assert.Contains("\"id\": \"OtherPackage\",", package5.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package5.Value.GetContentString());
        }

        [Fact]
        public async Task RunInternal_CallsCatalogStorageLoadStringExactlyOnce()
        {
            var mockServer = new MockServerHttpClientHandler();
            var auditingStorage = Mock.Of<IStorage>();
            var catalogStorage = new Mock<IStorage>(MockBehavior.Strict);
            var datetime = DateTime.MinValue.ToString("O") + "Z";
            var json = $"{{\"nuget:lastCreated\":\"{datetime}\"," +
                $"\"nuget:lastDeleted\":\"{datetime}\"," +
                $"\"nuget:lastEdited\":\"{datetime}\"}}";

            catalogStorage.Setup(x => x.ResolveUri(It.IsNotNull<string>()))
                .Returns(new Uri(_feedBaseUri));
            catalogStorage.Setup(x => x.LoadString(It.IsNotNull<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(json);

            mockServer.SetAction("/", GetRootActionAsync);
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$orderby=Created" + _feedUrlSuffix, GetEmptyPackages);
            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$orderby=LastEdited" + _feedUrlSuffix, GetEmptyPackages);

            var feed2catalogTestJob = new TestableFeed2CatalogJob(
                mockServer,
                _feedBaseUri,
                catalogStorage.Object,
                auditingStorage,
                skipCreatedPackagesProcessing: false,
                startDate: null,
                timeout: TimeSpan.FromMinutes(5),
                top: 20,
                verbose: true);

            await feed2catalogTestJob.RunOnce(CancellationToken.None);

            catalogStorage.Verify(x => x.ResolveUri(It.IsNotNull<string>()), Times.AtLeastOnce());
            catalogStorage.Verify(x => x.LoadString(It.IsNotNull<Uri>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        private static Task<HttpResponseMessage> GetCreatedPackages(HttpRequestMessage request)
        {
            var packages = new List<ODataPackage>
            {
                new ODataPackage
                {
                    Id = "ListedPackage",
                    Version = "1.0.0",
                    Description = "Listed package",
                    Hash = "",
                    Listed = true,

                    Created = new DateTime(2015, 1, 1),
                    Published = new DateTime(2015, 1, 1)
                },
                new ODataPackage
                {
                    Id = "UnlistedPackage",
                    Version = "1.0.0",
                    Description = "Unlisted package",
                    Hash = "",
                    Listed = false,

                    Created = new DateTime(2015, 1, 1),
                    Published = Convert.ToDateTime("1900-01-01T00:00:00Z")
                },
                new ODataPackage
                {
                    Id = "TestPackage.SemVer2",
                    Version = "1.0.0-alpha.1+githash",
                    Description = "A package with SemVer 2.0.0",
                    Hash = "",
                    Listed = false,

                    Created = new DateTime(2015, 1, 1),
                    Published = new DateTime(2015, 1, 1)
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    ODataFeedHelper.ToODataFeed(packages, new Uri(_feedBaseUri), "Packages"))
            });
        }

        private static Task<HttpResponseMessage> GetEditedPackages(HttpRequestMessage request)
        {
            var packages = new List<ODataPackage>
            {
                new ODataPackage
                {
                    Id = "ListedPackage",
                    Version = "1.0.1",
                    Description = "Listed package",
                    Hash = "",
                    Listed = true,

                    Created = new DateTime(2014, 1, 1),
                    LastEdited = new DateTime(2015, 1, 1),
                    Published = new DateTime(2014, 1, 1)
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    ODataFeedHelper.ToODataFeed(packages, new Uri(_feedBaseUri), "Packages"))
            });
        }

        private static Task<HttpResponseMessage> GetEmptyPackages(HttpRequestMessage request)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    ODataFeedHelper.ToODataFeed(Enumerable.Empty<ODataPackage>(), new Uri(_feedBaseUri), "Packages"))
            });
        }

        private static Task<HttpResponseMessage> GetCreatedPackagesSecondRequest(HttpRequestMessage request)
        {
            var packages = new List<ODataPackage>
            {
                new ODataPackage
                {
                    Id = "OtherPackage",
                    Version = "1.0.0",
                    Description = "Other package",
                    Hash = "",
                    Listed = true,

                    Created = new DateTime(2015, 1, 1, 1, 1, 3),
                    Published = new DateTime(2015, 1, 1, 1, 1, 3)
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    ODataFeedHelper.ToODataFeed(packages, new Uri(_feedBaseUri), "Packages"))
            });
        }

        private void InitializeTest(bool skipCreatedPackagesProcessing = false)
        {
            _skipCreatedPackagesProcessing = skipCreatedPackagesProcessing;

            _job = new TestableFeed2CatalogJob(
                _server,
                _feedBaseUri,
                _catalogStorage,
                _auditingStorage,
                skipCreatedPackagesProcessing,
                startDate: null,
                timeout: TimeSpan.FromMinutes(5),
                top: _top,
                verbose: true);
        }

        private PackageCreationOrEdit CreatePackageCreationOrEdit()
        {
            var package = TestPackage.Create(_random);
            var isListed = Convert.ToBoolean(_random.Next(minValue: 0, maxValue: 2));
            var created = DateTimeOffset.UtcNow;

            // Avoid hitting this bug accidentally:  https://github.com/NuGet/NuGetGallery/issues/2841
            if (created == _packageOperations.OfType<PackageCreationOrEdit>().LastOrDefault()?.ODataPackage.Created)
            {
                created = created.AddMilliseconds(1);
            }

            var oDataPackage = new ODataPackage()
            {
                Id = package.Id,
                Version = package.Version.ToNormalizedString(),
                Description = package.Description,
                Hash = GetPackageHash(package),
                Listed = isListed,
                Created = created.UtcDateTime,
                Published = isListed ? created.UtcDateTime : Constants.UnpublishedDate
            };

            return new PackageCreationOrEdit(package, oDataPackage);
        }

        private PackageCreationOrEdit AddCreatedPackageToFeed()
        {
            var operation = CreatePackageCreationOrEdit();

            _packageOperations.Add(operation);

            return operation;
        }

        private PackageCreationOrEdit AddEditedPackageToFeed(PackageCreationOrEdit entry)
        {
            var editedPackage = AddPackageEntry(entry.Package);
            var edited = DateTimeOffset.UtcNow;

            // Avoid hitting this bug accidentally:  https://github.com/NuGet/NuGetGallery/issues/2841
            if (edited == _packageOperations.OfType<PackageCreationOrEdit>().LastOrDefault(e => e.ODataPackage.LastEdited.HasValue)?.ODataPackage.LastEdited)
            {
                edited = edited.AddMilliseconds(1);
            }

            var oDataPackage = new ODataPackage()
            {
                Id = entry.ODataPackage.Id,
                Version = entry.ODataPackage.Version,
                Description = entry.ODataPackage.Description,
                Hash = GetPackageHash(editedPackage),
                Listed = entry.ODataPackage.Listed,
                Created = entry.ODataPackage.Created,
                LastEdited = edited.UtcDateTime,
                Published = entry.ODataPackage.Published
            };

            var operation = new PackageCreationOrEdit(editedPackage, oDataPackage);

            _packageOperations.Add(operation);

            return operation;
        }

        private void AddDeletedPackage(
            PackageCreationOrEdit operation,
            DateTime? deletionTime = null,
            bool isSoftDelete = false)
        {
            // By default, avoid the deletion time being equal to the catalog's last deleted cursor.
            deletionTime = deletionTime ?? GetUniqueDateTime();

            var auditRecord = new JObject(
                new JProperty("record",
                    new JObject(
                        new JProperty("id", operation.Package.Id),
                        new JProperty("version", operation.Package.Version.ToNormalizedString()))),
                new JProperty("actor",
                    new JObject(
                        new JProperty("timestampUtc", deletionTime.Value.ToString(_auditRecordDateTimeFormat)))));

            var packageId = operation.Package.Id.ToLowerInvariant();
            var packageVersion = operation.Package.Version.ToNormalizedString().ToLowerInvariant();
            var fileNamePostfix = isSoftDelete ? "softdelete.audit.v1.json" : "Deleted.audit.v1.json";
            var uri = new Uri($"https://nuget.test/auditing/{packageId}/{packageVersion}-{fileNamePostfix}");

            _auditingStorage.Content.TryAdd(uri, new JTokenStorageContent(auditRecord));

            _packageOperations.Add(new PackageDeletion(uri, auditRecord, deletionTime.Value));
        }

        private async Task RunInternalAndVerifyAsync(CancellationToken cancellationToken, PackageOperation skippedOperation = null)
        {
            if (_hasFirstRunOnceAsyncBeenCalledBefore)
            {
                // Package details URL's contain a timestamp with second granularity.
                // Format:   <BaseUri>/data/yyyy.MM.dd.HH.mm.ss/<PackageIdLowerCase>.<PackageVersionLowerCase>.json
                // Example:  https://nuget.test/data/2018.07.20.20.15.30/bjxobigmgsxsossw.3.8.9.json
                // To ensure that successive catalog batches have unique timestamps, wait one full second.
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            _hasFirstRunOnceAsyncBeenCalledBefore = true;

            PrepareFeed();

            await _job.RunOnce(cancellationToken);

            VerifyCatalog(skippedOperation);
        }

        private void VerifyCatalog(PackageOperation skippedOperation)
        {
            List<PackageOperation> verifiablePackageOperations;

            if (_skipCreatedPackagesProcessing)
            {
                verifiablePackageOperations = _packageOperations
                    .Where(entry =>
                        entry != skippedOperation
                        && (!(entry is PackageCreationOrEdit) || ((PackageCreationOrEdit)entry).ODataPackage.LastEdited.HasValue))
                    .ToList();
            }
            else
            {
                verifiablePackageOperations = _packageOperations
                    .Where(entry => entry != skippedOperation)
                    .ToList();
            }

            if (verifiablePackageOperations.Count == 0)
            {
                Assert.Equal(0, _catalogStorage.Content.Count);

                return;
            }

            bool isEmptyCatalogBatch = _lastFeedEntriesCount == verifiablePackageOperations.Count;

            if (!isEmptyCatalogBatch)
            {
                ++_catalogBatchesProcessed;

                _lastFeedEntriesCount = verifiablePackageOperations.Count;
            }

            var expectedCatalogEntryCount = verifiablePackageOperations.Count
                + 1  // index.json
                + 1; // page0.json
            Assert.Equal(expectedCatalogEntryCount, _catalogStorage.Content.Count);

            var indexUri = new Uri(_baseUri, "index.json");
            var pageUri = new Uri(_baseUri, "page0.json");

            string commitId;
            string commitTimeStamp;

            VerifyCatalogIndex(
                verifiablePackageOperations,
                isEmptyCatalogBatch,
                indexUri,
                pageUri,
                out commitId,
                out commitTimeStamp);

            Assert.True(
                DateTime.TryParseExact(
                    commitTimeStamp,
                    _catalogCommitTimestampDateTimeFormat,
                    DateTimeFormatInfo.CurrentInfo,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var commitTimeStampDateTime));

            VerifyCatalogPage(verifiablePackageOperations, indexUri, pageUri, commitId, commitTimeStamp);
            VerifyCatalogPackageItems(verifiablePackageOperations, commitId, commitTimeStamp, commitTimeStampDateTime);

            Assert.True(verifiablePackageOperations.All(packageOperation => !string.IsNullOrEmpty(packageOperation.CommitId)));
        }

        private void VerifyCatalogIndex(
            List<PackageOperation> packageOperations,
            bool isEmptyCatalogBatch,
            Uri indexUri,
            Uri pageUri,
            out string commitId,
            out string commitTimeStamp)
        {
            Assert.True(_catalogStorage.Content.TryGetValue(indexUri, out var storage));
            Assert.IsType<JTokenStorageContent>(storage);

            var index = ((JTokenStorageContent)storage).Content as JObject;
            Assert.NotNull(index);

            index = ReadJsonWithoutDateTimeHandling(index);

            var properties = index.Properties();

            Assert.Equal(10, properties.Count());
            Assert.Equal(indexUri.AbsoluteUri, index[CatalogConstants.IdKeyword].Value<string>());
            Assert.Equal(
                new JArray(CatalogConstants.CatalogRoot, CatalogConstants.AppendOnlyCatalog, CatalogConstants.Permalink).ToString(),
                index[CatalogConstants.TypeKeyword].ToString());

            commitId = index[CatalogConstants.CommitId].Value<string>();
            Assert.True(Guid.TryParse(commitId, out var guid));

            commitTimeStamp = index[CatalogConstants.CommitTimeStamp].Value<string>();

            Assert.Equal(1, index[CatalogConstants.Count].Value<int>());

            if (_catalogBatchesProcessed > 1 && _catalogLastDeleted == Constants.DateTimeMinValueUtc && !isEmptyCatalogBatch)
            {
                _catalogLastDeleted = _catalogLastCreated;
            }

            _catalogLastEdited = packageOperations.OfType<PackageCreationOrEdit>()
                .Where(entry => entry.ODataPackage.LastEdited.HasValue)
                .Select(entry => entry.ODataPackage.LastEdited.Value)
                .DefaultIfEmpty(Constants.DateTimeMinValueUtc)
                .Max();

            if (_skipCreatedPackagesProcessing)
            {
                _catalogLastCreated = _catalogLastEdited;
            }
            else
            {
                _catalogLastCreated = packageOperations.OfType<PackageCreationOrEdit>()
                    .Max(entry => entry.ODataPackage.Created);
            }

            _catalogLastDeleted = packageOperations.OfType<PackageDeletion>()
                .Select(package => package.DeletionTime)
                .DefaultIfEmpty(_catalogLastDeleted)
                .Select(deletionTime => deletionTime.UtcDateTime)
                .Max();

            Assert.Equal(
                _catalogLastCreated.ToString(_catalogDateTimeFormat),
                index[CatalogConstants.NuGetLastCreated].Value<string>());
            Assert.Equal(
                _catalogLastEdited.ToString(_catalogDateTimeFormat),
                index[CatalogConstants.NuGetLastEdited].Value<string>());
            Assert.Equal(
                _catalogLastDeleted.ToString(_catalogDateTimeFormat),
                index[CatalogConstants.NuGetLastDeleted].Value<string>());

            var expectedItems = new JArray(
                new JObject(
                    new JProperty(CatalogConstants.IdKeyword, pageUri.AbsoluteUri),
                    new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.CatalogPage),
                    new JProperty(CatalogConstants.CommitId, commitId),
                    new JProperty(CatalogConstants.CommitTimeStamp, commitTimeStamp),
                    new JProperty(CatalogConstants.Count, packageOperations.Count)));

            Assert.Equal(expectedItems.ToString(), index[CatalogConstants.Items].ToString());

            VerifyContext(index);
        }

        private void VerifyCatalogPage(
            List<PackageOperation> packageOperations,
            Uri indexUri,
            Uri pageUri,
            string commitId,
            string commitTimeStamp)
        {
            Assert.True(_catalogStorage.Content.TryGetValue(pageUri, out var storage));
            Assert.IsType<JTokenStorageContent>(storage);

            var page = ((JTokenStorageContent)storage).Content as JObject;
            Assert.NotNull(page);

            page = ReadJsonWithoutDateTimeHandling(page);

            var properties = page.Properties();

            Assert.Equal(8, properties.Count());
            Assert.Equal(pageUri.AbsoluteUri, page[CatalogConstants.IdKeyword].Value<string>());
            Assert.Equal(CatalogConstants.CatalogPage, page[CatalogConstants.TypeKeyword].Value<string>());

            Assert.Equal(commitId, page[CatalogConstants.CommitId].Value<string>());
            Assert.Equal(commitTimeStamp, page[CatalogConstants.CommitTimeStamp].Value<string>());

            var actualPackageDetailsCount = page[CatalogConstants.Count].Value<int>();

            Assert.Equal(packageOperations.Count, actualPackageDetailsCount);

            Assert.Equal(indexUri.AbsoluteUri, page[CatalogConstants.Parent].Value<string>());

            // This is a bit tricky.
            // A catalog page lists package detail items in chronologically descending order,
            // which is the reverse order of our internal list.
            // https://github.com/NuGet/NuGetGallery/issues/4757
            // Also, within a catalog batch the order of individual items is nondeterministic.
            // We'll match up expected items with actual items by picking in the next commit batch
            // the first item we haven't seen already with the same package identity as the expected item.
            var items = page[CatalogConstants.Items].Reverse().ToArray();
            var itemsByPackageIdentity = items
                .GroupBy(item => new PackageIdentity(
                    item[CatalogConstants.NuGetId].Value<string>(),
                    new NuGetVersion(item[CatalogConstants.NuGetVersion].Value<string>())))
                .ToArray();
            var actualItemsCount = items.Count();
            Assert.Equal(packageOperations.Count, actualItemsCount);

            var unseenItems = new HashSet<JToken>(items);

            for (var i = 0; i < actualItemsCount; ++i)
            {
                var expectedItem = packageOperations[i];
                var actualItem = itemsByPackageIdentity.Single(group => group.Key.Equals(expectedItem.PackageIdentity))
                    .First(item => unseenItems.Contains(item));

                unseenItems.Remove(actualItem);

                var actualCommitId = actualItem[CatalogConstants.CommitId].Value<string>();
                var actualCommitTimeStamp = actualItem[CatalogConstants.CommitTimeStamp].Value<string>();

                if (i == actualItemsCount - 1)
                {
                    Assert.Equal(commitId, actualCommitId);
                    Assert.Equal(commitTimeStamp, actualCommitTimeStamp);
                }

                // If there are later catalog updates, we'll verify these haven't changed then.
                expectedItem.CommitId = expectedItem.CommitId ?? actualCommitId;
                expectedItem.CommitTimeStamp = expectedItem.CommitTimeStamp ?? actualCommitTimeStamp;

                var expectedTimestamp = DateTime.ParseExact(
                    expectedItem.CommitTimeStamp,
                    _catalogCommitTimestampDateTimeFormat,
                    DateTimeFormatInfo.CurrentInfo,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

                var expectedUri = GetPackageDetailsUri(expectedTimestamp, expectedItem);

                Assert.Equal(expectedUri.AbsoluteUri, actualItem[CatalogConstants.IdKeyword].Value<string>());

                var expectedType = expectedItem is PackageCreationOrEdit ? CatalogConstants.NuGetPackageDetails : CatalogConstants.NuGetPackageDelete;

                Assert.Equal(expectedType, actualItem[CatalogConstants.TypeKeyword].Value<string>());
                Assert.Equal(expectedItem.CommitId, actualCommitId);
                Assert.Equal(expectedItem.CommitTimeStamp, actualCommitTimeStamp);
                Assert.Equal(expectedItem.PackageIdentity.Id, actualItem[CatalogConstants.NuGetId].Value<string>());
                Assert.Equal(
                    expectedItem.PackageIdentity.Version.ToNormalizedString(),
                    actualItem[CatalogConstants.NuGetVersion].Value<string>());
            }

            VerifyContext(page);
        }

        private void VerifyCatalogPackageItems(
            List<PackageOperation> packageOperations,
            string commitId,
            string commitTimeStamp,
            DateTime commitTimeStampDateTime)
        {
            var lastEntry = packageOperations.Last();
            var packageDetailsUri = GetPackageDetailsUri(commitTimeStampDateTime, lastEntry);

            Assert.True(_catalogStorage.Content.TryGetValue(packageDetailsUri, out var storage));
            Assert.IsType<StringStorageContent>(storage);

            var packageDetails = JObject.Parse(((StringStorageContent)storage).Content);
            Assert.NotNull(packageDetails);

            packageDetails = ReadJsonWithoutDateTimeHandling(packageDetails);

            if (lastEntry is PackageCreationOrEdit)
            {
                VerifyCatalogPackageDetails(commitId, commitTimeStamp, (PackageCreationOrEdit)lastEntry, packageDetailsUri, packageDetails);
            }
            else
            {
                VerifyCatalogPackageDelete(commitId, commitTimeStamp, (PackageDeletion)lastEntry, packageDetailsUri, packageDetails);
            }
        }

        private void VerifyCatalogPackageDetails(
            string commitId,
            string commitTimeStamp,
            PackageCreationOrEdit lastEntry,
            Uri packageDetailsUri,
            JObject packageDetails)
        {
            var properties = packageDetails.Properties();

            Assert.Equal(19, properties.Count());
            Assert.Equal(packageDetailsUri.AbsoluteUri, packageDetails[CatalogConstants.IdKeyword].Value<string>());
            Assert.Equal(
                new JArray(CatalogConstants.PackageDetails, CatalogConstants.CatalogPermalink),
                packageDetails[CatalogConstants.TypeKeyword]);
            Assert.Equal(lastEntry.Package.Author, packageDetails[CatalogConstants.Authors].Value<string>());
            Assert.Equal(commitId, packageDetails[CatalogConstants.CatalogCommitId].Value<string>());
            Assert.Equal(commitTimeStamp, packageDetails[CatalogConstants.CatalogCommitTimeStamp].Value<string>());
            Assert.Equal(
                lastEntry.ODataPackage.Created.ToString(_catalogDateTimeFormat),
                packageDetails[CatalogConstants.Created].Value<string>());
            Assert.Equal(lastEntry.ODataPackage.Description, packageDetails[CatalogConstants.Description].Value<string>());
            Assert.Equal(lastEntry.ODataPackage.Id, packageDetails[CatalogConstants.Id].Value<string>());
            Assert.False(packageDetails[CatalogConstants.IsPrerelease].Value<bool>());
            Assert.Equal(
                (lastEntry.ODataPackage.LastEdited ?? Constants.DateTimeMinValueUtc).ToString(_catalogDateTimeFormat),
                packageDetails[CatalogConstants.LastEdited].Value<string>());
            Assert.Equal(lastEntry.ODataPackage.Listed, packageDetails[CatalogConstants.Listed].Value<bool>());
            Assert.Equal(lastEntry.ODataPackage.Hash, packageDetails[CatalogConstants.PackageHash].Value<string>());
            Assert.Equal(Constants.Sha512, packageDetails[CatalogConstants.PackageHashAlgorithm].Value<string>());
            Assert.Equal(lastEntry.Package.Stream.Length, packageDetails[CatalogConstants.PackageSize].Value<int>());
            Assert.Equal(
                lastEntry.ODataPackage.Published.ToString(_catalogDateTimeFormat),
                packageDetails[CatalogConstants.Published].Value<string>());
            Assert.Equal(lastEntry.Package.Version.ToFullString(), packageDetails[CatalogConstants.VerbatimVersion].Value<string>());
            Assert.Equal(lastEntry.Package.Version.ToNormalizedString(), packageDetails[CatalogConstants.Version].Value<string>());

            var expectedPackageEntries = GetPackageEntries(lastEntry.Package)
                .OrderBy(entry => entry.FullName)
                .Select(entry =>
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, new Uri(packageDetailsUri, $"#{entry.FullName}").AbsoluteUri),
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.PackageEntry),
                        new JProperty(CatalogConstants.CompressedLength, entry.CompressedLength),
                        new JProperty(CatalogConstants.FullName, entry.FullName),
                        new JProperty(CatalogConstants.Length, entry.Length),
                        new JProperty(CatalogConstants.Name, entry.Name)))
                .ToArray();

            var actualPackageEntries = packageDetails[CatalogConstants.PackageEntries]
                .Children()
                .OrderBy(token => token[CatalogConstants.FullName].Value<string>())
                .ToArray();

            Assert.Equal(expectedPackageEntries.Length, actualPackageEntries.Length);

            for (var i = 0; i < expectedPackageEntries.Length; ++i)
            {
                Assert.Equal(expectedPackageEntries[i].ToString(), actualPackageEntries[i].ToString());
            }

            var expectedContext = new JObject(
                new JProperty(CatalogConstants.VocabKeyword, CatalogConstants.NuGetSchemaUri),
                new JProperty(CatalogConstants.Catalog, CatalogConstants.NuGetCatalogSchemaUri),
                new JProperty(CatalogConstants.Xsd, CatalogConstants.XmlSchemaUri),
                new JProperty(CatalogConstants.Dependencies,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.Dependency),
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
                new JProperty(CatalogConstants.DependencyGroups,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.DependencyGroup),
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
                new JProperty(CatalogConstants.PackageEntries,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.PackageEntryUncapitalized),
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
                new JProperty(CatalogConstants.SupportedFrameworks,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.SupportedFramework),
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
                new JProperty(CatalogConstants.Tags,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.Tag),
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
                new JProperty(CatalogConstants.Published,
                    new JObject(new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
                new JProperty(CatalogConstants.Created,
                    new JObject(new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
                new JProperty(CatalogConstants.LastEdited,
                    new JObject(new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
                new JProperty(CatalogConstants.CatalogCommitTimeStamp,
                    new JObject(new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))));

            Assert.Equal(expectedContext.ToString(), packageDetails[CatalogConstants.ContextKeyword].ToString());
        }

        private void VerifyCatalogPackageDelete(
            string commitId,
            string commitTimeStamp,
            PackageDeletion lastEntry,
            Uri packageDeleteUri,
            JObject packageDelete)
        {
            var properties = packageDelete.Properties();

            Assert.Equal(9, properties.Count());
            Assert.Equal(packageDeleteUri.AbsoluteUri, packageDelete[CatalogConstants.IdKeyword].Value<string>());
            Assert.Equal(
                new JArray(CatalogConstants.PackageDelete, CatalogConstants.CatalogPermalink),
                packageDelete[CatalogConstants.TypeKeyword]);
            Assert.Equal(commitId, packageDelete[CatalogConstants.CatalogCommitId].Value<string>());
            Assert.Equal(commitTimeStamp, packageDelete[CatalogConstants.CatalogCommitTimeStamp].Value<string>());
            Assert.Equal(lastEntry.PackageIdentity.Id, packageDelete[CatalogConstants.Id].Value<string>());
            Assert.Equal(lastEntry.PackageIdentity.Id, packageDelete[CatalogConstants.OriginalId].Value<string>());
            Assert.Equal(lastEntry.Published.ToString(_catalogDateTimeFormat), packageDelete[CatalogConstants.Published].Value<string>());
            Assert.Equal(lastEntry.PackageIdentity.Version.ToNormalizedString(), packageDelete[CatalogConstants.Version].Value<string>());

            var expectedContext = new JObject(
                new JProperty(CatalogConstants.VocabKeyword, CatalogConstants.NuGetSchemaUri),
                new JProperty(CatalogConstants.Catalog, CatalogConstants.NuGetCatalogSchemaUri),
                new JProperty(CatalogConstants.Xsd, CatalogConstants.XmlSchemaUri),
                new JProperty(CatalogConstants.Details, CatalogConstants.CatalogDetails),
                new JProperty(CatalogConstants.CatalogCommitTimeStamp,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
                new JProperty(CatalogConstants.Published,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
                new JProperty(CatalogConstants.Categories,
                    new JObject(
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
                new JProperty(CatalogConstants.Entries,
                    new JObject(
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
                new JProperty(CatalogConstants.Links,
                    new JObject(
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
                new JProperty(CatalogConstants.Tags,
                    new JObject(
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
                new JProperty(CatalogConstants.PackageContent,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.IdKeyword))));

            Assert.Equal(expectedContext.ToString(), packageDelete[CatalogConstants.ContextKeyword].ToString());
        }

        private Uri GetPackageDetailsUri(DateTime catalogTimeStamp, PackageOperation packageOperation)
        {
            var packageId = packageOperation.PackageIdentity.Id.ToLowerInvariant();
            var packageVersion = packageOperation.PackageIdentity.Version.ToNormalizedString().ToLowerInvariant();

            return new Uri($"{_baseUri.AbsoluteUri}data/{catalogTimeStamp.ToString(_catalogUrlDateTimeFormat)}/{packageId}.{packageVersion}.json");
        }

        private DateTime GetUniqueDateTime()
        {
            var now = DateTimeOffset.UtcNow;

            if (_timestamp == now)
            {
                _timestamp = now.AddMilliseconds(1);
            }
            else
            {
                _timestamp = now;
            }

            return _timestamp.UtcDateTime;
        }

        private void PrepareFeed()
        {
            _server.Actions.Clear();

            _server.SetAction("/", GetRootActionAsync);

            PublishCreatedPackages();
            PublishEditedPackages();

            foreach (var packageOperation in _packageOperations.OfType<PackageCreationOrEdit>().Select(o => o.Package))
            {
                _server.SetAction(
                    $"/package/{packageOperation.Id}/{packageOperation.Version.ToNormalizedString()}",
                    request => GetStreamContentActionAsync(request, packageOperation.Stream));
            }
        }

        private void PublishCreatedPackages()
        {
            var feedUrlSuffix = $"&$top={_top}&$select=Id,NormalizedVersion,Created,LastEdited,Published,LicenseNames,LicenseReportUrl&semVerLevel=2.0.0";
            var packageOperations = _packageOperations.OfType<PackageCreationOrEdit>();

            foreach (var packageOperation in packageOperations)
            {
                _server.SetAction(
                    $"/Packages?$filter=Created%20gt%20DateTime'{_feedLastCreated.ToString(_feedUrlDateTimeFormat)}'&$orderby=Created" + feedUrlSuffix,
                    request => GetResponse(request, new[] { packageOperation.ODataPackage }));

                _feedLastCreated = packageOperation.ODataPackage.Created;
            }

            _server.SetAction(
                $"/Packages?$filter=Created%20gt%20DateTime'{_feedLastCreated.ToString(_feedUrlDateTimeFormat)}'&$orderby=Created" + feedUrlSuffix,
                request => GetResponse(request, Enumerable.Empty<ODataPackage>()));
        }

        private void PublishEditedPackages()
        {
            var feedUrlSuffix = $"&$top={_top}&$select=Id,NormalizedVersion,Created,LastEdited,Published,LicenseNames,LicenseReportUrl&semVerLevel=2.0.0";
            var packageOperations = _packageOperations.OfType<PackageCreationOrEdit>()
                .Where(entry => entry.ODataPackage.LastEdited.HasValue);

            foreach (var packageOperation in packageOperations)
            {
                _server.SetAction(
                    $"/Packages?$filter=LastEdited%20gt%20DateTime'{_feedLastEdited.ToString(_feedUrlDateTimeFormat)}'&$orderby=LastEdited" + feedUrlSuffix,
                    request => GetResponse(request, new[] { packageOperation.ODataPackage }));

                _feedLastEdited = packageOperation.ODataPackage.LastEdited.Value;
            }

            _server.SetAction(
                $"/Packages?$filter=LastEdited%20gt%20DateTime'{_feedLastEdited.ToString(_feedUrlDateTimeFormat)}'&$orderby=LastEdited" + feedUrlSuffix,
                request => GetResponse(request, Enumerable.Empty<ODataPackage>()));
        }

        private TestPackage AddPackageEntry(TestPackage package)
        {
            var stream = new MemoryStream();

            package.Stream.Position = 0;
            package.Stream.CopyTo(stream);

            stream.Position = 0;

            using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
            {
                var entryName = $"file{zip.Entries.Count - 1}.bin";
                var entry = zip.CreateEntry(entryName);

                using (var entryStream = entry.Open())
                using (var writer = new StreamWriter(entryStream))
                using (var rng = RandomNumberGenerator.Create())
                {
                    var byteCount = _random.Next(1, 10);
                    var bytes = new byte[byteCount];

                    rng.GetNonZeroBytes(bytes);

                    writer.Write(bytes);
                }
            }

            return new TestPackage(package.Id, package.Version, package.Author, package.Description, stream);
        }

        private static JObject ReadJsonWithoutDateTimeHandling(JObject jObject)
        {
            using (var stringReader = new StringReader(jObject.ToString()))
            using (var jsonReader = new JsonTextReader(stringReader))
            {
                jsonReader.DateParseHandling = DateParseHandling.None;

                return JToken.ReadFrom(jsonReader) as JObject;
            }
        }

        private static IReadOnlyList<PackageEntry> GetPackageEntries(TestPackage package)
        {
            using (var zip = new ZipArchive(package.Stream, ZipArchiveMode.Read, leaveOpen: true))
            {
                return zip.Entries.Select(entry => new PackageEntry(entry)).ToArray();
            }
        }

        private static string GetPackageHash(TestPackage package)
        {
            using (var hashAlgorithm = HashAlgorithm.Create(Constants.Sha512))
            {
                package.Stream.Position = 0;

                var hash = hashAlgorithm.ComputeHash(package.Stream);

                package.Stream.Position = 0;

                return Convert.ToBase64String(hash);
            }
        }

        private static Task<HttpResponseMessage> GetResponse(HttpRequestMessage request, IEnumerable<ODataPackage> packages)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ODataFeedHelper.ToODataFeed(packages, new Uri(_feedBaseUri), "Packages"))
            });
        }

        private static Task<HttpResponseMessage> GetRootActionAsync(HttpRequestMessage request)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        private static Task<HttpResponseMessage> GetStreamContentActionAsync(HttpRequestMessage request, Stream stream)
        {
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(stream)
                });
        }

        private static Task<HttpResponseMessage> GetStreamContentActionAsync(HttpRequestMessage request, string filePath)
        {
            return GetStreamContentActionAsync(request, File.OpenRead(filePath));
        }

        private static void VerifyContext(JObject indexOrPage)
        {
            var expectedContext = new JObject(
                new JProperty(CatalogConstants.VocabKeyword, CatalogConstants.NuGetCatalogSchemaUri),
                new JProperty(CatalogConstants.NuGet, CatalogConstants.NuGetSchemaUri),
                new JProperty(CatalogConstants.Items,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.Item),
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
                new JProperty(CatalogConstants.Parent,
                    new JObject(new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.IdKeyword))),
                new JProperty(CatalogConstants.CommitTimeStamp,
                    new JObject(new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XmlDateTimeSchemaUri))),
                new JProperty(CatalogConstants.NuGetLastCreated,
                    new JObject(new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XmlDateTimeSchemaUri))),
                new JProperty(CatalogConstants.NuGetLastEdited,
                    new JObject(new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XmlDateTimeSchemaUri))),
                new JProperty(CatalogConstants.NuGetLastDeleted,
                    new JObject(new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XmlDateTimeSchemaUri))));

            Assert.Equal(expectedContext.ToString(), indexOrPage[CatalogConstants.ContextKeyword].ToString());
        }

        private abstract class PackageOperation
        {
            internal string CommitId { get; set; }
            internal string CommitTimeStamp { get; set; }
            internal abstract PackageIdentity PackageIdentity { get; }
        }

        private sealed class PackageCreationOrEdit : PackageOperation
        {
            internal ODataPackage ODataPackage { get; }
            internal TestPackage Package { get; }
            internal override PackageIdentity PackageIdentity { get; }

            internal PackageCreationOrEdit(TestPackage package, ODataPackage oDataPackage)
            {
                Package = package;
                ODataPackage = oDataPackage;
                PackageIdentity = new PackageIdentity(package.Id, package.Version);
            }
        }

        private sealed class PackageDeletion : PackageOperation
        {
            internal DateTimeOffset DeletionTime { get; }
            internal JObject Json { get; }
            internal override PackageIdentity PackageIdentity { get; }
            internal DateTime Published => DeletionTime.UtcDateTime;
            internal Uri Uri { get; }

            internal PackageDeletion(Uri uri, JObject json, DateTimeOffset deletionTime)
            {
                Uri = uri;
                Json = json;
                DeletionTime = deletionTime;

                var record = json["record"];

                PackageIdentity = new PackageIdentity(
                    record["id"].Value<string>(),
                    new NuGetVersion(record["version"].Value<string>()));
            }
        }
    }
}
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
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NgTests
{
    public class Db2CatalogTests : IDisposable
    {
        private const string PackageContentUrlFormat = "https://unittest.org/packages/{id-lower}/{version-lower}.nupkg";

        private bool _isDisposed;
        private DateTime _feedLastCreated;
        private DateTime _feedLastEdited;
        private DateTimeOffset _timestamp;
        private bool _hasFirstRunOnceAsyncBeenCalledBefore;
        private int _lastFeedEntriesCount;
        private readonly List<PackageOperation> _packageOperations;
        private readonly Random _random;
        private readonly MemoryStorage _auditingStorage;
        private readonly MemoryStorage _catalogStorage;
        private TestableDb2CatalogJob _job;
        private readonly Uri _baseUri;
        private bool _skipCreatedPackagesProcessing;
        private readonly MockServerHttpClientHandler _server;
        private readonly PackageContentUriBuilder _packageContentUriBuilder;
        private readonly ITestOutputHelper _testOutputHelper;

        public Db2CatalogTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _server = new MockServerHttpClientHandler();
            _random = new Random();
            _packageOperations = new List<PackageOperation>();
            _baseUri = new Uri("http://unit.test");

            _catalogStorage = new MemoryStorage(_baseUri);
            _auditingStorage = new MemoryStorage(_baseUri);

            _packageContentUriBuilder = new PackageContentUriBuilder(PackageContentUrlFormat);
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
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(skipCreatedPackagesProcessing, top, galleryDatabaseMock);

            await RunInternalAndVerifyAsync(galleryDatabaseMock, top);
        }

        [Fact]
        public async Task RunInternal_WithNoCatalogAndCreatedPackageInFeed_CreatesCatalog()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc);
        }

        [Fact]
        public async Task RunInternal_WithNoCatalogAndCreatedPackageInFeedAndWithCreatedPackagesSkipped_DoesNotCreateCatalog()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: true,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(galleryDatabaseMock, top);
        }

        [Fact]
        public async Task RunInternal_WithCatalogAndNoActivity_DoesNotUpdateCatalog()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package = AddCreatedPackageToFeed();

            // Create the catalog.
            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc);

            // Nothing new in the feed this time.
            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc);
        }

        [Fact]
        public async Task RunInternal_WithCatalogAndNoActivityAndWithCreatedPackagesSkipped_DoesNotUpdateCatalog()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: true,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package = CreatePackageCreationOrEdit();
            var editedPackage = AddEditedPackageToFeed(package);

            // Create the catalog.
            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: editedPackage.FeedPackageDetails.LastEditedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: editedPackage.FeedPackageDetails.LastEditedDate);

            // Nothing new in the feed this time.
            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: editedPackage.FeedPackageDetails.LastEditedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: editedPackage.FeedPackageDetails.LastEditedDate);
        }

        [Fact]
        // This test verifies current, flawed behavior.
        // https://github.com/NuGet/NuGetGallery/issues/2841
        public async Task RunInternal_WithPackagesWithSameCreatedTimeInFeedAndWhenProcessedInDifferentCatalogBatches_SkipsSecondEntry()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package1 = AddCreatedPackageToFeed();
            var package2 = AddCreatedPackageToFeed(package1.FeedPackageDetails.CreatedDate);

            // Remove the "package2" argument if/when the bug is fixed.
            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package1.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc,
                skippedPackage: package2);
        }

        [Fact]
        // This test verifies current, flawed behavior.
        // https://github.com/NuGet/NuGetGallery/issues/2841
        public async Task RunInternal_WithPackagesWithSameLastEditedTimeInFeedAndWhenProcessedInDifferentCatalogBatches_SkipsSecondEntry()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package1 = AddCreatedPackageToFeed();
            var package2 = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package2.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: package1.FeedPackageDetails.CreatedDate,
                expectedLastEdited: Constants.DateTimeMinValueUtc);

            package1 = AddEditedPackageToFeed(package1);
            package2 = AddEditedPackageToFeed(package2, package1.FeedPackageDetails.LastEditedDate);

            // Remove the "package2" argument if/when the bug is fixed.
            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package2.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: package1.FeedPackageDetails.CreatedDate,
                expectedLastEdited: package1.FeedPackageDetails.LastEditedDate,
                skippedPackage: package2);
        }

        [Fact]
        public async Task RunInternal_WithCreatedPackagesInFeedAtDifferentTimes_UpdatesCatalog()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package1 = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package1.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc);

            var package2 = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package2.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: package1.FeedPackageDetails.CreatedDate,
                expectedLastEdited: Constants.DateTimeMinValueUtc);
        }

        [Fact]
        public async Task RunInternal_WithCreatedPackagesInFeedAtDifferentTimesAndWithCreatedPackagesSkipped_DoesNotUpdateCatalog()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: true,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(galleryDatabaseMock, top);

            AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(galleryDatabaseMock, top);
        }

        [Fact]
        public async Task RunInternal_WithCreatedPackageAndEditedPackageInFeedAtDifferentTimes_UpdatesCatalog()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc);

            var editedPackage = AddEditedPackageToFeed(package);

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: package.FeedPackageDetails.CreatedDate,
                expectedLastEdited: editedPackage.FeedPackageDetails.LastEditedDate);
        }

        [Fact]
        public async Task RunInternal_WithCreatedPackageAndEditedPackageInFeedAtDifferentTimesAndWithCreatedPackagesSkipped_UpdatesCatalog()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: true,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc);

            var editedPackage = AddEditedPackageToFeed(package);

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: editedPackage.FeedPackageDetails.LastEditedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: editedPackage.FeedPackageDetails.LastEditedDate);
        }

        [Fact]
        public async Task RunInternal_WithCreatedPackageAndEditedPackageInFeedAtSameTime_UpdatesCatalog()
        {
            const int top = 1;
            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc);

            var editedPackage = AddEditedPackageToFeed(package);

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: package.FeedPackageDetails.CreatedDate,
                expectedLastEdited: editedPackage.FeedPackageDetails.LastEditedDate);
        }

        [Fact]
        public async Task RunInternal_WithCreatedPackageAndEditedPackageInFeedAtSameTimeAndWithCreatedPackagesSkipped_UpdatesCatalog()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: true,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc);

            var editedPackage = AddEditedPackageToFeed(package);

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: editedPackage.FeedPackageDetails.LastEditedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: editedPackage.FeedPackageDetails.LastEditedDate);
        }

        [Fact]
        public async Task RunInternal_WithEditedPackagesAndWithCreatedPackagesSkipped_UpdatesCatalog()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: true,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package = CreatePackageCreationOrEdit();
            var lastDeleted = Constants.DateTimeMinValueUtc;

            for (var i = 0; i < 3; ++i)
            {
                package = AddEditedPackageToFeed(package);

                await RunInternalAndVerifyAsync(
                    galleryDatabaseMock,
                    top,
                    expectedLastCreated: package.FeedPackageDetails.LastEditedDate,
                    expectedLastDeleted: lastDeleted,
                    expectedLastEdited: package.FeedPackageDetails.LastEditedDate);

                if (lastDeleted == Constants.DateTimeMinValueUtc)
                {
                    lastDeleted = package.FeedPackageDetails.LastEditedDate;
                }
            }
        }

        [Fact]
        public async Task RunInternal_WithCreatedPackageThenDeletedPackage_UpdatesCatalog()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc);

            var deletedPackage = AddDeletedPackage(package);

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: deletedPackage.DeletionTime.UtcDateTime,
                expectedLastEdited: Constants.DateTimeMinValueUtc);
        }

        [Fact]
        public async Task RunInternal_WithMultipleDeletedPackagesWithDifferentPackageIdentities_ProcessesAllDeletions()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package1 = AddCreatedPackageToFeed();
            var package2 = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package2.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: package1.FeedPackageDetails.CreatedDate,
                expectedLastEdited: Constants.DateTimeMinValueUtc);

            var deletedPackage1 = AddDeletedPackage(package1);
            var deletedPackage2 = AddDeletedPackage(package2);

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package2.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: deletedPackage2.DeletionTime.UtcDateTime,
                expectedLastEdited: Constants.DateTimeMinValueUtc);
        }

        [Fact]
        public async Task RunInternal_WithMultipleDeletedPackagesWithSamePackageIdentity_PutsEachPackageInSeparateCommit()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc);

            var deletionTime = DateTimeOffset.UtcNow;

            var deletedPackage1 = AddDeletedPackage(package, deletionTime.UtcDateTime, isSoftDelete: true);
            var deletedPackage2 = AddDeletedPackage(package, deletionTime.UtcDateTime, isSoftDelete: false);

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: deletionTime.UtcDateTime,
                expectedLastEdited: Constants.DateTimeMinValueUtc);
        }

        [Fact]
        public async Task RunInternal_WithDeletedPackageOlderThan15MinutesAgo_SkipsDeletedPackage()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc);

            var deletedPackage = AddDeletedPackage(package, deletionTime: DateTime.UtcNow.AddMinutes(-16));

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: Constants.DateTimeMinValueUtc,
                expectedLastEdited: Constants.DateTimeMinValueUtc,
                skippedPackage: deletedPackage);
        }

        [Fact]
        public async Task RunInternal_WithMultipleCreatedPackages_ProcessesAllCreations()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package1 = AddCreatedPackageToFeed();
            var package2 = AddCreatedPackageToFeed();

            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package2.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: package1.FeedPackageDetails.CreatedDate,
                expectedLastEdited: Constants.DateTimeMinValueUtc);
        }

        [Fact]
        public async Task RunInternal_WithMultipleEditedPackages_ProcessesAllEdits()
        {
            const int top = 1;

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>(MockBehavior.Strict);

            InitializeTest(
                skipCreatedPackagesProcessing: false,
                top: top,
                galleryDatabaseMock: galleryDatabaseMock);

            var package1 = AddCreatedPackageToFeed();
            var package2 = AddCreatedPackageToFeed();

            // Create the catalog.
            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package2.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: package1.FeedPackageDetails.CreatedDate,
                expectedLastEdited: Constants.DateTimeMinValueUtc);

            var editedPackage1 = AddEditedPackageToFeed(package1);
            var editedPackage2 = AddEditedPackageToFeed(package2);

            // Now test multiple edits.
            await RunInternalAndVerifyAsync(
                galleryDatabaseMock,
                top,
                expectedLastCreated: package2.FeedPackageDetails.CreatedDate,
                expectedLastDeleted: package1.FeedPackageDetails.CreatedDate,
                expectedLastEdited: editedPackage2.FeedPackageDetails.LastEditedDate);
        }

        [Fact]
        public async Task CreatesNewCatalogFromCreatedAndEditedAndDeletedPackages()
        {
            // Arrange
            const int top = 20;

            var catalogStorage = new MemoryStorage();
            var auditingStorage = new MemoryStorage();
            auditingStorage.Content.TryAdd(
                new Uri(auditingStorage.BaseAddress, "package/OtherPackage/1.0.0/2015-01-01T00:01:01-deleted.audit.v1.json"),
                new StringStorageContent(TestCatalogEntries.DeleteAuditRecordForOtherPackage100));

            var mockServer = new MockServerHttpClientHandler();
            RegisterPackageContentUri(mockServer, "ListedPackage", "1.0.0", "Packages\\ListedPackage.1.0.0.zip");
            RegisterPackageContentUri(mockServer, "ListedPackage", "1.0.1", "Packages\\ListedPackage.1.0.1.zip");
            RegisterPackageContentUri(mockServer, "UnlistedPackage", "1.0.0", "Packages\\UnlistedPackage.1.0.0.zip");
            RegisterPackageContentUri(mockServer, "TestPackage.SemVer2", "1.0.0-alpha.1", "Packages\\TestPackage.SemVer2.1.0.0-alpha.1.nupkg");

            var cursor1 = new DateTime(1, 1, 1).ForceUtc();
            var cursor2 = new DateTime(2015, 1, 1).ForceUtc();

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>();

            galleryDatabaseMock
                .Setup(m => m.GetPackagesCreatedSince(cursor1, top))
                .ReturnsAsync(GetCreatedPackages);

            galleryDatabaseMock
                .Setup(m => m.GetPackagesCreatedSince(cursor2, top))
                .ReturnsAsync(GetEmptyPackages);

            galleryDatabaseMock
                .Setup(m => m.GetPackagesEditedSince(cursor1, top))
                .ReturnsAsync(GetEditedPackages);

            galleryDatabaseMock
                .Setup(m => m.GetPackagesEditedSince(cursor2, top))
                .ReturnsAsync(GetEmptyPackages);

            // Act
            var db2catalogTestJob = new TestableDb2CatalogJob(
                mockServer,
                catalogStorage,
                auditingStorage,
                skipCreatedPackagesProcessing: false,
                startDate: null,
                timeout: TimeSpan.FromMinutes(5),
                top: top,
                verbose: true,
                galleryDatabaseMock: galleryDatabaseMock,
                packageContentUriBuilder: _packageContentUriBuilder,
                testOutputHelper: _testOutputHelper);

            await db2catalogTestJob.RunOnceAsync(CancellationToken.None);

            // Assert
            Assert.Equal(7, catalogStorage.Content.Count);

            // Ensure catalog has index.json
            var catalogIndex = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("index.json"));
            Assert.NotNull(catalogIndex.Key);
            Assert.Contains("\"nuget:lastCreated\":\"2015-01-01T00:00:00Z\"", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastDeleted\":\"2015-01-01T01:01:01.0748028Z\"", catalogIndex.Value.GetContentString());
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

            var package5 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage.1.0.0.json"));
            Assert.NotNull(package5.Key);
            Assert.Contains("\"PackageDelete\",", package5.Value.GetContentString());
            Assert.Contains("\"id\": \"OtherPackage\",", package5.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package5.Value.GetContentString());
        }

        [Fact]
        public async Task AppendsDeleteToExistingCatalog()
        {
            // Arrange
            const int top = 20;
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackages();
            var auditingStorage = new MemoryStorage();

            var firstAuditingRecord = new Uri(auditingStorage.BaseAddress, $"package/OtherPackage/1.0.0/{Guid.NewGuid()}-deleted.audit.v1.json");
            var secondAuditingRecord = new Uri(auditingStorage.BaseAddress, $"package/AnotherPackage/1.0.0/{Guid.NewGuid()}-deleted.audit.v1.json");

            auditingStorage.Content.TryAdd(firstAuditingRecord, new StringStorageContent(TestCatalogEntries.DeleteAuditRecordForOtherPackage100));
            auditingStorage.Content.TryAdd(secondAuditingRecord, new StringStorageContent(TestCatalogEntries.DeleteAuditRecordForOtherPackage100.Replace("OtherPackage", "AnotherPackage")));
            auditingStorage.ListMock.TryAdd(secondAuditingRecord, new StorageListItem(secondAuditingRecord, new DateTime(2010, 1, 1)));

            var mockServer = new MockServerHttpClientHandler();
            RegisterPackageContentUri(mockServer, "ListedPackage", "1.0.0", "Packages\\ListedPackage.1.0.0.zip");
            RegisterPackageContentUri(mockServer, "ListedPackage", "1.0.1", "Packages\\ListedPackage.1.0.1.zip");
            RegisterPackageContentUri(mockServer, "UnlistedPackage", "1.0.0", "Packages\\UnlistedPackage.1.0.0.zip");

            var cursor1 = new DateTime(1, 1, 1).ForceUtc();
            var cursor2 = new DateTime(2015, 1, 1).ForceUtc();

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>();

            galleryDatabaseMock
                .Setup(m => m.GetPackagesCreatedSince(cursor1, top))
                .ReturnsAsync(GetCreatedPackages);

            galleryDatabaseMock
                .Setup(m => m.GetPackagesCreatedSince(cursor2, top))
                .ReturnsAsync(GetEmptyPackages);

            galleryDatabaseMock
                .Setup(m => m.GetPackagesEditedSince(cursor1, top))
                .ReturnsAsync(GetEditedPackages);

            galleryDatabaseMock
                .Setup(m => m.GetPackagesEditedSince(cursor2, top))
                .ReturnsAsync(GetEmptyPackages);

            // Act
            var db2catalogTestJob = new TestableDb2CatalogJob(
                mockServer,
                catalogStorage,
                auditingStorage,
                skipCreatedPackagesProcessing: false,
                startDate: null,
                timeout: TimeSpan.FromMinutes(5),
                top: top,
                verbose: true,
                galleryDatabaseMock: galleryDatabaseMock,
                packageContentUriBuilder: _packageContentUriBuilder,
                testOutputHelper: _testOutputHelper);

            await db2catalogTestJob.RunOnceAsync(CancellationToken.None);

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
            var top = 20;
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackages();
            var auditingStorage = new MemoryStorage();
            auditingStorage.Content.TryAdd(
                new Uri(auditingStorage.BaseAddress, "package/OtherPackage/1.0.0/2015-01-01T00:01:01-deleted.audit.v1.json"),
                new StringStorageContent(TestCatalogEntries.DeleteAuditRecordForOtherPackage100));

            var mockServer = new MockServerHttpClientHandler();
            RegisterPackageContentUri(mockServer, "ListedPackage", "1.0.0", "Packages\\ListedPackage.1.0.0.zip");
            RegisterPackageContentUri(mockServer, "ListedPackage", "1.0.1", "Packages\\ListedPackage.1.0.1.zip");
            RegisterPackageContentUri(mockServer, "UnlistedPackage", "1.0.0", "Packages\\UnlistedPackage.1.0.0.zip");
            RegisterPackageContentUri(mockServer, "OtherPackage", "1.0.0", "Packages\\OtherPackage.1.0.0.zip");

            var cursor1 = new DateTime(1, 1, 1).ForceUtc();
            var cursor2 = new DateTime(2015, 1, 1).ForceUtc();
            var cursor3 = new DateTime(2015, 1, 1, 1, 1, 3).ForceUtc();

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>();

            galleryDatabaseMock
                .Setup(m => m.GetPackagesCreatedSince(cursor1, top))
                .ReturnsAsync(GetCreatedPackages);

            galleryDatabaseMock
                .Setup(m => m.GetPackagesCreatedSince(cursor2, top))
                .ReturnsAsync(GetCreatedPackagesSecondRequest);

            galleryDatabaseMock
                .Setup(m => m.GetPackagesCreatedSince(cursor3, top))
                .ReturnsAsync(GetEmptyPackages);

            galleryDatabaseMock
                .Setup(m => m.GetPackagesEditedSince(cursor1, top))
                .ReturnsAsync(GetEditedPackages);

            galleryDatabaseMock
                .Setup(m => m.GetPackagesEditedSince(cursor2, top))
                .ReturnsAsync(GetEmptyPackages);

            // Act
            var db2catalogTestJob = new TestableDb2CatalogJob(
                mockServer,
                catalogStorage,
                auditingStorage,
                skipCreatedPackagesProcessing: false,
                startDate: null,
                timeout: TimeSpan.FromMinutes(5),
                top: top,
                verbose: true,
                galleryDatabaseMock: galleryDatabaseMock,
                packageContentUriBuilder: _packageContentUriBuilder,
                testOutputHelper: _testOutputHelper);

            await db2catalogTestJob.RunOnceAsync(CancellationToken.None);

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
            const int top = 20;
            var mockServer = new MockServerHttpClientHandler();
            var auditingStorage = Mock.Of<IStorage>();
            var catalogStorage = new Mock<IStorage>(MockBehavior.Strict);
            var datetime = DateTime.MinValue.ToString("O") + "Z";
            var json = $"{{\"nuget:lastCreated\":\"{datetime}\"," +
                $"\"nuget:lastDeleted\":\"{datetime}\"," +
                $"\"nuget:lastEdited\":\"{datetime}\"}}";

            catalogStorage.Setup(x => x.ResolveUri(It.IsNotNull<string>()))
                .Returns(_baseUri);
            catalogStorage.Setup(x => x.LoadStringAsync(It.IsNotNull<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(json);

            var cursor1 = new DateTime(1, 1, 1).ForceUtc();

            var galleryDatabaseMock = new Mock<IGalleryDatabaseQueryService>();

            galleryDatabaseMock
                .Setup(m => m.GetPackagesCreatedSince(cursor1, top))
                .ReturnsAsync(GetEmptyPackages);

            galleryDatabaseMock
                .Setup(m => m.GetPackagesEditedSince(cursor1, top))
                .ReturnsAsync(GetEmptyPackages);

            var db2catalogTestJob = new TestableDb2CatalogJob(
                mockServer,
                catalogStorage.Object,
                auditingStorage,
                skipCreatedPackagesProcessing: false,
                startDate: null,
                timeout: TimeSpan.FromMinutes(5),
                top: top,
                verbose: true,
                galleryDatabaseMock: galleryDatabaseMock,
                packageContentUriBuilder: _packageContentUriBuilder,
                testOutputHelper: _testOutputHelper);

            await db2catalogTestJob.RunOnceAsync(CancellationToken.None);

            catalogStorage.Verify(x => x.ResolveUri(It.IsNotNull<string>()), Times.AtLeastOnce());
            catalogStorage.Verify(x => x.LoadStringAsync(It.IsNotNull<Uri>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        private SortedList<DateTime, IList<FeedPackageDetails>> GetCreatedPackages()
        {
            var packages = new List<FeedPackageDetails>
            {
                new FeedPackageDetails(
                    contentUri: _packageContentUriBuilder.Build("ListedPackage", "1.0.0"),
                    createdDate: new DateTime(2015, 1, 1).ForceUtc(),
                    lastEditedDate: DateTime.MinValue,
                    publishedDate: new DateTime(2015, 1, 1).ForceUtc(),
                    packageId: "ListedPackage",
                    packageNormalizedVersion: "1.0.0",
                    packageFullVersion: "1.0.0.0"),
                new FeedPackageDetails(
                    contentUri: _packageContentUriBuilder.Build("UnlistedPackage", "1.0.0"),
                    createdDate: new DateTime(2015, 1, 1).ForceUtc(),
                    lastEditedDate: DateTime.MinValue,
                    publishedDate: Constants.UnpublishedDate,
                    packageId: "UnlistedPackage",
                    packageNormalizedVersion: "1.0.0",
                    packageFullVersion: "1.0.0+metadata"),

                // The real SemVer2 version is embedded in the nupkg.
                // The below FeedPackageDetails entity expects normalized versions.
                new FeedPackageDetails(
                    contentUri: _packageContentUriBuilder.Build("TestPackage.SemVer2", "1.0.0-alpha.1"),
                    createdDate: new DateTime(2015, 1, 1).ForceUtc(),
                    lastEditedDate: DateTime.MinValue,
                    publishedDate: new DateTime(2015, 1, 1).ForceUtc(),
                    packageId: "TestPackage.SemVer2",
                    packageNormalizedVersion: "1.0.0-alpha.1",
                    packageFullVersion: "1.0.0-alpha.1")
            };

            return GalleryDatabaseQueryService.OrderPackagesByKeyDate(packages, p => p.CreatedDate);
        }

        private SortedList<DateTime, IList<FeedPackageDetails>> GetEditedPackages()
        {
            var packages = new List<FeedPackageDetails>
            {
                new FeedPackageDetails(
                    contentUri: _packageContentUriBuilder.Build("ListedPackage", "1.0.1"),
                    createdDate: new DateTime(2014, 1, 1).ForceUtc(),
                    lastEditedDate: new DateTime(2015, 1, 1).ForceUtc(),
                    publishedDate: new DateTime(2014, 1, 1).ForceUtc(),
                    packageId: "ListedPackage",
                    packageNormalizedVersion: "1.0.1",
                    packageFullVersion: "1.0.1")
            };

            return GalleryDatabaseQueryService.OrderPackagesByKeyDate(packages, p => p.LastEditedDate);
        }

        private SortedList<DateTime, IList<FeedPackageDetails>> GetEmptyPackages()
        {
            return GalleryDatabaseQueryService.OrderPackagesByKeyDate(new List<FeedPackageDetails>(), p => p.CreatedDate);
        }

        private SortedList<DateTime, IList<FeedPackageDetails>> GetCreatedPackagesSecondRequest()
        {
            var packages = new List<FeedPackageDetails>
            {
                new FeedPackageDetails(
                    contentUri: _packageContentUriBuilder.Build("OtherPackage", "1.0.0"),
                    createdDate: new DateTime(2015, 1, 1, 1, 1, 3).ForceUtc(),
                    lastEditedDate: new DateTime(2015, 1, 1, 1, 1, 3).ForceUtc(),
                    publishedDate: new DateTime(2015, 1, 1, 1, 1, 3).ForceUtc(),
                    packageId: "OtherPackage",
                    packageNormalizedVersion: "1.0.0",
                    packageFullVersion: "1.0.0")
            };

            return GalleryDatabaseQueryService.OrderPackagesByKeyDate(packages, p => p.CreatedDate);
        }

        private void InitializeTest(
            bool skipCreatedPackagesProcessing,
            int top,
            Mock<IGalleryDatabaseQueryService> galleryDatabaseMock)
        {
            _skipCreatedPackagesProcessing = skipCreatedPackagesProcessing;

            _job = new TestableDb2CatalogJob(
                _server,
                _catalogStorage,
                _auditingStorage,
                skipCreatedPackagesProcessing,
                startDate: null,
                timeout: TimeSpan.FromMinutes(5),
                top: top,
                verbose: true,
                galleryDatabaseMock: galleryDatabaseMock,
                packageContentUriBuilder: _packageContentUriBuilder,
                testOutputHelper: _testOutputHelper);
        }

        private PackageCreationOrEdit CreatePackageCreationOrEdit(DateTime? createdDate = null)
        {
            var package = TestPackage.Create(_random);
            var isListed = Convert.ToBoolean(_random.Next(minValue: 0, maxValue: 2));
            var created = createdDate ?? DateTimeOffset.UtcNow;

            // Avoid hitting this bug accidentally:  https://github.com/NuGet/NuGetGallery/issues/2841
            if (!createdDate.HasValue && created == _packageOperations.OfType<PackageCreationOrEdit>().LastOrDefault()?.FeedPackageDetails.CreatedDate)
            {
                created = created.AddMilliseconds(1);
            }

            var normalizedVersion = package.Version.ToNormalizedString();
            var fullVersion = package.Version.ToFullString();
            var feedPackageDetails = new FeedPackageDetails(
                contentUri: _packageContentUriBuilder.Build(package.Id, normalizedVersion),
                createdDate: created.UtcDateTime,
                lastEditedDate: DateTime.MinValue,
                publishedDate: isListed ? created.UtcDateTime : Constants.UnpublishedDate,
                packageId: package.Id,
                packageNormalizedVersion: normalizedVersion,
                packageFullVersion: fullVersion,
                licenseNames: null,
                licenseReportUrl: null,
                deprecationInfo: null,
                requiresLicenseAcceptance: false);

            return new PackageCreationOrEdit(package, feedPackageDetails);
        }

        private PackageCreationOrEdit AddCreatedPackageToFeed(DateTime? createdDate = null)
        {
            var operation = CreatePackageCreationOrEdit(createdDate);

            _packageOperations.Add(operation);

            return operation;
        }

        private PackageCreationOrEdit AddEditedPackageToFeed(PackageCreationOrEdit entry, DateTime? lastEditedDate = null)
        {
            var editedPackage = AddPackageEntry(entry.Package);
            var edited = lastEditedDate ?? DateTime.UtcNow;

            // Avoid hitting this bug accidentally:  https://github.com/NuGet/NuGetGallery/issues/2841
            if (!lastEditedDate.HasValue && edited == _packageOperations.OfType<PackageCreationOrEdit>().LastOrDefault(e => e.FeedPackageDetails.LastEditedDate != DateTime.MinValue)?.FeedPackageDetails.LastEditedDate)
            {
                edited = edited.AddMilliseconds(1);
            }

            var feedPackageDetails = new FeedPackageDetails(
                contentUri: entry.FeedPackageDetails.ContentUri,
                createdDate: entry.FeedPackageDetails.CreatedDate,
                lastEditedDate: edited,
                publishedDate: entry.FeedPackageDetails.PublishedDate,
                packageId: entry.FeedPackageDetails.PackageId,
                packageNormalizedVersion: entry.FeedPackageDetails.PackageNormalizedVersion,
                packageFullVersion: entry.FeedPackageDetails.PackageFullVersion,
                licenseNames: entry.FeedPackageDetails.LicenseNames,
                licenseReportUrl: entry.FeedPackageDetails.LicenseReportUrl,
                deprecationInfo: entry.FeedPackageDetails.DeprecationInfo,
                requiresLicenseAcceptance: entry.FeedPackageDetails.RequiresLicenseAcceptance);

            var operation = new PackageCreationOrEdit(editedPackage, feedPackageDetails);

            _packageOperations.Add(operation);

            return operation;
        }

        private PackageDeletion AddDeletedPackage(
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
                        new JProperty("timestampUtc", deletionTime.Value.ToString("O")))));

            var packageId = operation.Package.Id.ToLowerInvariant();
            var packageVersion = operation.Package.Version.ToNormalizedString().ToLowerInvariant();
            var fileNamePostfix = isSoftDelete ? "softdelete.audit.v1.json" : "Deleted.audit.v1.json";
            var uri = new Uri($"https://nuget.test/auditing/{packageId}/{packageVersion}-{fileNamePostfix}");

            _auditingStorage.Content.TryAdd(uri, new JTokenStorageContent(auditRecord));

            var deletion = new PackageDeletion(uri, auditRecord, deletionTime.Value);

            _packageOperations.Add(deletion);

            return deletion;
        }

        private async Task RunInternalAndVerifyAsync(
            Mock<IGalleryDatabaseQueryService> galleryDatabaseHelperMock,
            int top,
            DateTime? expectedLastCreated = null,
            DateTime? expectedLastDeleted = null,
            DateTime? expectedLastEdited = null,
            PackageOperation skippedPackage = null)
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

            _server.Actions.Clear();

            PublishCreatedPackages(galleryDatabaseHelperMock, top);
            PublishEditedPackages(galleryDatabaseHelperMock, top);

            await _job.RunOnceAsync(CancellationToken.None);

            VerifyCatalog(expectedLastCreated, expectedLastDeleted, expectedLastEdited, skippedPackage);
        }

        private void RegisterPackageContentUri(TestPackage package)
        {
            _server.SetAction(
                _packageContentUriBuilder.Build(package.Id, package.Version.ToNormalizedString()).AbsolutePath,
                request => GetStreamContentActionAsync(package.Stream));
        }

        private void RegisterPackageContentUri(MockServerHttpClientHandler server, string packageId, string packageVersion, string filePath)
        {
            server.SetAction(
                _packageContentUriBuilder.Build(packageId, packageVersion).AbsolutePath,
                request => GetStreamContentActionAsync(filePath));
        }

        private void VerifyCatalog(
            DateTime? expectedLastCreated,
            DateTime? expectedLastDeleted,
            DateTime? expectedLastEdited,
            PackageOperation skippedOperation)
        {
            List<PackageOperation> verifiablePackageOperations;

            if (_skipCreatedPackagesProcessing)
            {
                verifiablePackageOperations = _packageOperations
                    .Where(entry =>
                        entry != skippedOperation
                        && (!(entry is PackageCreationOrEdit) || ((PackageCreationOrEdit)entry).FeedPackageDetails.LastEditedDate != DateTime.MinValue))
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
                Assert.Empty(_catalogStorage.Content);

                return;
            }

            bool isEmptyCatalogBatch = _lastFeedEntriesCount == verifiablePackageOperations.Count;

            if (!isEmptyCatalogBatch)
            {
                _lastFeedEntriesCount = verifiablePackageOperations.Count;
            }

            var expectedCatalogEntryCount = verifiablePackageOperations.Count
                + 1  // index.json
                + 1; // page0.json
            Assert.Equal(expectedCatalogEntryCount, _catalogStorage.Content.Count);

            Assert.True(expectedLastCreated.HasValue);
            Assert.True(expectedLastDeleted.HasValue);
            Assert.True(expectedLastEdited.HasValue);

            var indexUri = new Uri(_baseUri, "index.json");
            var pageUri = new Uri(_baseUri, "page0.json");

            string commitId;
            string commitTimeStamp;

            VerifyCatalogIndex(
                verifiablePackageOperations,
                indexUri,
                pageUri,
                expectedLastCreated.Value,
                expectedLastDeleted.Value,
                expectedLastEdited.Value,
                out commitId,
                out commitTimeStamp);

            Assert.True(
                DateTime.TryParseExact(
                    commitTimeStamp,
                    CatalogConstants.CommitTimeStampFormat,
                    DateTimeFormatInfo.CurrentInfo,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var commitTimeStampDateTime));

            VerifyCatalogPage(verifiablePackageOperations, indexUri, pageUri, commitId, commitTimeStamp);
            VerifyCatalogPackageItems(verifiablePackageOperations);

            Assert.True(verifiablePackageOperations.All(packageOperation => !string.IsNullOrEmpty(packageOperation.CommitId)));
        }

        private void VerifyCatalogIndex(
            List<PackageOperation> packageOperations,
            Uri indexUri,
            Uri pageUri,
            DateTime expectedLastCreated,
            DateTime expectedLastDeleted,
            DateTime expectedLastEdited,
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

            Assert.Equal(
                expectedLastCreated.ToString("O"),
                DateTime.Parse(index[CatalogConstants.NuGetLastCreated].Value<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToString("O"));
            Assert.Equal(
                expectedLastDeleted.ToString("O"),
                DateTime.Parse(index[CatalogConstants.NuGetLastDeleted].Value<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToString("O"));
            Assert.Equal(
                expectedLastEdited.ToString("O"),
                DateTime.Parse(index[CatalogConstants.NuGetLastEdited].Value<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToString("O"));

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
                    CatalogConstants.CommitTimeStampFormat,
                    DateTimeFormatInfo.CurrentInfo,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

                expectedItem.CommitTimeStampDateTime = expectedTimestamp;

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

        private void VerifyCatalogPackageItems(List<PackageOperation> packageOperations)
        {
            PackageOperation previousPackageOperation = null;

            foreach (var packageOperation in packageOperations)
            {
                if (previousPackageOperation != null)
                {
                    Assert.True(packageOperation.CommitTimeStampDateTime >= previousPackageOperation.CommitTimeStampDateTime);
                }

                Uri packageDetailsUri = GetPackageDetailsUri(packageOperation.CommitTimeStampDateTime, packageOperation);

                Assert.True(_catalogStorage.Content.TryGetValue(packageDetailsUri, out var storage));
                Assert.IsAssignableFrom<StringStorageContent>(storage);

                var packageDetails = JObject.Parse(((StringStorageContent)storage).Content);
                Assert.NotNull(packageDetails);

                packageDetails = ReadJsonWithoutDateTimeHandling(packageDetails);

                if (packageOperation is PackageCreationOrEdit)
                {
                    VerifyCatalogPackageDetails((PackageCreationOrEdit)packageOperation, packageDetailsUri, packageDetails);
                }
                else
                {
                    VerifyCatalogPackageDelete((PackageDeletion)packageOperation, packageDetailsUri, packageDetails);
                }

                previousPackageOperation = packageOperation;
            }
        }

        private void VerifyCatalogPackageDetails(
            PackageCreationOrEdit packageOperation,
            Uri packageDetailsUri,
            JObject packageDetails)
        {
            var properties = packageDetails.Properties();

            Assert.Equal(19, properties.Count());
            Assert.Equal(packageDetailsUri.AbsoluteUri, packageDetails[CatalogConstants.IdKeyword].Value<string>());
            Assert.Equal(
                new JArray(CatalogConstants.PackageDetails, CatalogConstants.CatalogPermalink),
                packageDetails[CatalogConstants.TypeKeyword]);
            Assert.Equal(packageOperation.Package.Author, packageDetails[CatalogConstants.Authors].Value<string>());
            Assert.Equal(packageOperation.CommitId, packageDetails[CatalogConstants.CatalogCommitId].Value<string>());
            Assert.Equal(packageOperation.CommitTimeStamp, packageDetails[CatalogConstants.CatalogCommitTimeStamp].Value<string>());
            Assert.Equal(
                packageOperation.FeedPackageDetails.CreatedDate.ToString("O"),
                DateTime.Parse(packageDetails[CatalogConstants.Created].Value<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToString("O"));
            Assert.Equal(packageOperation.Package.Description, packageDetails[CatalogConstants.Description].Value<string>());
            Assert.Equal(packageOperation.FeedPackageDetails.PackageId, packageDetails[CatalogConstants.Id].Value<string>());
            Assert.False(packageDetails[CatalogConstants.IsPrerelease].Value<bool>());
            Assert.Equal(
                packageOperation.FeedPackageDetails.LastEditedDate.ToString("O"),
                DateTime.Parse(packageDetails[CatalogConstants.LastEdited].Value<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToString("O"));
            Assert.Equal(packageOperation.FeedPackageDetails.PublishedDate != Constants.UnpublishedDate, packageDetails[CatalogConstants.Listed].Value<bool>());
            Assert.Equal(GetPackageHash(packageOperation.Package), packageDetails[CatalogConstants.PackageHash].Value<string>());
            Assert.Equal(Constants.Sha512, packageDetails[CatalogConstants.PackageHashAlgorithm].Value<string>());
            Assert.Equal(packageOperation.Package.Stream.Length, packageDetails[CatalogConstants.PackageSize].Value<int>());
            Assert.Equal(
                packageOperation.FeedPackageDetails.PublishedDate.ToString("O"),
                DateTime.Parse(packageDetails[CatalogConstants.Published].Value<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToString("O"));
            Assert.Equal(packageOperation.Package.Version.ToFullString(), packageDetails[CatalogConstants.VerbatimVersion].Value<string>());
            Assert.Equal(packageOperation.Package.Version.ToNormalizedString(), packageDetails[CatalogConstants.Version].Value<string>());

            var expectedPackageEntries = GetPackageEntries(packageOperation.Package)
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
                new JProperty(CatalogConstants.PackageTypes,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.PackageTypeUncapitalized),
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
                    new JObject(new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
                new JProperty(CatalogConstants.Reasons,
                    new JObject(
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))));

            Assert.Equal(expectedContext.ToString(), packageDetails[CatalogConstants.ContextKeyword].ToString());
        }

        private void VerifyCatalogPackageDelete(
            PackageDeletion packageOperation,
            Uri packageDeleteUri,
            JObject packageDelete)
        {
            var properties = packageDelete.Properties();

            Assert.Equal(9, properties.Count());
            Assert.Equal(packageDeleteUri.AbsoluteUri, packageDelete[CatalogConstants.IdKeyword].Value<string>());
            Assert.Equal(
                new JArray(CatalogConstants.PackageDelete, CatalogConstants.CatalogPermalink),
                packageDelete[CatalogConstants.TypeKeyword]);
            Assert.Equal(packageOperation.CommitId, packageDelete[CatalogConstants.CatalogCommitId].Value<string>());
            Assert.Equal(packageOperation.CommitTimeStamp, packageDelete[CatalogConstants.CatalogCommitTimeStamp].Value<string>());
            Assert.Equal(packageOperation.PackageIdentity.Id, packageDelete[CatalogConstants.Id].Value<string>());
            Assert.Equal(packageOperation.PackageIdentity.Id, packageDelete[CatalogConstants.OriginalId].Value<string>());
            Assert.Equal(
                packageOperation.Published.ToString("O"),
                DateTime.Parse(packageDelete[CatalogConstants.Published].Value<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToString("O"));
            Assert.Equal(packageOperation.PackageIdentity.Version.ToNormalizedString(), packageDelete[CatalogConstants.Version].Value<string>());

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

            return new Uri($"{_baseUri.AbsoluteUri}data/{catalogTimeStamp.ToString(CatalogConstants.UrlTimeStampFormat)}/{packageId}.{packageVersion}.json");
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

        private void PublishCreatedPackages(Mock<IGalleryDatabaseQueryService> galleryDatabaseHelperMock, int top)
        {
            var packageOperations = _packageOperations.OfType<PackageCreationOrEdit>();

            foreach (var packageOperation in packageOperations)
            {
                var createdPackages = new SortedList<DateTime, IList<FeedPackageDetails>>();
                createdPackages.Add(packageOperation.FeedPackageDetails.CreatedDate, new List<FeedPackageDetails> { packageOperation.FeedPackageDetails });

                galleryDatabaseHelperMock
                    .Setup(m => m.GetPackagesCreatedSince(_feedLastCreated, top))
                    .ReturnsAsync(createdPackages);

                RegisterPackageContentUri(packageOperation.Package);

                _feedLastCreated = packageOperation.FeedPackageDetails.CreatedDate;
            }

            galleryDatabaseHelperMock
                    .Setup(m => m.GetPackagesCreatedSince(_feedLastCreated, top))
                    .ReturnsAsync(new SortedList<DateTime, IList<FeedPackageDetails>>());
        }

        private void PublishEditedPackages(Mock<IGalleryDatabaseQueryService> galleryDatabaseHelperMock, int top)
        {
            var packageOperations = _packageOperations.OfType<PackageCreationOrEdit>()
                .Where(entry => entry.FeedPackageDetails.LastEditedDate != DateTime.MinValue);

            foreach (var packageOperation in packageOperations)
            {
                var editedPackages = new SortedList<DateTime, IList<FeedPackageDetails>>();
                editedPackages.Add(packageOperation.FeedPackageDetails.LastEditedDate, new List<FeedPackageDetails> { packageOperation.FeedPackageDetails });

                galleryDatabaseHelperMock
                    .Setup(m => m.GetPackagesEditedSince(_feedLastEdited, top))
                    .ReturnsAsync(editedPackages);

                RegisterPackageContentUri(packageOperation.Package);

                _feedLastEdited = packageOperation.FeedPackageDetails.LastEditedDate;
            }

            galleryDatabaseHelperMock
                    .Setup(m => m.GetPackagesEditedSince(_feedLastEdited, top))
                    .ReturnsAsync(new SortedList<DateTime, IList<FeedPackageDetails>>());
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

            return new TestPackage(package.Id, package.Version, package.Author, package.Description, package.Nuspec, stream);
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

        private static Task<HttpResponseMessage> GetStreamContentActionAsync(Stream stream)
        {
            // Ensure we reset the stream position to 0 (a package edit might have happened).
            stream.Position = 0;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            };

            response.Content.Headers.Add("Content-Length", stream.Length.ToString());

            return Task.FromResult(response);
        }

        private static Task<HttpResponseMessage> GetStreamContentActionAsync(string filePath)
        {
            return GetStreamContentActionAsync(File.OpenRead(filePath));
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
            internal DateTime CommitTimeStampDateTime { get; set; }
            internal abstract PackageIdentity PackageIdentity { get; }
        }

        private sealed class PackageCreationOrEdit : PackageOperation
        {
            internal FeedPackageDetails FeedPackageDetails { get; }
            internal TestPackage Package { get; }
            internal override PackageIdentity PackageIdentity { get; }

            internal PackageCreationOrEdit(TestPackage package, FeedPackageDetails feedPackageDetails)
            {
                Package = package;
                FeedPackageDetails = feedPackageDetails;
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
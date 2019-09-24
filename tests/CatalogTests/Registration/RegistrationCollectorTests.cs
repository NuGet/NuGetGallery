// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CatalogTests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NgTests;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;
using Xunit;

namespace CatalogTests.Registration
{
    public class RegistrationCollectorTests : RegistrationTestBase
    {
        private readonly static Uri _baseUri = new Uri("https://nuget.test");
        private readonly static JObject _contextKeyword = new JObject(
            new JProperty(CatalogConstants.VocabKeyword, CatalogConstants.NuGetSchemaUri),
            new JProperty(CatalogConstants.NuGet, CatalogConstants.NuGetSchemaUri),
            new JProperty(CatalogConstants.Items,
                new JObject(
                    new JProperty(CatalogConstants.IdKeyword, CatalogConstants.Item),
                    new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
            new JProperty(CatalogConstants.Parent,
                new JObject(
                    new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.IdKeyword))),
            new JProperty(CatalogConstants.CommitTimeStamp,
                new JObject(
                    new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
            new JProperty(CatalogConstants.NuGetLastCreated,
                new JObject(
                    new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
            new JProperty(CatalogConstants.NuGetLastEdited,
                new JObject(
                    new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
            new JProperty(CatalogConstants.NuGetLastDeleted,
                new JObject(
                    new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))));

        private MemoryStorage _legacyStorage;
        private TestStorageFactory _legacyStorageFactory;
        private MockServerHttpClientHandler _mockServer;
        private RegistrationCollector _target;
        private MemoryStorage _semVer2Storage;
        private TestStorageFactory _semVer2StorageFactory;

        private void SharedInit(bool useLegacy, bool useSemVer2, Uri baseUri = null, Uri indexUri = null, Uri contentBaseUri = null, Uri galleryBaseUri = null, bool forceFlatContainerIcons = false)
        {
            if (useLegacy)
            {
                _legacyStorage = new MemoryStorage(baseUri ?? new Uri("http://tempuri.org"));
                _legacyStorageFactory = new TestStorageFactory(name => _legacyStorage.WithName(name));
            }

            if (useSemVer2)
            {
                _semVer2Storage = new MemoryStorage(baseUri ?? new Uri("http://tempuri.org"));
                _semVer2StorageFactory = new TestStorageFactory(name => _semVer2Storage.WithName(name));
            }

            _mockServer = new MockServerHttpClientHandler();
            _mockServer.SetAction("/", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            _target = new RegistrationCollector(
                indexUri ?? new Uri("http://tempuri.org/index.json"),
                _legacyStorageFactory,
                _semVer2StorageFactory,
                contentBaseUri ?? new Uri("http://tempuri.org/packages"),
                galleryBaseUri ?? new Uri("http://tempuri.org/gallery"),
                forceFlatContainerIcons,
                Mock.Of<ITelemetryService>(),
                Mock.Of<ILogger>(),
                handlerFunc: () => _mockServer,
                httpRetryStrategy: new NoRetryStrategy());

            RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Constructor_WhenMaxConcurrentBatchesIsNotPositive_Throws(int maxConcurrentBatches)
        {
            var storageFactory = new TestStorageFactory();

            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new RegistrationCollector(
                    _baseUri,
                    storageFactory,
                    storageFactory,
                    _baseUri,
                    _baseUri,
                    false,
                    Mock.Of<ITelemetryService>(),
                    Mock.Of<ILogger>(),
                    maxConcurrentBatches: maxConcurrentBatches));

            Assert.Equal("maxConcurrentBatches", exception.ParamName);
            Assert.Equal(maxConcurrentBatches, exception.ActualValue);
        }

        [Theory]
        [InlineData("/data/2015.10.12.10.08.54/unlistedpackage.1.0.0.json", null)]
        [InlineData("/data/2015.10.12.10.08.55/listedpackage.1.0.1.json", "2015-10-12T10:08:54.1506742Z")]
        [InlineData("/data/2015.10.12.10.08.55/anotherpackage.1.0.0.json", "2015-10-12T10:08:54.1506742Z")]
        public async Task DoesNotSkipPackagesWhenExceptionOccurs(string catalogUri, string expectedCursorBeforeRetry)
        {
            // Arrange
            SharedInit(useLegacy: true, useSemVer2: false);

            var catalogStorage = Catalogs.CreateTestCatalogWithCommitThenTwoPackageCommit();
            await _mockServer.AddStorageAsync(catalogStorage);

            // Make the first request for a catalog leaf node fail. This will cause the registration collector
            // to fail the first time but pass the second time.
            FailFirstRequest(catalogUri);

            expectedCursorBeforeRetry = expectedCursorBeforeRetry ?? MemoryCursor.MinValue.ToString("O");

            ReadWriteCursor front = new DurableCursor(
                _legacyStorage.ResolveUri("cursor.json"),
                _legacyStorage,
                MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await Assert.ThrowsAsync<BatchProcessingException>(
                () => _target.RunAsync(front, back, CancellationToken.None));
            var cursorBeforeRetry = front.Value;
            await _target.RunAsync(front, back, CancellationToken.None);
            var cursorAfterRetry = front.Value;

            // Assert
            var unlistedPackage100 = _legacyStorage
                .Content
                .FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0.json"));
            Assert.NotNull(unlistedPackage100.Key);

            var listedPackage101 = _legacyStorage
                .Content
                .FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1.json"));
            Assert.NotNull(listedPackage101.Key);

            var anotherPackage100 = _legacyStorage
                .Content
                .FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/anotherpackage/1.0.0.json"));
            Assert.NotNull(anotherPackage100.Key);

            Assert.Equal(MemoryCursor.MinValue, cursorBeforeRetry);
            Assert.Equal(DateTime.Parse("2015-10-12T10:08:55.3335317Z").ToUniversalTime(), cursorAfterRetry);
        }

        private void FailFirstRequest(string relativeUri)
        {
            var originalAction = _mockServer.Actions[relativeUri];
            var hasFailed = false;
            Func<HttpRequestMessage, Task<HttpResponseMessage>> failFirst = request =>
            {
                if (!hasFailed)
                {
                    hasFailed = true;
                    throw new HttpRequestException("Simulated HTTP failure.");
                }

                return originalAction(request);
            };
            _mockServer.SetAction(relativeUri, failFirst);
        }

        [Fact]
        public async Task CreatesRegistrationsAndRespectsDeletes()
        {
            // Arrange
            SharedInit(useLegacy: true, useSemVer2: false);

            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            await _mockServer.AddStorageAsync(catalogStorage);

            ReadWriteCursor front = new DurableCursor(_legacyStorage.ResolveUri("cursor.json"), _legacyStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await _target.RunAsync(front, back, CancellationToken.None);

            // Assert
            Assert.Equal(6, _legacyStorage.Content.Count);

            // Ensure storage has cursor.json
            var cursorJson = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(cursorJson.Key);

            // Check package entries - ListedPackage
            var package1Index = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/index.json"));
            Assert.NotNull(package1Index.Key);
            Assert.Contains("\"listed\":true,", package1Index.Value.GetContentString());
            Assert.Contains("\"catalog:CatalogRoot\"", package1Index.Value.GetContentString());
            Assert.Contains("\"PackageRegistration\"", package1Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2015.10.12.10.08.54/listedpackage.1.0.0.json\"", package1Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/packages/listedpackage.1.0.0.nupkg\"", package1Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2015.10.12.10.08.55/listedpackage.1.0.1.json\"", package1Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/packages/listedpackage.1.0.1.nupkg\"", package1Index.Value.GetContentString());
            Assert.Contains("\"lower\":\"1.0.0\",", package1Index.Value.GetContentString());
            Assert.Contains("\"upper\":\"1.0.1\"", package1Index.Value.GetContentString());

            var package1 = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.0.json"));
            Assert.Contains("\"listed\":true,", package1.Value.GetContentString());
            Assert.NotNull(package1.Key);

            var package2 = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1.json"));
            Assert.Contains("\"listed\":true,", package2.Value.GetContentString());
            Assert.NotNull(package2.Key);

            // Check package entries - UnlistedPackage
            var package2Index = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/index.json"));
            Assert.NotNull(package2Index.Key);
            Assert.Contains("\"listed\":false,", package2Index.Value.GetContentString());
            Assert.Contains("\"catalog:CatalogRoot\"", package2Index.Value.GetContentString());
            Assert.Contains("\"PackageRegistration\"", package2Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2015.10.12.10.08.54/unlistedpackage.1.0.0.json\"", package2Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/packages/unlistedpackage.1.0.0.nupkg\"", package2Index.Value.GetContentString());
            Assert.Contains("\"lower\":\"1.0.0\",", package2Index.Value.GetContentString());
            Assert.Contains("\"upper\":\"1.0.0\"", package2Index.Value.GetContentString());

            var package3 = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0.json"));
            Assert.Contains("\"listed\":false,", package3.Value.GetContentString());
            Assert.NotNull(package3.Key);

            // Ensure storage does not have the deleted "OtherPackage"
            var otherPackageIndex = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage/index.json"));
            Assert.Null(otherPackageIndex.Key);
        }

        [Theory]
        [MemberData(nameof(PageContent))]
        public async Task WhenPackageHasMultipleCommitsRespectsOrder(string pageContent)
        {
            // Arrange
            SharedInit(useLegacy: true, useSemVer2: false);

            var catalogStorage = Catalogs.CreateTestCatalogWithThreeItemsForSamePackage(pageContent);
            await _mockServer.AddStorageAsync(catalogStorage);

            ReadWriteCursor front = new DurableCursor(_legacyStorage.ResolveUri("cursor.json"), _legacyStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await _target.RunAsync(front, back, CancellationToken.None);

            // Assert
            Assert.Equal(3, _legacyStorage.Content.Count);

            // Ensure storage has cursor.json
            var cursorJson = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(cursorJson.Key);

            // Check package entries - ListedPackage
            var myPackageIndexFile = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/mypackage/index.json"));
            Assert.NotNull(myPackageIndexFile.Key);
            Assert.Contains("\"catalog:CatalogRoot\"", myPackageIndexFile.Value.GetContentString());
            Assert.Contains("\"PackageRegistration\"", myPackageIndexFile.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2017.02.08.17.16.18/mypackage.3.0.0.json\"", myPackageIndexFile.Value.GetContentString());
            Assert.Contains("\"packageContent\":\"http://tempuri.org/packages/mypackage.3.0.0.nupkg\"", myPackageIndexFile.Value.GetContentString());
            Assert.Contains("\"lower\":\"3.0.0\",", myPackageIndexFile.Value.GetContentString());
            Assert.Contains("\"upper\":\"3.0.0\"", myPackageIndexFile.Value.GetContentString());

            var myPackageVersionFile = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/mypackage/3.0.0.json"));
            Assert.NotNull(myPackageVersionFile.Key);
            Assert.Contains("\"catalogEntry\":\"http://tempuri.org/data/2017.02.08.17.16.18/mypackage.3.0.0.json\"", myPackageVersionFile.Value.GetContentString());
            Assert.Contains("\"listed\":true", myPackageVersionFile.Value.GetContentString());
            Assert.Contains("\"packageContent\":\"http://tempuri.org/packages/mypackage.3.0.0.nupkg\"", myPackageIndexFile.Value.GetContentString());
        }

        public static IEnumerable<object[]> PageContent
        {
            get
            {
                // Reverse chronological order of commits in page
                yield return new object[]
                {
                    TestCatalogEntries.TestCatalogPageForMyPackage_Option1,
                };

                // Random chronoligical order of commits in page
                yield return new object[]
                {
                    TestCatalogEntries.TestCatalogPageForMyPackage_Option2
                };
            }
        }

        [Fact]
        public async Task CreatesRegistrationsWithSemVer2()
        {
            // Arrange
            SharedInit(useLegacy: true, useSemVer2: true);

            var catalogStorage = Catalogs.CreateTestCatalogWithSemVer2Package();
            await _mockServer.AddStorageAsync(catalogStorage);

            var front = new DurableCursor(_legacyStorage.ResolveUri("cursor.json"), _legacyStorage, MemoryCursor.MinValue);
            var back = MemoryCursor.CreateMax();

            // Act
            await _target.RunAsync(front, back, CancellationToken.None);

            // Assert
            // Verify the contents of the legacy (non-SemVer 2.0.0) storage
            Assert.Single(_legacyStorage.Content);

            var legacyCursorJson = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(legacyCursorJson.Key);

            // Verify the contents of the SemVer 2.0.0 storage
            Assert.Equal(2, _semVer2Storage.Content.Count);

            var semVer2CursorJson = _semVer2Storage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.Null(semVer2CursorJson.Key);

            var index = _semVer2Storage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/testpackage.semver2/index.json"));
            Assert.NotNull(index.Key);
            Assert.Contains("\"catalog:CatalogRoot\"", index.Value.GetContentString());
            Assert.Contains("\"PackageRegistration\"", index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2015.10.12.10.08.54/testpackage.semver2.1.0.0-alpha.1.json\"", index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/packages/testpackage.semver2.1.0.0-alpha.1.nupkg\"", index.Value.GetContentString());
            Assert.Contains("\"version\":\"1.0.0-alpha.1+githash\"", index.Value.GetContentString());
            Assert.Contains("1.0.0-alpha.1/1.0.0-alpha.1", index.Value.GetContentString());
            Assert.Contains("\"lower\":\"1.0.0-alpha.1\",", index.Value.GetContentString());
            Assert.Contains("\"upper\":\"1.0.0-alpha.1\"", index.Value.GetContentString());

            var package = _semVer2Storage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/testpackage.semver2/1.0.0-alpha.1.json"));
            Assert.NotNull(package.Key);
            Assert.Contains("\"Package\"", package.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2015.10.12.10.08.54/testpackage.semver2.1.0.0-alpha.1.json\"", package.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/packages/testpackage.semver2.1.0.0-alpha.1.nupkg\"", package.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/testpackage.semver2/index.json\"", package.Value.GetContentString());
        }

        [Fact]
        public async Task IgnoresSemVer2PackagesInLegacyStorageWhenSemVer2IsEnabled()
        {
            // Arrange
            SharedInit(useLegacy: true, useSemVer2: true);

            var catalogStorage = Catalogs.CreateTestCatalogWithSemVer2Package();
            await _mockServer.AddStorageAsync(catalogStorage);

            var front = new DurableCursor(_legacyStorage.ResolveUri("cursor.json"), _legacyStorage, MemoryCursor.MinValue);
            var back = MemoryCursor.CreateMax();

            // Act
            await _target.RunAsync(front, back, CancellationToken.None);

            // Assert
            var legacyCursor = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(legacyCursor.Key);
            var legacyIndex = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/testpackage.semver2/index.json"));
            Assert.Null(legacyIndex.Key);
            var legacyLeaf = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/testpackage.semver2/1.0.0-alpha.1.json"));
            Assert.Null(legacyLeaf.Key);
            Assert.Single(_legacyStorage.Content);

            var semVer2Cursor = _semVer2Storage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.Null(semVer2Cursor.Key);
            var semVer2Index = _semVer2Storage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/testpackage.semver2/index.json"));
            Assert.NotNull(semVer2Index.Key);
            var semVer2Leaf = _semVer2Storage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/testpackage.semver2/1.0.0-alpha.1.json"));
            Assert.NotNull(semVer2Leaf.Key);
            Assert.Equal(2, _semVer2Storage.Content.Count);
        }

        [Fact]
        public async Task PutsSemVer2PackagesInLegacyStorageWhenSemVer2IsDisabled()
        {
            // Arrange
            SharedInit(useLegacy: true, useSemVer2: false);

            var catalogStorage = Catalogs.CreateTestCatalogWithSemVer2Package();
            await _mockServer.AddStorageAsync(catalogStorage);

            var front = new DurableCursor(_legacyStorage.ResolveUri("cursor.json"), _legacyStorage, MemoryCursor.MinValue);
            var back = MemoryCursor.CreateMax();

            // Act
            await _target.RunAsync(front, back, CancellationToken.None);

            // Assert
            var legacyCursor = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(legacyCursor.Key);
            var legacyIndex = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/testpackage.semver2/index.json"));
            Assert.NotNull(legacyIndex.Key);
            var legacyLeaf = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/testpackage.semver2/1.0.0-alpha.1.json"));
            Assert.NotNull(legacyLeaf.Key);
            Assert.Equal(3, _legacyStorage.Content.Count);
        }

        [Fact]
        public async Task HandlesDeleteCatalogItemWithNonNormalizedVersion()
        {
            // Arrange
            SharedInit(useLegacy: true, useSemVer2: false);

            var catalogStorage = Catalogs.CreateTestCatalogWithNonNormalizedDelete();
            await _mockServer.AddStorageAsync(catalogStorage);

            ReadWriteCursor front = new DurableCursor(_legacyStorage.ResolveUri("cursor.json"), _legacyStorage, MemoryCursor.MinValue);

            // Act
            await _target.RunAsync(
                front,
                new MemoryCursor(DateTime.Parse("2015-10-12T10:08:54.1506742")),
                CancellationToken.None);
            var intermediateUris = _legacyStorage
                .Content
                .Select(pair => pair.Key.ToString())
                .OrderBy(uri => uri)
                .ToList();
            await _target.RunAsync(
                front,
                MemoryCursor.CreateMax(),
                CancellationToken.None);

            // Assert
            Assert.Equal(3, intermediateUris.Count);
            Assert.Equal("http://tempuri.org/cursor.json", intermediateUris[0]);
            Assert.Equal("http://tempuri.org/otherpackage/1.0.0.json", intermediateUris[1]);
            Assert.Equal("http://tempuri.org/otherpackage/index.json", intermediateUris[2]);

            // This should really be 1, but see:
            // https://github.com/NuGet/Engineering/issues/404
            var finalUris = _legacyStorage
                 .Content
                 .Select(pair => pair.Key.ToString())
                 .OrderBy(uri => uri)
                 .ToList();
            Assert.Equal(2, finalUris.Count);
            Assert.Equal("http://tempuri.org/cursor.json", finalUris[0]);
            Assert.Equal("http://tempuri.org/otherpackage/1.0.0.json", finalUris[1]);
        }

        [Fact]
        public async Task RunAsync_WhenPackageIdSpansCommitsAndVariesInCasing_BatchesAcrossCommitsByLowerCasePackageId()
        {
            var pageUri = new Uri(_baseUri, "v3-catalog0/page0.json");
            var indexUri = new Uri(_baseUri, "v3-catalog0/index.json");
            var contentBaseUri = new Uri(_baseUri, "packages");

            SharedInit(useLegacy: true, useSemVer2: true, baseUri: _baseUri, indexUri: indexUri, contentBaseUri: contentBaseUri);

            var commitId1 = Guid.NewGuid().ToString();
            var commitId2 = Guid.NewGuid().ToString();
            var commitTimeStamp1 = DateTimeOffset.UtcNow.AddMinutes(-1);
            var commitTimeStamp2 = commitTimeStamp1.AddMinutes(1);
            var independentPackageDetails1 = new CatalogIndependentPackageDetails(
                id: "ABC",
                version: "1.0.0",
                baseUri: _baseUri.AbsoluteUri,
                commitId: commitId1,
                commitTimeStamp: commitTimeStamp1);
            var independentPackageDetails2 = new CatalogIndependentPackageDetails(
                id: "AbC",
                version: "1.0.1",
                baseUri: _baseUri.AbsoluteUri,
                commitId: commitId1,
                commitTimeStamp: commitTimeStamp1);
            var independentPackageDetails3 = new CatalogIndependentPackageDetails(
                id: "abc",
                version: "1.0.2",
                baseUri: _baseUri.AbsoluteUri,
                commitId: commitId2,
                commitTimeStamp: commitTimeStamp2);
            var packageDetails = new[]
            {
                CatalogPackageDetails.Create(independentPackageDetails1),
                CatalogPackageDetails.Create(independentPackageDetails2),
                CatalogPackageDetails.Create(independentPackageDetails3)
            };

            var independentPage = new CatalogIndependentPage(
                pageUri.AbsoluteUri,
                CatalogConstants.CatalogPage,
                commitId2,
                commitTimeStamp2.ToString(CatalogConstants.CommitTimeStampFormat),
                packageDetails.Length,
                indexUri.AbsoluteUri,
                packageDetails,
                _contextKeyword);

            var index = CatalogIndex.Create(independentPage, _contextKeyword);

            var catalogStorage = new MemoryStorage(_baseUri);

            catalogStorage.Content.TryAdd(indexUri, CreateStringStorageContent(index));
            catalogStorage.Content.TryAdd(pageUri, CreateStringStorageContent(independentPage));
            catalogStorage.Content.TryAdd(
                new Uri(independentPackageDetails1.IdKeyword),
                CreateStringStorageContent(independentPackageDetails1));
            catalogStorage.Content.TryAdd(
                new Uri(independentPackageDetails2.IdKeyword),
                CreateStringStorageContent(independentPackageDetails2));
            catalogStorage.Content.TryAdd(
                new Uri(independentPackageDetails3.IdKeyword),
                CreateStringStorageContent(independentPackageDetails3));

            await _mockServer.AddStorageAsync(catalogStorage);

            await _target.RunAsync(CancellationToken.None);

            var expectedPage = new ExpectedPage(independentPackageDetails1, independentPackageDetails2, independentPackageDetails3);

            Verify(_legacyStorage, expectedPage);
            Verify(_semVer2Storage, expectedPage);
        }

        private void Verify(MemoryStorage storage, ExpectedPage expectedPage)
        {
            var firstPackageDetails = expectedPage.Details.First();
            var registrationIndexUri = GetRegistrationPackageIndexUri(storage.BaseAddress, firstPackageDetails.Id.ToLowerInvariant());
            var registrationIndex = GetStorageContent<RegistrationIndex>(storage, registrationIndexUri);

            var commitId = registrationIndex.CommitId;
            var commitTimeStamp = registrationIndex.CommitTimeStamp;

            Assert.Equal(1, registrationIndex.Count);

            var registrationPage = registrationIndex.Items.Single();

            Assert.Equal(CatalogConstants.CatalogCatalogPage, registrationPage.TypeKeyword);
            Assert.Equal(commitId, registrationPage.CommitId);
            Assert.Equal(commitTimeStamp, registrationPage.CommitTimeStamp);
            Assert.Equal(expectedPage.Details.Count, registrationPage.Count);
            Assert.Equal(registrationIndexUri.AbsoluteUri, registrationPage.Parent);
            Assert.Equal(expectedPage.LowerVersion, registrationPage.Lower);
            Assert.Equal(expectedPage.UpperVersion, registrationPage.Upper);

            Assert.Equal(expectedPage.Details.Count, registrationPage.Count);
            Assert.Equal(registrationPage.Count, registrationPage.Items.Length);

            for (var i = 0; i < registrationPage.Count; ++i)
            {
                var catalogPackageDetails = expectedPage.Details[i];
                var packageId = catalogPackageDetails.Id.ToLowerInvariant();
                var packageVersion = catalogPackageDetails.Version.ToLowerInvariant();
                var registrationPackageVersionUri = GetRegistrationPackageVersionUri(storage.BaseAddress, packageId, packageVersion);
                var packageContentUri = GetPackageContentUri(storage.BaseAddress, packageId, packageVersion);

                var registrationPackage = registrationPage.Items[i];

                Assert.Equal(registrationPackageVersionUri.AbsoluteUri, registrationPackage.IdKeyword);
                Assert.Equal(CatalogConstants.Package, registrationPackage.TypeKeyword);
                Assert.Equal(commitId, registrationPackage.CommitId);
                Assert.Equal(commitTimeStamp, registrationPackage.CommitTimeStamp);
                Assert.Equal(packageContentUri.AbsoluteUri, registrationPackage.PackageContent);
                Assert.Equal(registrationIndexUri.AbsoluteUri, registrationPackage.Registration);

                var registrationCatalogEntry = registrationPackage.CatalogEntry;

                Assert.Equal(catalogPackageDetails.IdKeyword, registrationCatalogEntry.IdKeyword);
                Assert.Equal(CatalogConstants.PackageDetails, registrationCatalogEntry.TypeKeyword);
                Assert.Equal(catalogPackageDetails.Id, registrationCatalogEntry.Id);
                Assert.Equal(catalogPackageDetails.Version, registrationCatalogEntry.Version);
                Assert.Equal(catalogPackageDetails.Authors, registrationCatalogEntry.Authors);
                Assert.Equal(catalogPackageDetails.Description, registrationCatalogEntry.Description);
                Assert.Equal(catalogPackageDetails.Listed, registrationCatalogEntry.Listed);

                Assert.Equal(packageContentUri.AbsoluteUri, registrationCatalogEntry.PackageContent);
                Assert.Equal(GetRegistrationDateTime(catalogPackageDetails.Published), registrationCatalogEntry.Published);
                Assert.Equal(catalogPackageDetails.RequireLicenseAcceptance, registrationCatalogEntry.RequireLicenseAcceptance);

                var registrationPackageDetails = GetStorageContent<RegistrationIndependentPackage>(storage, new Uri(registrationPackage.IdKeyword));

                Assert.Equal(registrationPackageVersionUri.AbsoluteUri, registrationPackageDetails.IdKeyword);
                Assert.Equal(new[] { CatalogConstants.Package, CatalogConstants.NuGetCatalogSchemaPermalinkUri }, registrationPackageDetails.TypeKeyword);
                Assert.Equal(catalogPackageDetails.IdKeyword, registrationPackageDetails.CatalogEntry);
                Assert.Equal(catalogPackageDetails.Listed, registrationPackageDetails.Listed);
                Assert.Equal(packageContentUri.AbsoluteUri, registrationPackageDetails.PackageContent);
                Assert.Equal(GetRegistrationDateTime(catalogPackageDetails.Published), registrationPackageDetails.Published);
                Assert.Equal(registrationIndexUri.AbsoluteUri, registrationPackageDetails.Registration);
            }
        }

        private static StringStorageContent CreateStringStorageContent<T>(T value)
        {
            return new StringStorageContent(JsonConvert.SerializeObject(value, _jsonSettings));
        }
    }
}
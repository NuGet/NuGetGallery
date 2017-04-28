// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Registration;
using Xunit;

namespace NgTests
{
    public class RegistrationCollectorTests
    {
        private MemoryStorage _legacyStorage;
        private TestStorageFactory _legacyStorageFactory;
        private MockServerHttpClientHandler _mockServer;
        private RegistrationCollector _target;
        private MemoryStorage _semVer2Storage;
        private TestStorageFactory _semVer2StorageFactory;

        private void SharedInit(bool useLegacy, bool useSemVer2)
        {
            if (useLegacy)
            {
                _legacyStorage = new MemoryStorage();
                _legacyStorageFactory = new TestStorageFactory(name => _legacyStorage.WithName(name));
            }

            if (useSemVer2)
            {
                _semVer2Storage = new MemoryStorage();
                _semVer2StorageFactory = new TestStorageFactory(name => _semVer2Storage.WithName(name));
            }

            _mockServer = new MockServerHttpClientHandler();
            _mockServer.SetAction("/", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            // Setup collector
            _target = new RegistrationCollector(
                new Uri("http://tempuri.org/index.json"),
                _legacyStorageFactory,
                _semVer2StorageFactory,
                handlerFunc: () => _mockServer)
            {
                ContentBaseAddress = new Uri("http://tempuri.org/packages")
            };

            RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();
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
            await _mockServer.AddStorage(catalogStorage);

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
            await Assert.ThrowsAsync<Exception>(() => _target.Run(front, back, CancellationToken.None));
            var cursorBeforeRetry = front.Value;
            await _target.Run(front, back, CancellationToken.None);
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

            Assert.Equal(DateTime.Parse(expectedCursorBeforeRetry).ToUniversalTime(), cursorBeforeRetry);
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
            await _mockServer.AddStorage(catalogStorage);

            ReadWriteCursor front = new DurableCursor(_legacyStorage.ResolveUri("cursor.json"), _legacyStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

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
            await _mockServer.AddStorage(catalogStorage);

            ReadWriteCursor front = new DurableCursor(_legacyStorage.ResolveUri("cursor.json"), _legacyStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

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
            await _mockServer.AddStorage(catalogStorage);
            
            var front = new DurableCursor(_legacyStorage.ResolveUri("cursor.json"), _legacyStorage, MemoryCursor.MinValue);
            var back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

            // Assert
            // Verify the contents of the legacy (non-SemVer 2.0.0) storage
            Assert.Equal(1, _legacyStorage.Content.Count);

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
            await _mockServer.AddStorage(catalogStorage);

            var front = new DurableCursor(_legacyStorage.ResolveUri("cursor.json"), _legacyStorage, MemoryCursor.MinValue);
            var back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

            // Assert
            var legacyCursor = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(legacyCursor.Key);
            var legacyIndex = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/testpackage.semver2/index.json"));
            Assert.Null(legacyIndex.Key);
            var legacyLeaf = _legacyStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/testpackage.semver2/1.0.0-alpha.1.json"));
            Assert.Null(legacyLeaf.Key);
            Assert.Equal(1, _legacyStorage.Content.Count);

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
            await _mockServer.AddStorage(catalogStorage);

            var front = new DurableCursor(_legacyStorage.ResolveUri("cursor.json"), _legacyStorage, MemoryCursor.MinValue);
            var back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

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
            await _mockServer.AddStorage(catalogStorage);

            ReadWriteCursor front = new DurableCursor(_legacyStorage.ResolveUri("cursor.json"), _legacyStorage, MemoryCursor.MinValue);

            // Act
            await _target.Run(
                front,
                new MemoryCursor(DateTime.Parse("2015-10-12T10:08:54.1506742")),
                CancellationToken.None);
            var intermediateUris = _legacyStorage
                .Content
                .Select(pair => pair.Key.ToString())
                .OrderBy(uri => uri)
                .ToList();
            await _target.Run(
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
    }
}
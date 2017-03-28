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
        private MemoryStorage _catalogToRegistrationStorage;
        private TestStorageFactory _catalogToRegistrationStorageFactory;
        private MockServerHttpClientHandler _mockServer;
        private RegistrationCollector _target;

        void SharedInit()
        {
            _catalogToRegistrationStorage = new MemoryStorage();
            _catalogToRegistrationStorageFactory = new TestStorageFactory(name => _catalogToRegistrationStorage.WithName(name));
            _mockServer = new MockServerHttpClientHandler();
            _mockServer.SetAction("/", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            // Setup collector
            _target = new RegistrationCollector(new Uri("http://tempuri.org/index.json"), _catalogToRegistrationStorageFactory, () => _mockServer)
            {
                ContentBaseAddress = new Uri("http://tempuri.org/packages")
            };

            RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();
        }

        [Fact]
        public async Task CreatesRegistrationsAndRespectsDeletes()
        {
            // Arrange
            SharedInit();

            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            await _mockServer.AddStorage(catalogStorage);

            ReadWriteCursor front = new DurableCursor(_catalogToRegistrationStorage.ResolveUri("cursor.json"), _catalogToRegistrationStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

            // Assert
            Assert.Equal(6, _catalogToRegistrationStorage.Content.Count);

            // Ensure storage has cursor.json
            var cursorJson = _catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(cursorJson.Key);

            // Check package entries - ListedPackage
            var package1Index = _catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/index.json"));
            Assert.NotNull(package1Index.Key);
            Assert.Contains("\"catalog:CatalogRoot\"", package1Index.Value.GetContentString());
            Assert.Contains("\"PackageRegistration\"", package1Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2015.10.12.10.08.54/listedpackage.1.0.0.json\"", package1Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/packages/listedpackage.1.0.0.nupkg\"", package1Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2015.10.12.10.08.55/listedpackage.1.0.1.json\"", package1Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/packages/listedpackage.1.0.1.nupkg\"", package1Index.Value.GetContentString());
            Assert.Contains("\"lower\":\"1.0.0\",", package1Index.Value.GetContentString());
            Assert.Contains("\"upper\":\"1.0.1\"", package1Index.Value.GetContentString());

            var package1 = _catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.0.json"));
            Assert.NotNull(package1.Key);

            var package2 = _catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1.json"));
            Assert.NotNull(package2.Key);

            // Check package entries - UnlistedPackage
            var package2Index = _catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/index.json"));
            Assert.NotNull(package1Index.Key);
            Assert.Contains("\"catalog:CatalogRoot\"", package2Index.Value.GetContentString());
            Assert.Contains("\"PackageRegistration\"", package2Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2015.10.12.10.08.54/unlistedpackage.1.0.0.json\"", package2Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/packages/unlistedpackage.1.0.0.nupkg\"", package2Index.Value.GetContentString());
            Assert.Contains("\"lower\":\"1.0.0\",", package2Index.Value.GetContentString());
            Assert.Contains("\"upper\":\"1.0.0\"", package2Index.Value.GetContentString());

            var package3 = _catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0.json"));
            Assert.NotNull(package3.Key);

            // Ensure storage does not have the deleted "OtherPackage"
            var otherPackageIndex = _catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage/index.json"));
            Assert.Null(otherPackageIndex.Key);
        }

        [Theory]
        [MemberData(nameof(PageContent))]
        public async Task WhenPackageHasMultipleCommitsRespectsOrder(string pageContent)
        {
            // Arrange
            SharedInit();

            var catalogStorage = Catalogs.CreateTestCatalogWithThreeItemsForSamePackage(pageContent);
            await _mockServer.AddStorage(catalogStorage);

            ReadWriteCursor front = new DurableCursor(_catalogToRegistrationStorage.ResolveUri("cursor.json"), _catalogToRegistrationStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

            // Assert
            Assert.Equal(3, _catalogToRegistrationStorage.Content.Count);

            // Ensure storage has cursor.json
            var cursorJson = _catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(cursorJson.Key);

            // Check package entries - ListedPackage
            var myPackageIndexFile = _catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/mypackage/index.json"));
            Assert.NotNull(myPackageIndexFile.Key);
            Assert.Contains("\"catalog:CatalogRoot\"", myPackageIndexFile.Value.GetContentString());
            Assert.Contains("\"PackageRegistration\"", myPackageIndexFile.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2017.02.08.17.16.18/mypackage.3.0.0.json\"", myPackageIndexFile.Value.GetContentString());
            Assert.Contains("\"packageContent\":\"http://tempuri.org/packages/mypackage.3.0.0.nupkg\"", myPackageIndexFile.Value.GetContentString());
            Assert.Contains("\"lower\":\"3.0.0\",", myPackageIndexFile.Value.GetContentString());
            Assert.Contains("\"upper\":\"3.0.0\"", myPackageIndexFile.Value.GetContentString());

            var myPackageVersionFile = _catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/mypackage/3.0.0.json"));
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
    }
}
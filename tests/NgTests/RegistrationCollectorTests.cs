// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
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
        [Fact]
        public async Task CreatesRegistrationsAndRespectsDeletes()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            var catalogToRegistrationStorage = new MemoryStorage();
            var catalogToRegistrationStorageFactory = new TestStorageFactory(name => catalogToRegistrationStorage.WithName(name));

            var mockServer = new MockServerHttpClientHandler();

            mockServer.SetAction("/", async request => new HttpResponseMessage(HttpStatusCode.OK));
            await mockServer.AddStorage(catalogStorage);

            // Setup collector
            var target = new RegistrationCollector(new Uri("http://tempuri.org/index.json"), catalogToRegistrationStorageFactory, () => mockServer)
            {
                ContentBaseAddress = new Uri("http://tempuri.org/packages")
            };

            RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();

            ReadWriteCursor front = new DurableCursor(catalogToRegistrationStorage.ResolveUri("cursor.json"), catalogToRegistrationStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await target.Run(front, back, CancellationToken.None);

            // Assert
            Assert.Equal(6, catalogToRegistrationStorage.Content.Count);

            // Ensure storage has cursor.json
            var cursorJson = catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(cursorJson.Key);

            // Check package entries - ListedPackage
            var package1Index = catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/index.json"));
            Assert.NotNull(package1Index.Key);
            Assert.Contains("\"catalog:CatalogRoot\"", package1Index.Value.GetContentString());
            Assert.Contains("\"PackageRegistration\"", package1Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2015.10.12.10.08.54/listedpackage.1.0.0.json\"", package1Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/packages/listedpackage.1.0.0.nupkg\"", package1Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2015.10.12.10.08.55/listedpackage.1.0.1.json\"", package1Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/packages/listedpackage.1.0.1.nupkg\"", package1Index.Value.GetContentString());
            Assert.Contains("\"lower\":\"1.0.0\",", package1Index.Value.GetContentString());
            Assert.Contains("\"upper\":\"1.0.1\"", package1Index.Value.GetContentString());

            var package1 = catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.0.json"));
            Assert.NotNull(package1.Key);

            var package2 = catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1.json"));
            Assert.NotNull(package2.Key);

            // Check package entries - UnlistedPackage
            var package2Index = catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/index.json"));
            Assert.NotNull(package1Index.Key);
            Assert.Contains("\"catalog:CatalogRoot\"", package2Index.Value.GetContentString());
            Assert.Contains("\"PackageRegistration\"", package2Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/data/2015.10.12.10.08.54/unlistedpackage.1.0.0.json\"", package2Index.Value.GetContentString());
            Assert.Contains("\"http://tempuri.org/packages/unlistedpackage.1.0.0.nupkg\"", package2Index.Value.GetContentString());
            Assert.Contains("\"lower\":\"1.0.0\",", package2Index.Value.GetContentString());
            Assert.Contains("\"upper\":\"1.0.0\"", package2Index.Value.GetContentString());

            var package3 = catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0.json"));
            Assert.NotNull(package3.Key);

            // Ensure storage does not have the deleted "OtherPackage"
            var otherPackageIndex = catalogToRegistrationStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage/index.json"));
            Assert.Null(otherPackageIndex.Key);
        }
    }
}
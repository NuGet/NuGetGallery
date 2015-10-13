// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ng;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace NgTests
{
    public class DnxCatalogCollectorTests
    {
        [Fact]
        public async Task CreatesFlatContainerAndRespectsDeletes()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));

            var mockServer = new MockServerHttpClientHandler();

            mockServer.SetAction("/", async request => new HttpResponseMessage(HttpStatusCode.OK));
            await mockServer.AddStorage(catalogStorage);

            mockServer.SetAction("/listedpackage.1.0.0.nupkg", async request => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.0.zip")) });
            mockServer.SetAction("/listedpackage.1.0.1.nupkg", async request => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) });
            mockServer.SetAction("/unlistedpackage.1.0.0.nupkg", async request => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\UnlistedPackage.1.0.0.zip")) });
            mockServer.SetAction("/otherpackage.1.0.0.nupkg", async request => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\OtherPackage.1.0.0.zip")) });

            // Setup collector
            var target = new DnxCatalogCollector(new Uri("http://tempuri.org/index.json"), catalogToDnxStorageFactory, () => mockServer)
            {
                ContentBaseAddress = new Uri("http://tempuri.org/packages")
            };
            ReadWriteCursor front = new DurableCursor(catalogToDnxStorage.ResolveUri("cursor.json"), catalogToDnxStorage, MemoryCursor.Min.Value);
            ReadCursor back = MemoryCursor.Max;

            // Act
            await target.Run(front, back, CancellationToken.None);

            // Assert
            Assert.Equal(9, catalogToDnxStorage.Content.Count);

            // Ensure storage has cursor.json
            var cursorJson = catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(cursorJson.Key);

            // Check package entries - ListedPackage
            var package1Index = catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/index.json"));
            Assert.NotNull(package1Index.Key);
            Assert.Contains("\"1.0.0\"", package1Index.Value.GetContentString());
            Assert.Contains("\"1.0.1\"", package1Index.Value.GetContentString());

            var package1Nuspec = catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.0/listedpackage.nuspec"));
            var package1Nupkg = catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.0/listedpackage.1.0.0.nupkg"));
            Assert.NotNull(package1Nuspec.Key);
            Assert.NotNull(package1Nupkg.Key);

            var package2Nuspec = catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1/listedpackage.nuspec"));
            var package2Nupkg = catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1/listedpackage.1.0.1.nupkg"));
            Assert.NotNull(package2Nuspec.Key);
            Assert.NotNull(package2Nupkg.Key);

            // Check package entries - UnlistedPackage
            var package3Index = catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/index.json"));
            Assert.NotNull(package3Index.Key);
            Assert.Contains("\"1.0.0\"", package3Index.Value.GetContentString());

            var package3Nuspec = catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0/unlistedpackage.nuspec"));
            var package3Nupkg = catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0/unlistedpackage.1.0.0.nupkg"));
            Assert.NotNull(package3Nuspec.Key);
            Assert.NotNull(package3Nupkg.Key);

            // Ensure storage does not have the deleted "OtherPackage"
            var otherPackageIndex = catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage/index.json"));
            var otherPackageNuspec = catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage/1.0.0/otherpackage.nuspec"));
            var otherPackageNupkg = catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage/1.0.0/otherpackage.1.0.0.nupkg"));
            Assert.Null(otherPackageIndex.Key);
            Assert.Null(otherPackageNuspec.Key);
            Assert.Null(otherPackageNupkg.Key);
        }
    }
}
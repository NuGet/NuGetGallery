// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Dnx;
using Xunit;

namespace NgTests
{
    public class DnxCatalogCollectorTests
    {
        private MemoryStorage _catalogToDnxStorage;
        private TestStorageFactory _catalogToDnxStorageFactory;
        private MockServerHttpClientHandler _mockServer;
        private DnxCatalogCollector _target;

        private void SharedInit()
        {
            _catalogToDnxStorage = new MemoryStorage();
            _catalogToDnxStorageFactory = new TestStorageFactory(name => _catalogToDnxStorage.WithName(name));
            _mockServer = new MockServerHttpClientHandler();
            _mockServer.SetAction("/", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            // Setup collector
            _target = new DnxCatalogCollector(new Uri("http://tempuri.org/index.json"), _catalogToDnxStorageFactory, () => _mockServer)
            {
                ContentBaseAddress = new Uri("http://tempuri.org/packages")
            };            
        }

        [Fact]
        public async Task CreatesFlatContainerAndRespectsDeletes()
        {
            // Arrange
            SharedInit();
            
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction("/listedpackage.1.0.0.nupkg", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.0.zip")) }));
            _mockServer.SetAction("/listedpackage.1.0.1.nupkg", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));
            _mockServer.SetAction("/unlistedpackage.1.0.0.nupkg", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\UnlistedPackage.1.0.0.zip")) }));
            _mockServer.SetAction("/otherpackage.1.0.0.nupkg", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\OtherPackage.1.0.0.zip")) }));

            // Setup collector
            ReadWriteCursor front = new DurableCursor(_catalogToDnxStorage.ResolveUri("cursor.json"), _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

            // Assert
            Assert.Equal(9, _catalogToDnxStorage.Content.Count);

            // Ensure storage has cursor.json
            var cursorJson = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(cursorJson.Key);

            // Check package entries - ListedPackage
            var package1Index = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/index.json"));
            Assert.NotNull(package1Index.Key);
            Assert.Contains("\"1.0.0\"", package1Index.Value.GetContentString());
            Assert.Contains("\"1.0.1\"", package1Index.Value.GetContentString());

            var package1Nuspec = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.0/listedpackage.nuspec"));
            var package1Nupkg = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.0/listedpackage.1.0.0.nupkg"));
            Assert.NotNull(package1Nuspec.Key);
            Assert.NotNull(package1Nupkg.Key);

            var package2Nuspec = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1/listedpackage.nuspec"));
            var package2Nupkg = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1/listedpackage.1.0.1.nupkg"));
            Assert.NotNull(package2Nuspec.Key);
            Assert.NotNull(package2Nupkg.Key);

            // Check package entries - UnlistedPackage
            var package3Index = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/index.json"));
            Assert.NotNull(package3Index.Key);
            Assert.Contains("\"1.0.0\"", package3Index.Value.GetContentString());

            var package3Nuspec = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0/unlistedpackage.nuspec"));
            var package3Nupkg = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0/unlistedpackage.1.0.0.nupkg"));
            Assert.NotNull(package3Nuspec.Key);
            Assert.NotNull(package3Nupkg.Key);

            // Ensure storage does not have the deleted "OtherPackage"
            var otherPackageIndex = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage/index.json"));
            var otherPackageNuspec = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage/1.0.0/otherpackage.nuspec"));
            var otherPackageNupkg = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage/1.0.0/otherpackage.1.0.0.nupkg"));
            Assert.Null(otherPackageIndex.Key);
            Assert.Null(otherPackageNuspec.Key);
            Assert.Null(otherPackageNupkg.Key);
        }

        [Theory]
        [InlineData("/unlistedpackage.1.0.0.nupkg", null)]
        [InlineData("/listedpackage.1.0.1.nupkg", "2015-10-12T10:08:54.1506742Z")]
        [InlineData("/anotherpackage.1.0.0.nupkg", "2015-10-12T10:08:54.1506742Z")]
        public async Task DoesNotSkipPackagesWhenExceptionOccurs(string catalogUri, string expectedCursorBeforeRetry)
        {
            // Arrange 
            SharedInit();

            var catalogStorage = Catalogs.CreateTestCatalogWithCommitThenTwoPackageCommit();
            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction("/unlistedpackage.1.0.0.nupkg", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\UnlistedPackage.1.0.0.zip")) }));
            _mockServer.SetAction("/listedpackage.1.0.1.nupkg", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));
            _mockServer.SetAction("/anotherpackage.1.0.0.nupkg", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.0.zip")) }));

            // Make the first request for a catalog leaf node fail. This will cause the registration collector
            // to fail the first time but pass the second time.
            FailFirstRequest(catalogUri);

            expectedCursorBeforeRetry = expectedCursorBeforeRetry ?? MemoryCursor.MinValue.ToString("O");

            ReadWriteCursor front = new DurableCursor(
                _catalogToDnxStorage.ResolveUri("cursor.json"),
                _catalogToDnxStorage,
                MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await Assert.ThrowsAsync<HttpRequestException>(() => _target.Run(front, back, CancellationToken.None));
            var cursorBeforeRetry = front.Value;
            await _target.Run(front, back, CancellationToken.None);
            var cursorAfterRetry = front.Value;

            // Assert
            var unlistedPackage100 = _catalogToDnxStorage
                .Content
                .FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0/unlistedpackage.1.0.0.nupkg"));
            Assert.NotNull(unlistedPackage100.Key);

            var listedPackage101 = _catalogToDnxStorage
                .Content
                .FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1/listedpackage.1.0.1.nupkg"));
            Assert.NotNull(listedPackage101.Key);

            var anotherPackage100 = _catalogToDnxStorage
                .Content
                .FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/anotherpackage/1.0.0/anotherpackage.1.0.0.nupkg"));
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

        [Theory]
        [InlineData("1.2.0")]
        [InlineData("0.1.2")]
        [InlineData("1.2.3.4")]
        [InlineData("1.2.3-beta1")]
        [InlineData("1.2.3-beta.1")]
        [Description("Test the dnxmaker save and delete scenarios.")]
        public async Task DnxMakerTestVersion(string version)
        {
            string id = "testid";
            // Arrange
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            DnxMaker maker = new DnxMaker(catalogToDnxStorageFactory);

            var nupkg = new MemoryStream();
            StreamWriter writer = new StreamWriter(nupkg);
            writer.Write("nupkg data");
            writer.Flush();
            nupkg.Position = 0;

            //Act
            var dnxEntry = await maker.AddPackage(nupkg, "nuspec data", id, version, CancellationToken.None);
            string expectedNuspec = $"{catalogToDnxStorage.BaseAddress}{id}/{version}/{id}.nuspec";
            string expectedNupkg = $"{catalogToDnxStorage.BaseAddress}{id}/{version}/{id}.{version}.nupkg";

            var storageForPackage = catalogToDnxStorageFactory.Create(id);
            var indexJson = await storageForPackage.Load(new Uri(storageForPackage.BaseAddress, "index.json"), new CancellationToken());

            var indexObject = JObject.Parse(indexJson.GetContentString());

            var versions = indexObject["versions"].ToObject<string[]>();

            //Assert
            Assert.True(versions.Length > 0);
            Assert.Equal(version, versions[0]);
            Assert.Equal(expectedNuspec, dnxEntry.Nuspec.ToString());
            Assert.Equal(expectedNupkg, dnxEntry.Nupkg.ToString());
            //three items : nuspec, nupkg, and index.json
            Assert.Equal(catalogToDnxStorage.Content.Count, 3);

            //Act
            await maker.DeletePackage(id, version, CancellationToken.None);
            //Assert
            Assert.Equal(catalogToDnxStorage.Content.Count, 0);
        }

        [Theory]
        [InlineData("1.2")]
        [InlineData("1.2.3.0")]
        [InlineData("1.02.3")]
        [Description("Test the dnxmaker save and delete scenarios.")]
        public async Task DnxMakerFailsTestVersion(string version)
        {
            string id = "testid";
            // Arrange
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            DnxMaker maker = new DnxMaker(catalogToDnxStorageFactory);

            var nupkg = new MemoryStream();
            StreamWriter writer = new StreamWriter(nupkg);
            writer.Write("nupkg data");
            writer.Flush();
            nupkg.Position = 0;

            //Act
            var dnxEntry = await maker.AddPackage(nupkg, "nuspec data", id, version, CancellationToken.None);
            string expectedNuspec = $"{catalogToDnxStorage.BaseAddress}{id}/{version}/{id}.nuspec";
            string expectedNupkg = $"{catalogToDnxStorage.BaseAddress}{id}/{version}/{id}.{version}.nupkg";

            var storageForPackage = catalogToDnxStorageFactory.Create(id);
            var indexJson = await storageForPackage.Load(new Uri(storageForPackage.BaseAddress, "index.json"), new CancellationToken());

            var indexObject = JObject.Parse(indexJson.GetContentString());

            var versions = indexObject["versions"].ToObject<string[]>();

            //Assert
            Assert.True(versions.Length > 0);
            Assert.Equal(version, versions[0]);
            Assert.NotEqual(expectedNuspec, dnxEntry.Nuspec.ToString());
            Assert.NotEqual(expectedNupkg, dnxEntry.Nupkg.ToString());
            //three items : nuspec, nupkg, and index.json
            Assert.Equal(catalogToDnxStorage.Content.Count, 3);

            //Act
            await maker.DeletePackage(id, version, CancellationToken.None);
            //Assert
            Assert.Equal(catalogToDnxStorage.Content.Count, 0);
        }

    }
}
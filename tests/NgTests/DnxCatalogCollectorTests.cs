// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Dnx;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace NgTests
{
    public class DnxCatalogCollectorTests
    {
        private MemoryStorage _catalogToDnxStorage;
        private TestStorageFactory _catalogToDnxStorageFactory;
        private MockServerHttpClientHandler _mockServer;
        private ILogger<DnxCatalogCollector> _logger;
        private DnxCatalogCollector _target;

        public DnxCatalogCollectorTests()
        {
            _catalogToDnxStorage = new MemoryStorage();
            _catalogToDnxStorageFactory = new TestStorageFactory(name => _catalogToDnxStorage.WithName(name));
            _mockServer = new MockServerHttpClientHandler();
            _mockServer.SetAction("/", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            var loggerFactory = new LoggerFactory();
            _logger = loggerFactory.CreateLogger<DnxCatalogCollector>();

            // Setup collector
            _target = new DnxCatalogCollector(
                new Uri("http://tempuri.org/index.json"),
                _catalogToDnxStorageFactory,
                new Mock<ITelemetryService>().Object,
                _logger,
                () => _mockServer)
            {
                ContentBaseAddress = new Uri("http://tempuri.org/packages/")
            };
        }

        [Fact]
        public async Task SkipsPackagesWhenCannotFindNuspecInNupkg()
        {
            // Arrange
            // This package has no nuspec, and should be ignored.
            var zipWithNoNuspec = CreateZipStreamWithEntry("readme.txt");

            // This package has a nuspec with the wrong name, which should be accepted.
            var zipWithWrongNameNuspec = CreateZipStreamWithEntry("Newtonsoft.Json.nuspec");
            
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackages();
            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(zipWithNoNuspec) }));
            _mockServer.SetAction(
                "/packages/listedpackage.1.0.1.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));
            _mockServer.SetAction(
                "/packages/unlistedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(zipWithWrongNameNuspec) }));

            // Setup collector
            ReadWriteCursor front = new DurableCursor(_catalogToDnxStorage.ResolveUri("cursor.json"), _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

            // Assert
            Assert.Equal(7, _catalogToDnxStorage.Content.Count);

            // Ensure storage has cursor.json
            var cursorJson = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(cursorJson.Key);

            // Check package entries - ListedPackage
            var package1Index = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/index.json"));
            Assert.NotNull(package1Index.Key);
            Assert.DoesNotContain("\"1.0.0\"", package1Index.Value.GetContentString());
            Assert.Contains("\"1.0.1\"", package1Index.Value.GetContentString());

            Assert.Empty(_catalogToDnxStorage.Content.Where(x => x.Key.PathAndQuery.Contains("/listedpackage/1.0.0")));

            var packageNuspec = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1/listedpackage.nuspec"));
            var packageNupkg = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1/listedpackage.1.0.1.nupkg"));
            Assert.NotNull(packageNuspec.Key);
            Assert.NotNull(packageNupkg.Key);

            // Check package entries - UnlistedPackage
            var package2Index = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/index.json"));
            Assert.NotNull(package2Index.Key);
            Assert.Contains("\"1.0.0\"", package2Index.Value.GetContentString());

            var package2Nuspec = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0/unlistedpackage.nuspec"));
            var package2Nupkg = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0/unlistedpackage.1.0.0.nupkg"));
            Assert.NotNull(package2Nuspec.Key);
            Assert.NotNull(package2Nupkg.Key);
        }

        private static MemoryStream CreateZipStreamWithEntry(string name)
        {
            var zipWithNoNuspec = new MemoryStream();
            using (var zipArchive = new ZipArchive(zipWithNoNuspec, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zipArchive.CreateEntry(name);
                using (var entryStream = entry.Open())
                using (var entryWriter = new StreamWriter(entryStream))
                {
                    entryWriter.WriteLine("Hello, world.");
                }
            }
            zipWithNoNuspec.Position = 0;
            return zipWithNoNuspec;
        }

        [Fact]
        public async Task SkipsPackagesWhenSourceNupkgIsNotFound()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackages();
            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("Nothing.") }));
            _mockServer.SetAction(
                "/packages/listedpackage.1.0.1.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));
            _mockServer.SetAction(
                "/packages/unlistedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("Nothing.") }));

            // Setup collector
            ReadWriteCursor front = new DurableCursor(_catalogToDnxStorage.ResolveUri("cursor.json"), _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

            // Assert
            Assert.Equal(4, _catalogToDnxStorage.Content.Count);

            // Ensure storage has cursor.json
            var cursorJson = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(cursorJson.Key);

            // Check package entries - ListedPackage
            var package1Index = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/index.json"));
            Assert.NotNull(package1Index.Key);
            Assert.DoesNotContain("\"1.0.0\"", package1Index.Value.GetContentString());
            Assert.Contains("\"1.0.1\"", package1Index.Value.GetContentString());

            Assert.Empty(_catalogToDnxStorage.Content.Where(x => x.Key.PathAndQuery.Contains("/listedpackage/1.0.0")));

            var packageNuspec = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1/listedpackage.nuspec"));
            var packageNupkg = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.1/listedpackage.1.0.1.nupkg"));
            Assert.NotNull(packageNuspec.Key);
            Assert.NotNull(packageNupkg.Key);

            // Check package entries - UnlistedPackage
            Assert.Empty(_catalogToDnxStorage.Content.Where(x => x.Key.PathAndQuery.Contains("unlistedpackage")));
        }

        [Fact]
        public async Task CreatesFlatContainerAndRespectsDeletes()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.0.zip")) }));
            _mockServer.SetAction(
                "/packages/listedpackage.1.0.1.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));
            _mockServer.SetAction(
                "/packages/unlistedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\UnlistedPackage.1.0.0.zip")) }));
            _mockServer.SetAction(
                "/packages/otherpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\OtherPackage.1.0.0.zip")) }));

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

        [Fact]
        public async Task SkipsPackagesThatAreAlreadySynchronized()
        {
            // Arrange
            _catalogToDnxStorage = new SynchronizedMemoryStorage(new[]
            {
                new Uri("http://tempuri.org/packages/listedpackage.1.0.1.nupkg"),
            });
            _catalogToDnxStorageFactory = new TestStorageFactory(name => _catalogToDnxStorage.WithName(name));

            // Setup collector
            _target = new DnxCatalogCollector(
                new Uri("http://tempuri.org/index.json"),
                _catalogToDnxStorageFactory,
                new Mock<ITelemetryService>().Object,
                _logger,
                () => _mockServer)
            {
                ContentBaseAddress = new Uri("http://tempuri.org/packages/")
            };

            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.0.zip")) }));
            _mockServer.SetAction(
                "/packages/listedpackage.1.0.1.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));
            _mockServer.SetAction(
                "/packages/unlistedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\UnlistedPackage.1.0.0.zip")) }));
            _mockServer.SetAction(
                "/packages/otherpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\OtherPackage.1.0.0.zip")) }));

            // Setup collector
            ReadWriteCursor front = new DurableCursor(_catalogToDnxStorage.ResolveUri("cursor.json"), _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

            // Assert
            Assert.Equal(7, _catalogToDnxStorage.Content.Count);

            Assert.NotEmpty(_mockServer.Requests.Where(x => x.RequestUri.AbsoluteUri.Contains("unlistedpackage.1.0.0.nupkg")));
            Assert.NotEmpty(_mockServer.Requests.Where(x => x.RequestUri.AbsoluteUri.Contains("listedpackage.1.0.0.nupkg")));
            Assert.Empty(_mockServer.Requests.Where(x => x.RequestUri.AbsoluteUri.Contains("listedpackage.1.0.1.nupkg")));

            // Ensure storage has cursor.json
            var cursorJson = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("cursor.json"));
            Assert.NotNull(cursorJson.Key);

            // Check package entries - ListedPackage
            var package1Index = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/index.json"));
            Assert.NotNull(package1Index.Key);
            Assert.Contains("\"1.0.0\"", package1Index.Value.GetContentString());
            Assert.DoesNotContain("\"1.0.1\"", package1Index.Value.GetContentString());

            var package1Nuspec = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.0/listedpackage.nuspec"));
            var package1Nupkg = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage/1.0.0/listedpackage.1.0.0.nupkg"));
            Assert.NotNull(package1Nuspec.Key);
            Assert.NotNull(package1Nupkg.Key);

            Assert.Empty(_catalogToDnxStorage.Content.Where(x => x.Key.PathAndQuery.Contains("listedpackage/1.0.1")));

            // Check package entries - UnlistedPackage
            var package2Index = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/index.json"));
            Assert.NotNull(package2Index.Key);
            Assert.Contains("\"1.0.0\"", package2Index.Value.GetContentString());

            var package2Nuspec = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0/unlistedpackage.nuspec"));
            var package2Nupkg = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage/1.0.0/unlistedpackage.1.0.0.nupkg"));
            Assert.NotNull(package2Nuspec.Key);
            Assert.NotNull(package2Nupkg.Key);

            // Ensure storage does not have the deleted "OtherPackage"
            var otherPackageIndex = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage/index.json"));
            var otherPackageNuspec = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage/1.0.0/otherpackage.nuspec"));
            var otherPackageNupkg = _catalogToDnxStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage/1.0.0/otherpackage.1.0.0.nupkg"));
            Assert.Null(otherPackageIndex.Key);
            Assert.Null(otherPackageNuspec.Key);
            Assert.Null(otherPackageNupkg.Key);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task RejectsUnexpectedHttpStatusCodeWhenDownloadingPackage(HttpStatusCode statusCode)
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            await _mockServer.AddStorage(catalogStorage);
            _mockServer.Return404OnUnknownAction = true;

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(statusCode) { Content = new ByteArrayContent(new byte[0]) }));

            // Setup collector
            ReadWriteCursor front = new DurableCursor(_catalogToDnxStorage.ResolveUri("cursor.json"), _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _target.Run(front, back, CancellationToken.None));
            Assert.Equal(
                $"Expected status code OK for package download, actual: {statusCode}",
                ex.Message);
            Assert.Equal(0, _catalogToDnxStorage.Content.Count);
        }

        [Fact]
        public async Task OnlyDownloadsNupkgOncePerCatalogLeaf()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.0.zip")) }));
            _mockServer.SetAction(
                "/packages/listedpackage.1.0.1.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));
            _mockServer.SetAction(
                "/packages/unlistedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\UnlistedPackage.1.0.0.zip")) }));
            _mockServer.SetAction(
                "/packages/otherpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\OtherPackage.1.0.0.zip")) }));

            // Setup collector
            ReadWriteCursor front = new DurableCursor(_catalogToDnxStorage.ResolveUri("cursor.json"), _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

            // Assert
            Assert.Equal(9, _catalogToDnxStorage.Content.Count);

            Assert.Equal(5, _mockServer.Requests.Count);
            Assert.EndsWith("/index.json", _mockServer.Requests[0].RequestUri.AbsoluteUri);
            Assert.EndsWith("/page0.json", _mockServer.Requests[1].RequestUri.AbsoluteUri);
            Assert.Contains("/unlistedpackage.1.0.0.nupkg", _mockServer.Requests[2].RequestUri.AbsoluteUri);
            Assert.Contains("/listedpackage.1.0.0.nupkg", _mockServer.Requests[3].RequestUri.AbsoluteUri);
            Assert.Contains("/listedpackage.1.0.1.nupkg", _mockServer.Requests[4].RequestUri.AbsoluteUri);
        }

        [Fact]
        public async Task StoresCorrectPackageContent()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.0.zip")) }));
            _mockServer.SetAction(
                "/packages/listedpackage.1.0.1.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));
            _mockServer.SetAction(
                "/packages/unlistedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\UnlistedPackage.1.0.0.zip")) }));
            _mockServer.SetAction(
                "/packages/otherpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\OtherPackage.1.0.0.zip")) }));

            // Setup collector
            ReadWriteCursor front = new DurableCursor(_catalogToDnxStorage.ResolveUri("cursor.json"), _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            // Act
            await _target.Run(front, back, CancellationToken.None);

            // Assert
            Assert.Equal(9, _catalogToDnxStorage.Content.Count);

            VerifyContentBytes(
                "Packages\\ListedPackage.1.0.0.zip",
                new Uri("http://tempuri.org/listedpackage/1.0.0/listedpackage.1.0.0.nupkg"));
            VerifyContentBytes(
                "Packages\\ListedPackage.1.0.1.zip",
                new Uri("http://tempuri.org/listedpackage/1.0.1/listedpackage.1.0.1.nupkg"));
            VerifyContentBytes(
                "Packages\\UnlistedPackage.1.0.0.zip",
                new Uri("http://tempuri.org/unlistedpackage/1.0.0/unlistedpackage.1.0.0.nupkg"));
        }

        private void VerifyContentBytes(string srcFilePath, Uri destUri)
        {
            var srcBytes = GetStreamBytes(File.OpenRead(srcFilePath));
            var destBytes = _catalogToDnxStorage.ContentBytes[destUri];

            Assert.Equal(srcBytes, destBytes);
        }

        private byte[] GetStreamBytes(Stream srcStream)
        {
            using (srcStream)
            using (var memoryStream = new MemoryStream())
            {
                srcStream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        [Theory]
        [InlineData("/packages/unlistedpackage.1.0.0.nupkg", null)]
        [InlineData("/packages/listedpackage.1.0.1.nupkg", "2015-10-12T10:08:54.1506742Z")]
        [InlineData("/packages/anotherpackage.1.0.0.nupkg", "2015-10-12T10:08:54.1506742Z")]
        public async Task DoesNotSkipPackagesWhenExceptionOccurs(string catalogUri, string expectedCursorBeforeRetry)
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithCommitThenTwoPackageCommit();
            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/unlistedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\UnlistedPackage.1.0.0.zip")) }));
            _mockServer.SetAction(
                "/packages/listedpackage.1.0.1.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));
            _mockServer.SetAction(
                "/packages/anotherpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.0.zip")) }));

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

        private class SynchronizedMemoryStorage : MemoryStorage
        {
            private readonly HashSet<Uri> _synchronizedUris;
            private Dictionary<Uri, StorageContent> content;

            public SynchronizedMemoryStorage(IEnumerable<Uri> synchronizedUris)
            {
                _synchronizedUris = new HashSet<Uri>(synchronizedUris);
            }

            protected SynchronizedMemoryStorage(
                Uri baseAddress,
                Dictionary<Uri, StorageContent> content,
                Dictionary<Uri, byte[]> contentBytes,
                HashSet<Uri> synchronizedUris)
                : base(baseAddress, content, contentBytes)
            {
                _synchronizedUris = synchronizedUris;
            }

            public override Task<bool> AreSyncronized(Uri firstResourceUri, Uri secondResourceUri)
            {
                return Task.FromResult(_synchronizedUris.Contains(firstResourceUri));
            }

            public override Storage WithName(string name)
            {
                return new SynchronizedMemoryStorage(
                    new Uri(BaseAddress + name),
                    Content,
                    ContentBytes,
                    _synchronizedUris);
            }
        }
    }
}
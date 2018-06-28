// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
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
        private const string _nuspecData = "nuspec data";
        private static readonly HttpContent _noContent = new ByteArrayContent(new byte[0]);

        private MemoryStorage _catalogToDnxStorage;
        private TestStorageFactory _catalogToDnxStorageFactory;
        private MockServerHttpClientHandler _mockServer;
        private ILogger<DnxCatalogCollector> _logger;
        private DnxCatalogCollector _target;
        private Uri _cursorJsonUri;

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

            _cursorJsonUri = _catalogToDnxStorage.ResolveUri("cursor.json");
        }

        [Fact]
        public async Task Run_WhenPackageDoesNotHaveNuspec_SkipsPackage()
        {
            var zipWithNoNuspec = CreateZipStreamWithEntry("readme.txt", "content");
            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/listedpackage/index.json");
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackages();

            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(zipWithNoNuspec) }));

            var front = new DurableCursor(_cursorJsonUri, _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            await _target.Run(front, back, CancellationToken.None);

            Assert.Equal(1, _catalogToDnxStorage.Content.Count);
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(_cursorJsonUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(indexJsonUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.ContainsKey(_cursorJsonUri));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(indexJsonUri));
        }

        [Fact]
        public async Task Run_WhenPackageHasNuspecWithWrongName_ProcessesPackage()
        {
            var zipWithWrongNameNuspec = CreateZipStreamWithEntry("Newtonsoft.Json.nuspec", _nuspecData);
            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/unlistedpackage/index.json");
            var nupkgUri = _catalogToDnxStorage.ResolveUri("/unlistedpackage/1.0.0/unlistedpackage.1.0.0.nupkg");
            var nuspecUri = _catalogToDnxStorage.ResolveUri("/unlistedpackage/1.0.0/unlistedpackage.nuspec");
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackages();

            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/unlistedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(zipWithWrongNameNuspec) }));

            var front = new DurableCursor(_cursorJsonUri, _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            await _target.Run(front, back, CancellationToken.None);

            Assert.Equal(4, _catalogToDnxStorage.Content.Count);
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(_cursorJsonUri));
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(indexJsonUri));
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(nupkgUri));
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(nuspecUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.ContainsKey(_cursorJsonUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.TryGetValue(indexJsonUri, out var indexJson));
            Assert.True(_catalogToDnxStorage.ContentBytes.TryGetValue(nupkgUri, out var nupkg));
            Assert.True(_catalogToDnxStorage.ContentBytes.TryGetValue(nuspecUri, out var nuspec));

            Assert.Equal(GetExpectedIndexJsonContent("1.0.0"), Encoding.UTF8.GetString(indexJson));
            Assert.Equal(zipWithWrongNameNuspec.ToArray(), nupkg);
            Assert.Equal(_nuspecData, Encoding.UTF8.GetString(nuspec));
        }

        [Fact]
        public async Task Run_WhenSourceNupkgIsNotFound_SkipsPackage()
        {
            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/listedpackage/index.json");
            var nupkgUri = _catalogToDnxStorage.ResolveUri("/unlistedpackage/1.0.0/unlistedpackage.1.0.0.nupkg");
            var nuspecUri = _catalogToDnxStorage.ResolveUri("/unlistedpackage/1.0.0/unlistedpackage.nuspec");
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackages();

            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = _noContent }));

            var front = new DurableCursor(_cursorJsonUri, _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            await _target.Run(front, back, CancellationToken.None);

            Assert.Equal(1, _catalogToDnxStorage.Content.Count);
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(_cursorJsonUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(indexJsonUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(nupkgUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(nuspecUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.ContainsKey(_cursorJsonUri));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(indexJsonUri));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(nupkgUri));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(nuspecUri));
        }

        [Fact]
        public async Task Run_WithValidPackage_CreatesFlatContainer()
        {
            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/listedpackage/index.json");
            var nupkgUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.0/listedpackage.1.0.0.nupkg");
            var nuspecUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.0/listedpackage.nuspec");
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            var nupkgStream = File.OpenRead("Packages\\ListedPackage.1.0.0.zip");
            var expectedNupkg = GetStreamBytes(nupkgStream);

            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(nupkgStream) }));

            var front = new DurableCursor(_cursorJsonUri, _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            await _target.Run(front, back, CancellationToken.None);

            Assert.Equal(4, _catalogToDnxStorage.Content.Count);
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(_cursorJsonUri));
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(indexJsonUri));
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(nupkgUri));
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(nuspecUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.ContainsKey(_cursorJsonUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.TryGetValue(indexJsonUri, out var indexJson));
            Assert.True(_catalogToDnxStorage.ContentBytes.TryGetValue(nupkgUri, out var nupkg));
            Assert.True(_catalogToDnxStorage.ContentBytes.TryGetValue(nuspecUri, out var nuspec));

            Assert.Equal(GetExpectedIndexJsonContent("1.0.0"), Encoding.UTF8.GetString(indexJson));
            Assert.Equal(expectedNupkg, nupkg);
            Assert.Equal(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<package xmlns=\"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd\">\r\n    <metadata>\r\n        <id>ListedPackage</id>\r\n        <version>1.0.0</version>\r\n        <authors>NuGet</authors>\r\n        <requireLicenseAcceptance>false</requireLicenseAcceptance>\r\n        <description>Package description.</description>\r\n    </metadata>\r\n</package>",
                Encoding.UTF8.GetString(nuspec));
        }

        [Fact]
        public async Task Run_WithValidPackage_RespectsDeletion()
        {
            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/otherpackage/index.json");
            var nupkgUri = _catalogToDnxStorage.ResolveUri("/otherpackage/1.0.0/otherpackage.1.0.0.nupkg");
            var nuspecUri = _catalogToDnxStorage.ResolveUri("/otherpackage/1.0.0/otherpackage.nuspec");
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();

            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/otherpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\OtherPackage.1.0.0.zip")) }));

            var front = new DurableCursor(_cursorJsonUri, _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            await _target.Run(front, back, CancellationToken.None);

            Assert.Equal(1, _catalogToDnxStorage.Content.Count);
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(_cursorJsonUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(indexJsonUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(nupkgUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(nuspecUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.ContainsKey(_cursorJsonUri));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(indexJsonUri));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(nupkgUri));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(nuspecUri));
        }

        [Fact]
        public async Task Run_WithPackageCreatedThenDeleted_LeavesNoArtifacts()
        {
            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/otherpackage/index.json");
            var nupkgUri = _catalogToDnxStorage.ResolveUri("/otherpackage/1.0.0/otherpackage.1.0.0.nupkg");
            var nuspecUri = _catalogToDnxStorage.ResolveUri("/otherpackage/1.0.0/otherpackage.nuspec");
            var catalogStorage = Catalogs.CreateTestCatalogWithPackageCreatedThenDeleted();

            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/otherpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\OtherPackage.1.0.0.zip")) }));

            var front = new DurableCursor(_cursorJsonUri, _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            await _target.Run(front, back, CancellationToken.None);

            Assert.Equal(1, _catalogToDnxStorage.Content.Count);
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(_cursorJsonUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(indexJsonUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(nupkgUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(nuspecUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.ContainsKey(_cursorJsonUri));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(indexJsonUri));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(nupkgUri));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(nuspecUri));
        }

        [Fact]
        public async Task Run_WhenPackageIsAlreadySynchronized_SkipsPackage()
        {
            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/listedpackage/index.json");
            var nupkgUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.1/listedpackage.1.0.1.nupkg");
            var nuspecUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.1/listedpackage.nuspec");
            var nupkgStream = File.OpenRead("Packages\\ListedPackage.1.0.1.zip");
            var expectedNupkg = GetStreamBytes(nupkgStream);

            _catalogToDnxStorage = new SynchronizedMemoryStorage(new[]
            {
                new Uri("http://tempuri.org/packages/listedpackage.1.0.1.nupkg"),
            });
            _catalogToDnxStorageFactory = new TestStorageFactory(name => _catalogToDnxStorage.WithName(name));

            await _catalogToDnxStorage.Save(
                new Uri("http://tempuri.org/listedpackage/index.json"),
                new StringStorageContent(GetExpectedIndexJsonContent("1.0.1")),
                CancellationToken.None);

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
                "/packages/listedpackage.1.0.1.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(nupkgStream) }));

            var front = new DurableCursor(_cursorJsonUri, _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            await _target.Run(front, back, CancellationToken.None);

            Assert.Equal(2, _catalogToDnxStorage.Content.Count);
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(_cursorJsonUri));
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(indexJsonUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(nupkgUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(nuspecUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.ContainsKey(_cursorJsonUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.TryGetValue(indexJsonUri, out var indexJson));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(nupkgUri));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(nuspecUri));

            Assert.Equal(GetExpectedIndexJsonContent("1.0.1"), Encoding.UTF8.GetString(indexJson));
        }

        [Fact]
        public async Task Run_WhenPackageIsAlreadySynchronizedButNotInIndex_ProcessesPackage()
        {
            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/listedpackage/index.json");
            var nupkgUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.1/listedpackage.1.0.1.nupkg");
            var nuspecUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.1/listedpackage.nuspec");

            _catalogToDnxStorage = new SynchronizedMemoryStorage(new[]
            {
                new Uri("http://tempuri.org/packages/listedpackage.1.0.1.nupkg"),
            });
            _catalogToDnxStorageFactory = new TestStorageFactory(name => _catalogToDnxStorage.WithName(name));
            _mockServer = new MockServerHttpClientHandler();
            _mockServer.SetAction("/", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            _target = new DnxCatalogCollector(
                new Uri("http://tempuri.org/index.json"),
                _catalogToDnxStorageFactory,
                new Mock<ITelemetryService>().Object,
                new Mock<ILogger>().Object,
                () => _mockServer)
            {
                ContentBaseAddress = new Uri("http://tempuri.org/packages/")
            };

            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();

            await _mockServer.AddStorage(catalogStorage);

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.1.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));

            var front = new DurableCursor(_cursorJsonUri, _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            await _target.Run(front, back, CancellationToken.None);

            Assert.Equal(2, _catalogToDnxStorage.Content.Count);
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(_cursorJsonUri));
            Assert.True(_catalogToDnxStorage.Content.ContainsKey(indexJsonUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(nupkgUri));
            Assert.False(_catalogToDnxStorage.Content.ContainsKey(nuspecUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.ContainsKey(_cursorJsonUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.TryGetValue(indexJsonUri, out var indexJson));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(nupkgUri));
            Assert.False(_catalogToDnxStorage.ContentBytes.ContainsKey(nuspecUri));

            Assert.Equal(GetExpectedIndexJsonContent("1.0.1"), Encoding.UTF8.GetString(indexJson));
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task Run_WhenDownloadingPackage_RejectsUnexpectedHttpStatusCode(HttpStatusCode statusCode)
        {
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();

            await _mockServer.AddStorage(catalogStorage);

            _mockServer.Return404OnUnknownAction = true;

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(statusCode) { Content = _noContent }));

            var front = new DurableCursor(_cursorJsonUri, _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _target.Run(front, back, CancellationToken.None));
            Assert.Equal(
                $"Expected status code OK for package download, actual: {statusCode}",
                exception.Message);
            Assert.Equal(0, _catalogToDnxStorage.Content.Count);
        }

        [Fact]
        public async Task Run_WhenDownloadingPackage_OnlyDownloadsNupkgOncePerCatalogLeaf()
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
            ReadWriteCursor front = new DurableCursor(_cursorJsonUri, _catalogToDnxStorage, MemoryCursor.MinValue);
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

        [Theory]
        [InlineData("/packages/unlistedpackage.1.0.0.nupkg", null)]
        [InlineData("/packages/listedpackage.1.0.1.nupkg", "2015-10-12T10:08:54.1506742Z")]
        [InlineData("/packages/anotherpackage.1.0.0.nupkg", "2015-10-12T10:08:54.1506742Z")]
        public async Task Run_WhenExceptionOccurs_DoesNotSkipPackage(string catalogUri, string expectedCursorBeforeRetry)
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

            var front = new DurableCursor(
                _cursorJsonUri,
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

        private static byte[] GetStreamBytes(Stream srcStream)
        {
            using (var memoryStream = new MemoryStream())
            {
                srcStream.Position = 0;

                srcStream.CopyTo(memoryStream);

                srcStream.Position = 0;

                return memoryStream.ToArray();
            }
        }

        private static string GetExpectedIndexJsonContent(string version)
        {
            return $"{{\r\n  \"versions\": [\r\n    \"{version}\"\r\n  ]\r\n}}";
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

        private static MemoryStream CreateZipStreamWithEntry(string name, string content)
        {
            var zipWithNoNuspec = new MemoryStream();

            using (var zipArchive = new ZipArchive(zipWithNoNuspec, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zipArchive.CreateEntry(name);

                using (var entryStream = entry.Open())
                using (var entryWriter = new StreamWriter(entryStream))
                {
                    entryWriter.Write(content);
                }
            }

            zipWithNoNuspec.Position = 0;

            return zipWithNoNuspec;
        }

        private class SynchronizedMemoryStorage : MemoryStorage
        {
            private readonly HashSet<Uri> _synchronizedUris;

            public SynchronizedMemoryStorage(IEnumerable<Uri> synchronizedUris)
            {
                _synchronizedUris = new HashSet<Uri>(synchronizedUris);
            }

            protected SynchronizedMemoryStorage(
                Uri baseAddress,
                ConcurrentDictionary<Uri, StorageContent> content,
                ConcurrentDictionary<Uri, byte[]> contentBytes,
                HashSet<Uri> synchronizedUris)
                : base(baseAddress, content, contentBytes)
            {
                _synchronizedUris = synchronizedUris;
            }

            public override Task<bool> AreSynchronized(Uri firstResourceUri, Uri secondResourceUri)
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
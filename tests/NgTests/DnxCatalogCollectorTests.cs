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
        private const int _maxDegreeOfParallelism = 20;
        private static readonly HttpContent _noContent = new ByteArrayContent(new byte[0]);
        private const IAzureStorage _nullPreferredPackageSourceStorage = null;
        private static readonly Uri _contentBaseAddress = new Uri("http://tempuri.org/packages/");

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
                _nullPreferredPackageSourceStorage,
                _contentBaseAddress,
                Mock.Of<ITelemetryService>(),
                _logger,
                _maxDegreeOfParallelism,
                () => _mockServer);

            _cursorJsonUri = _catalogToDnxStorage.ResolveUri("cursor.json");
        }

        [Fact]
        public void Constructor_WhenIndexIsNull_Throws()
        {
            Uri index = null;

            using (var clientHandler = new HttpClientHandler())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new DnxCatalogCollector(
                        index,
                        new TestStorageFactory(),
                        _nullPreferredPackageSourceStorage,
                        _contentBaseAddress,
                        Mock.Of<ITelemetryService>(),
                        Mock.Of<ILogger>(),
                        maxDegreeOfParallelism: 1,
                        handlerFunc: () => clientHandler,
                        httpClientTimeout: TimeSpan.Zero));

                Assert.Equal("index", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_WhenStorageFactoryIsNull_Throws()
        {
            StorageFactory storageFactory = null;

            using (var clientHandler = new HttpClientHandler())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new DnxCatalogCollector(
                        new Uri("https://nuget.test"),
                        storageFactory,
                        _nullPreferredPackageSourceStorage,
                        contentBaseAddress: null,
                        telemetryService: Mock.Of<ITelemetryService>(),
                        logger: Mock.Of<ILogger>(),
                        maxDegreeOfParallelism: 1,
                        handlerFunc: () => clientHandler,
                        httpClientTimeout: TimeSpan.Zero));

                Assert.Equal("storageFactory", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_WhenTelemetryServiceIsNull_Throws()
        {
            using (var clientHandler = new HttpClientHandler())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new DnxCatalogCollector(
                        new Uri("https://nuget.test"),
                        new TestStorageFactory(),
                        _nullPreferredPackageSourceStorage,
                        contentBaseAddress: null,
                        telemetryService: null,
                        logger: Mock.Of<ILogger>(),
                        maxDegreeOfParallelism: 1,
                        handlerFunc: () => clientHandler,
                        httpClientTimeout: TimeSpan.Zero));

                Assert.Equal("telemetryService", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_Throws()
        {
            ILogger logger = null;

            using (var clientHandler = new HttpClientHandler())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new DnxCatalogCollector(
                        new Uri("https://nuget.test"),
                        new TestStorageFactory(),
                        _nullPreferredPackageSourceStorage,
                        null,
                        Mock.Of<ITelemetryService>(),
                        logger,
                        maxDegreeOfParallelism: 1,
                        handlerFunc: () => clientHandler,
                        httpClientTimeout: TimeSpan.Zero));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Constructor_WhenMaxDegreeOfParallelismIsLessThanOne_Throws(int maxDegreeOfParallelism)
        {
            using (var clientHandler = new HttpClientHandler())
            {
                var exception = Assert.Throws<ArgumentOutOfRangeException>(
                    () => new DnxCatalogCollector(
                        new Uri("https://nuget.test"),
                        new TestStorageFactory(),
                        _nullPreferredPackageSourceStorage,
                        contentBaseAddress: null,
                        telemetryService: Mock.Of<ITelemetryService>(),
                        logger: Mock.Of<ILogger>(),
                        maxDegreeOfParallelism: maxDegreeOfParallelism,
                        handlerFunc: () => clientHandler,
                        httpClientTimeout: TimeSpan.Zero));

                Assert.Equal("maxDegreeOfParallelism", exception.ParamName);
                Assert.StartsWith($"The argument must be within the range from 1 (inclusive) to {int.MaxValue} (inclusive).", exception.Message);
            }
        }

        [Fact]
        public void Constructor_WhenHandlerFuncIsNull_InstantiatesClass()
        {
            new DnxCatalogCollector(
                new Uri("https://nuget.test"),
                new TestStorageFactory(),
                _nullPreferredPackageSourceStorage,
                contentBaseAddress: null,
                telemetryService: Mock.Of<ITelemetryService>(),
                logger: Mock.Of<ILogger>(),
                maxDegreeOfParallelism: 1,
                handlerFunc: null,
                httpClientTimeout: TimeSpan.Zero);
        }

        [Fact]
        public void Constructor_WhenHttpClientTimeoutIsNull_InstantiatesClass()
        {
            using (var clientHandler = new HttpClientHandler())
            {
                new DnxCatalogCollector(
                    new Uri("https://nuget.test"),
                    new TestStorageFactory(),
                    _nullPreferredPackageSourceStorage,
                    contentBaseAddress: null,
                    telemetryService: Mock.Of<ITelemetryService>(),
                    logger: Mock.Of<ILogger>(),
                    maxDegreeOfParallelism: 1,
                    handlerFunc: () => clientHandler,
                    httpClientTimeout: null);
            }
        }

        [Fact]
        public async Task Run_WhenPackageDoesNotHaveNuspec_SkipsPackage()
        {
            var zipWithNoNuspec = CreateZipStreamWithEntry("readme.txt", "content");
            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/listedpackage/index.json");
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackages();

            await _mockServer.AddStorageAsync(catalogStorage);

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

            await _mockServer.AddStorageAsync(catalogStorage);

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

            await _mockServer.AddStorageAsync(catalogStorage);

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

            await _mockServer.AddStorageAsync(catalogStorage);

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

            await _mockServer.AddStorageAsync(catalogStorage);

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

            await _mockServer.AddStorageAsync(catalogStorage);

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
        public async Task Run_WithNonIAzureStorage_WhenPackageIsAlreadySynchronizedAndHasRequiredProperties_SkipsPackage()
        {
            _catalogToDnxStorage = new SynchronizedMemoryStorage(new[]
            {
                new Uri("http://tempuri.org/packages/listedpackage.1.0.1.nupkg"),
            });
            _catalogToDnxStorageFactory = new TestStorageFactory(name => _catalogToDnxStorage.WithName(name));

            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/listedpackage/index.json");
            var nupkgUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.1/listedpackage.1.0.1.nupkg");
            var nuspecUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.1/listedpackage.nuspec");
            var nupkgStream = File.OpenRead("Packages\\ListedPackage.1.0.1.zip");
            var expectedNupkg = GetStreamBytes(nupkgStream);

            await _catalogToDnxStorage.SaveAsync(
                new Uri("http://tempuri.org/listedpackage/index.json"),
                new StringStorageContent(GetExpectedIndexJsonContent("1.0.1")),
                CancellationToken.None);

            _target = new DnxCatalogCollector(
                new Uri("http://tempuri.org/index.json"),
                _catalogToDnxStorageFactory,
                _nullPreferredPackageSourceStorage,
                _contentBaseAddress,
                Mock.Of<ITelemetryService>(),
                _logger,
                _maxDegreeOfParallelism,
                () => _mockServer);

            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();
            await _mockServer.AddStorageAsync(catalogStorage);

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
        public async Task Run_WithIAzureStorage_WhenPackageIsAlreadySynchronizedAndHasRequiredProperties_SkipsPackage()
        {
            _catalogToDnxStorage = new AzureSynchronizedMemoryStorageStub(new[]
            {
                new Uri("http://tempuri.org/packages/listedpackage.1.0.0.nupkg")
            }, areRequiredPropertiesPresentAsync: true);
            _catalogToDnxStorageFactory = new TestStorageFactory(name => _catalogToDnxStorage.WithName(name));

            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/listedpackage/index.json");
            var nupkgUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.0/listedpackage.1.0.0.nupkg");
            var nuspecUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.0/listedpackage.nuspec");
            var nupkgStream = File.OpenRead("Packages\\ListedPackage.1.0.0.zip");
            var expectedNupkg = GetStreamBytes(nupkgStream);

            await _catalogToDnxStorage.SaveAsync(
                new Uri("http://tempuri.org/listedpackage/index.json"),
                new StringStorageContent(GetExpectedIndexJsonContent("1.0.0")),
                CancellationToken.None);

            _target = new DnxCatalogCollector(
                new Uri("http://tempuri.org/index.json"),
                _catalogToDnxStorageFactory,
                _nullPreferredPackageSourceStorage,
                _contentBaseAddress,
                Mock.Of<ITelemetryService>(),
                _logger,
                _maxDegreeOfParallelism,
                () => _mockServer);

            var catalogStorage = Catalogs.CreateTestCatalogWithOnePackage();
            await _mockServer.AddStorageAsync(catalogStorage);

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
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

            Assert.Equal(GetExpectedIndexJsonContent("1.0.0"), Encoding.UTF8.GetString(indexJson));
        }

        [Fact]
        public async Task Run_WithFakeIAzureStorage_WhenPackageIsAlreadySynchronizedButDoesNotHaveRequiredProperties_ProcessesPackage()
        {
            _catalogToDnxStorage = new AzureSynchronizedMemoryStorageStub(new[]
            {
                new Uri("http://tempuri.org/packages/listedpackage.1.0.0.nupkg")
            }, areRequiredPropertiesPresentAsync: false);
            _catalogToDnxStorageFactory = new TestStorageFactory(name => _catalogToDnxStorage.WithName(name));

            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/listedpackage/index.json");
            var nupkgUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.0/listedpackage.1.0.0.nupkg");
            var nuspecUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.0/listedpackage.nuspec");
            var nupkgStream = File.OpenRead("Packages\\ListedPackage.1.0.0.zip");
            var expectedNupkg = GetStreamBytes(nupkgStream);

            await _catalogToDnxStorage.SaveAsync(
                new Uri("http://tempuri.org/listedpackage/index.json"),
                new StringStorageContent(GetExpectedIndexJsonContent("1.0.0")),
                CancellationToken.None);

            _target = new DnxCatalogCollector(
                new Uri("http://tempuri.org/index.json"),
                _catalogToDnxStorageFactory,
                _nullPreferredPackageSourceStorage,
                _contentBaseAddress,
                Mock.Of<ITelemetryService>(),
                _logger,
                _maxDegreeOfParallelism,
                () => _mockServer);

            var catalogStorage = Catalogs.CreateTestCatalogWithOnePackage();
            await _mockServer.AddStorageAsync(catalogStorage);

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
            Assert.True(_catalogToDnxStorage.ContentBytes.ContainsKey(nupkgUri));
            Assert.True(_catalogToDnxStorage.ContentBytes.ContainsKey(nuspecUri));

            Assert.Equal(GetExpectedIndexJsonContent("1.0.0"), Encoding.UTF8.GetString(indexJson));
        }

        [Fact]
        public async Task Run_WhenPackageIsAlreadySynchronizedButNotInIndex_ProcessesPackage()
        {
            _catalogToDnxStorage = new SynchronizedMemoryStorage(new[]
            {
                new Uri("http://tempuri.org/packages/listedpackage.1.0.1.nupkg"),
            });
            _catalogToDnxStorageFactory = new TestStorageFactory(name => _catalogToDnxStorage.WithName(name));
            _mockServer = new MockServerHttpClientHandler();
            _mockServer.SetAction("/", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/listedpackage/index.json");
            var nupkgUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.1/listedpackage.1.0.1.nupkg");
            var nuspecUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.1/listedpackage.nuspec");

            _target = new DnxCatalogCollector(
                new Uri("http://tempuri.org/index.json"),
                _catalogToDnxStorageFactory,
                _nullPreferredPackageSourceStorage,
                _contentBaseAddress,
                Mock.Of<ITelemetryService>(),
                new Mock<ILogger>().Object,
                _maxDegreeOfParallelism,
                () => _mockServer);

            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackagesAndDelete();

            await _mockServer.AddStorageAsync(catalogStorage);

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

            await _mockServer.AddStorageAsync(catalogStorage);

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
            await _mockServer.AddStorageAsync(catalogStorage);

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

            // The packages were processed in random order.
            var remainingRequests = _mockServer.Requests
                .Skip(2)
                .Take(3)
                .Select(request => request.RequestUri.AbsoluteUri)
                .OrderBy(uri => uri)
                .ToArray();

            Assert.Contains("/listedpackage.1.0.0.nupkg", remainingRequests[0]);
            Assert.Contains("/listedpackage.1.0.1.nupkg", remainingRequests[1]);
            Assert.Contains("/unlistedpackage.1.0.0.nupkg", remainingRequests[2]);
        }

        [Theory]
        [InlineData("/packages/unlistedpackage.1.0.0.nupkg", null)]
        [InlineData("/packages/listedpackage.1.0.1.nupkg", "2015-10-12T10:08:54.1506742Z")]
        [InlineData("/packages/anotherpackage.1.0.0.nupkg", "2015-10-12T10:08:54.1506742Z")]
        public async Task Run_WhenExceptionOccurs_DoesNotSkipPackage(string catalogUri, string expectedCursorBeforeRetry)
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithCommitThenTwoPackageCommit();
            await _mockServer.AddStorageAsync(catalogStorage);

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

        [Fact]
        public async Task Run_WhenMultipleEntriesWithSamePackageIdentityInSameBatch_Throws()
        {
            var zipWithWrongNameNuspec = CreateZipStreamWithEntry("Newtonsoft.Json.nuspec", _nuspecData);
            var indexJsonUri = _catalogToDnxStorage.ResolveUri("/listedpackage/index.json");
            var nupkgUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.0/listedpackage.1.0.0.nupkg");
            var nuspecUri = _catalogToDnxStorage.ResolveUri("/listedpackage/1.0.0/listedpackage.nuspec");
            var nupkgStream = File.OpenRead(@"Packages\ListedPackage.1.0.1.zip");
            var expectedNupkg = GetStreamBytes(nupkgStream);
            var catalogStorage = Catalogs.CreateTestCatalogWithMultipleEntriesWithSamePackageIdentityInSameBatch();

            await _mockServer.AddStorageAsync(catalogStorage);

            _mockServer.SetAction(
                "/packages/listedpackage.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(nupkgStream) }));

            var front = new DurableCursor(_cursorJsonUri, _catalogToDnxStorage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _target.Run(front, back, CancellationToken.None));

            Assert.Equal("The catalog batch 10/13/2015 6:40:07 AM contains multiple entries for the same package identity.  Package(s):  listedpackage 1.0.0", exception.Message);
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
            protected HashSet<Uri> SynchronizedUris { get; private set; }

            public SynchronizedMemoryStorage(IEnumerable<Uri> synchronizedUris)
            {
                SynchronizedUris = new HashSet<Uri>(synchronizedUris);
            }

            protected SynchronizedMemoryStorage(
                Uri baseAddress,
                ConcurrentDictionary<Uri, StorageContent> content,
                ConcurrentDictionary<Uri, byte[]> contentBytes,
                HashSet<Uri> synchronizedUris)
                : base(baseAddress, content, contentBytes)
            {
                SynchronizedUris = synchronizedUris;
            }

            public override Task<bool> AreSynchronized(Uri firstResourceUri, Uri secondResourceUri)
            {
                return Task.FromResult(SynchronizedUris.Contains(firstResourceUri));
            }

            public override Storage WithName(string name)
            {
                return new SynchronizedMemoryStorage(
                    new Uri(BaseAddress + name),
                    Content,
                    ContentBytes,
                    SynchronizedUris);
            }
        }

        private class AzureSynchronizedMemoryStorageStub : SynchronizedMemoryStorage, IAzureStorage
        {
            private readonly bool _areRequiredPropertiesPresentAsync;

            internal AzureSynchronizedMemoryStorageStub(
                IEnumerable<Uri> synchronizedUris,
                bool areRequiredPropertiesPresentAsync)
                : base(synchronizedUris)
            {
                _areRequiredPropertiesPresentAsync = areRequiredPropertiesPresentAsync;
            }

            protected AzureSynchronizedMemoryStorageStub(
                Uri baseAddress,
                ConcurrentDictionary<Uri, StorageContent> content,
                ConcurrentDictionary<Uri, byte[]> contentBytes,
                HashSet<Uri> synchronizedUris,
                bool areRequiredPropertiesPresentAsync)
                : base(baseAddress, content, contentBytes, synchronizedUris)
            {
                _areRequiredPropertiesPresentAsync = areRequiredPropertiesPresentAsync;
            }

            public override Storage WithName(string name)
            {
                return new AzureSynchronizedMemoryStorageStub(
                    new Uri(BaseAddress + name),
                    Content,
                    ContentBytes,
                    SynchronizedUris,
                    _areRequiredPropertiesPresentAsync);
            }

            public Task<ICloudBlockBlob> GetCloudBlockBlobReferenceAsync(Uri blobUri)
            {
                throw new NotImplementedException();
            }

            public Task<ICloudBlockBlob> GetCloudBlockBlobReferenceAsync(string name)
            {
                throw new NotImplementedException();
            }

            public Task<bool> HasPropertiesAsync(Uri blobUri, string contentType, string cacheControl)
            {
                return Task.FromResult(_areRequiredPropertiesPresentAsync);
            }
        }
    }
}
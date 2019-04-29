// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Dnx;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;

namespace CatalogTests.Dnx
{
    public class DnxMakerTests
    {
        private const string _expectedCacheControl = "max-age=120";
        private const string _expectedNuspecContentType = "text/xml";
        private const string _expectedPackageContentType = "application/octet-stream";
        private const string _expectedPackageVersionIndexJsonCacheControl = "no-store";
        private const string _expectedPackageVersionIndexJsonContentType = "application/json";
        private const string _packageId = "testid";
        private const string _nupkgData = "nupkg data";
        private const string _nuspecData = "nuspec data";

        [Fact]
        public void Constructor_WhenStorageFactoryIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new DnxMaker(
                    storageFactory: null,
                    telemetryService: Mock.Of<ITelemetryService>(),
                    logger: Mock.Of<ILogger>()));

            Assert.Equal("storageFactory", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenTelemetryServiceIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new DnxMaker(
                    storageFactory: Mock.Of<StorageFactory>(),
                    telemetryService: null,
                    logger: Mock.Of<ILogger>()));

            Assert.Equal("telemetryService", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new DnxMaker(
                    storageFactory: Mock.Of<StorageFactory>(),
                    telemetryService: Mock.Of<ITelemetryService>(),
                    logger: null));

            Assert.Equal("logger", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetRelativeAddressNupkg_WhenIdIsNullOrEmpty_Throws(string id)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => DnxMaker.GetRelativeAddressNupkg(id, version: "1.0.0"));

            Assert.Equal("id", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetRelativeAddressNupkg_WhenVersionIsNullOrEmpty_Throws(string version)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => DnxMaker.GetRelativeAddressNupkg(id: "a", version: version));

            Assert.Equal("version", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Fact]
        public async Task HasPackageInIndexAsync_WhenStorageIsNull_Throws()
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => maker.HasPackageInIndexAsync(
                    storage: null,
                    id: "a",
                    version: "b",
                    cancellationToken: CancellationToken.None));

            Assert.Equal("storage", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task HasPackageInIndexAsync_WhenIdIsNullOrEmpty_Throws(string id)
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => maker.HasPackageInIndexAsync(
                    new MemoryStorage(),
                    id: id,
                    version: "a",
                    cancellationToken: CancellationToken.None));

            Assert.Equal("id", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task HasPackageInIndexAsync_WhenVersionIsNullOrEmpty_Throws(string version)
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => maker.HasPackageInIndexAsync(
                    new MemoryStorage(),
                    id: "a",
                    version: version,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("version", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Fact]
        public async Task HasPackageInIndexAsync_WhenCancelled_Throws()
        {
            var maker = CreateDnxMaker();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => maker.HasPackageInIndexAsync(
                    new MemoryStorage(),
                    id: "a",
                    version: "b",
                    cancellationToken: new CancellationToken(canceled: true)));
        }

        [Fact]
        public async Task HasPackageInIndexAsync_WhenPackageIdAndVersionDoNotExist_ReturnsFalse()
        {
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            var maker = new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());
            var storageForPackage = (MemoryStorage)catalogToDnxStorageFactory.Create(_packageId);

            var hasPackageInIndex = await maker.HasPackageInIndexAsync(storageForPackage, _packageId, "1.0.0", CancellationToken.None);

            Assert.False(hasPackageInIndex);
        }

        [Fact]
        public async Task HasPackageInIndexAsync_WhenPackageIdExistsButVersionDoesNotExist_ReturnsFalse()
        {
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            var maker = new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());

            await maker.UpdatePackageVersionIndexAsync(_packageId, v => v.Add(NuGetVersion.Parse("1.0.0")), CancellationToken.None);

            var storageForPackage = (MemoryStorage)catalogToDnxStorageFactory.Create(_packageId);

            var hasPackageInIndex = await maker.HasPackageInIndexAsync(storageForPackage, _packageId, "2.0.0", CancellationToken.None);

            Assert.False(hasPackageInIndex);
        }

        [Fact]
        public async Task HasPackageInIndexAsync_WhenPackageIdAndVersionExist_ReturnsTrue()
        {
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            var maker = new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());

            const string version = "1.0.0";

            await maker.UpdatePackageVersionIndexAsync(_packageId, v => v.Add(NuGetVersion.Parse(version)), CancellationToken.None);

            var storageForPackage = (MemoryStorage)catalogToDnxStorageFactory.Create(_packageId);

            var hasPackageInIndex = await maker.HasPackageInIndexAsync(storageForPackage, _packageId, version, CancellationToken.None);

            Assert.True(hasPackageInIndex);
        }

        [Fact]
        public async Task AddPackageAsync_WhenNupkgStreamIsNull_Throws()
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => maker.AddPackageAsync(
                    nupkgStream: null,
                    nuspec: "a",
                    packageId: "b",
                    normalizedPackageVersion: "c",
                    iconFilename: null,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("nupkgStream", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AddPackageAsync_WhenNuspecIsNullOrEmpty_Throws(string nuspec)
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => maker.AddPackageAsync(
                    Stream.Null,
                    nuspec,
                    packageId: "a",
                    normalizedPackageVersion: "b",
                    iconFilename: null,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("nuspec", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AddPackageAsync_WhenPackageIdIsNullOrEmpty_Throws(string packageId)
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => maker.AddPackageAsync(
                    Stream.Null,
                    nuspec: "a",
                    packageId: packageId,
                    normalizedPackageVersion: "b",
                    iconFilename: null,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("packageId", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AddPackageAsync_WhenNormalizedPackageVersionIsNullOrEmpty_Throws(string normalizedPackageVersion)
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => maker.AddPackageAsync(
                    Stream.Null,
                    nuspec: "a",
                    packageId: "b",
                    normalizedPackageVersion: normalizedPackageVersion,
                    iconFilename: null,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("normalizedPackageVersion", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Fact]
        public async Task AddPackageAsync_WhenCancelled_Throws()
        {
            var maker = CreateDnxMaker();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => maker.AddPackageAsync(
                    Stream.Null,
                    nuspec: "a",
                    packageId: "b",
                    normalizedPackageVersion: "c",
                    iconFilename: null,
                    cancellationToken: new CancellationToken(canceled: true)));
        }

        [Theory]
        [MemberData(nameof(PackageVersions))]
        public async Task AddPackageAsync_WithValidVersion_PopulatesStorageWithNupkgAndNuspec(string version)
        {
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            var maker = new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());
            var normalizedVersion = NuGetVersionUtility.NormalizeVersion(version);

            using (var nupkgStream = CreateFakePackageStream(_nupkgData))
            {
                var dnxEntry = await maker.AddPackageAsync(nupkgStream, _nuspecData, _packageId, version, null, CancellationToken.None);

                var expectedNuspec = new Uri($"{catalogToDnxStorage.BaseAddress}{_packageId}/{normalizedVersion}/{_packageId}.nuspec");
                var expectedNupkg = new Uri($"{catalogToDnxStorage.BaseAddress}{_packageId}/{normalizedVersion}/{_packageId}.{normalizedVersion}.nupkg");
                var storageForPackage = (MemoryStorage)catalogToDnxStorageFactory.Create(_packageId);

                Assert.Equal(expectedNuspec, dnxEntry.Nuspec);
                Assert.Equal(expectedNupkg, dnxEntry.Nupkg);
                Assert.Equal(2, catalogToDnxStorage.Content.Count);
                Assert.Equal(2, storageForPackage.Content.Count);

                Verify(catalogToDnxStorage, expectedNupkg, _nupkgData, _expectedCacheControl, _expectedPackageContentType);
                Verify(catalogToDnxStorage, expectedNuspec, _nuspecData, _expectedCacheControl, _expectedNuspecContentType);
                Verify(storageForPackage, expectedNupkg, _nupkgData, _expectedCacheControl, _expectedPackageContentType);
                Verify(storageForPackage, expectedNuspec, _nuspecData, _expectedCacheControl, _expectedNuspecContentType);
            }
        }

        [Theory]
        [InlineData("ahgjghaa.png", "image/png")]
        [InlineData("sdfgd.jpg", "image/jpeg")]
        [InlineData("csdfsd.jpeg", "image/jpeg")]
        public async Task AddPackageAsync_WhenEmbeddedIconPresent_SavesIcon(string iconFilename, string expectedContentType)
        {
            const string version = "1.2.3";
            const string imageContent = "Test image data";
            var imageDataBuffer = Encoding.UTF8.GetBytes(imageContent);

            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            var maker = new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());

            using (var nupkgStream = await CreateNupkgStreamWithIcon(iconFilename, imageDataBuffer))
            {
                await maker.AddPackageAsync(nupkgStream, _nuspecData, _packageId, version, iconFilename, CancellationToken.None);

                var expectedIconUrl = new Uri($"{catalogToDnxStorage.BaseAddress}{_packageId}/{version}/icon");
                Verify(catalogToDnxStorage, expectedIconUrl, imageContent, _expectedCacheControl, expectedContentType);
            }
        }

        [Fact]
        public async Task AddPackageAsync_WithStorage_WhenSourceStorageIsNull_Throws()
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => maker.AddPackageAsync(
                    sourceStorage: null,
                    nuspec: "a",
                    packageId: "b",
                    normalizedPackageVersion: "c",
                    iconFilename: null,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("sourceStorage", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AddPackageAsync_WithStorage_WhenNuspecIsNullOrEmpty_Throws(string nuspec)
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => maker.AddPackageAsync(
                    Mock.Of<IAzureStorage>(),
                    nuspec,
                    packageId: "a",
                    normalizedPackageVersion: "b",
                    iconFilename: null,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("nuspec", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AddPackageAsync_WithStorage_WhenPackageIdIsNullOrEmpty_Throws(string id)
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => maker.AddPackageAsync(
                    Mock.Of<IAzureStorage>(),
                    nuspec: "a",
                    packageId: id,
                    normalizedPackageVersion: "b",
                    iconFilename: null,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("packageId", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AddPackageAsync_WithStorage_WhenNormalizedPackageVersionIsNullOrEmpty_Throws(string version)
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => maker.AddPackageAsync(
                    Mock.Of<IAzureStorage>(),
                    nuspec: "a",
                    packageId: "b",
                    normalizedPackageVersion: version,
                    iconFilename: null,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("normalizedPackageVersion", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Fact]
        public async Task AddPackageAsync_WithStorage_WhenCancelled_Throws()
        {
            var maker = CreateDnxMaker();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => maker.AddPackageAsync(
                    Mock.Of<IAzureStorage>(),
                    nuspec: "a",
                    packageId: "b",
                    normalizedPackageVersion: "c",
                    iconFilename: null,
                    cancellationToken: new CancellationToken(canceled: true)));
        }

        [Theory]
        [MemberData(nameof(PackageVersions))]
        public async Task AddPackageAsync_WithStorage_WithIStorage_PopulatesStorageWithNupkgAndNuspec(string version)
        {
            var catalogToDnxStorage = new AzureStorageStub();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            var maker = new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());
            var normalizedVersion = NuGetVersionUtility.NormalizeVersion(version);
            var sourceStorage = new AzureStorageStub();

            var dnxEntry = await maker.AddPackageAsync(
                sourceStorage,
                _nuspecData,
                _packageId,
                normalizedVersion,
                null,
                CancellationToken.None);

            var expectedNuspecUri = new Uri($"{catalogToDnxStorage.BaseAddress}{_packageId}/{normalizedVersion}/{_packageId}.nuspec");
            var expectedNupkgUri = new Uri($"{catalogToDnxStorage.BaseAddress}{_packageId}/{normalizedVersion}/{_packageId}.{normalizedVersion}.nupkg");
            var expectedSourceUri = new Uri(sourceStorage.BaseAddress, $"{_packageId}.{normalizedVersion}.nupkg");
            var storageForPackage = (MemoryStorage)catalogToDnxStorageFactory.Create(_packageId);

            Assert.Equal(expectedNuspecUri, dnxEntry.Nuspec);
            Assert.Equal(expectedNupkgUri, dnxEntry.Nupkg);
            Assert.Equal(2, catalogToDnxStorage.Content.Count);
            Assert.Equal(2, storageForPackage.Content.Count);

            Verify(catalogToDnxStorage, expectedNupkgUri, expectedSourceUri.AbsoluteUri, _expectedCacheControl, _expectedPackageContentType);
            Verify(catalogToDnxStorage, expectedNuspecUri, _nuspecData, _expectedCacheControl, _expectedNuspecContentType);
            Verify(storageForPackage, expectedNupkgUri, expectedSourceUri.AbsoluteUri, _expectedCacheControl, _expectedPackageContentType);
            Verify(storageForPackage, expectedNuspecUri, _nuspecData, _expectedCacheControl, _expectedNuspecContentType);
        }

        [Theory]
        [InlineData("sdafs.png", "image/png")]
        [InlineData("hjy.jpg", "image/jpeg")]
        [InlineData("vfdg.jpeg", "image/jpeg")]
        public async Task AddPackageAsync_WithStorage_WhenEmbeddedIconPresent_SavesIcon(string iconFilename, string expectedContentType)
        {
            const string version = "1.2.3";
            const string imageContent = "Test image data";
            var imageDataBuffer = Encoding.UTF8.GetBytes(imageContent);

            var catalogToDnxStorage = new AzureStorageStub();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            var maker = new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());
            var sourceStorageMock = new Mock<IAzureStorage>();
            using (var nupkgStream = await CreateNupkgStreamWithIcon(iconFilename, imageDataBuffer))
            {
                var cloudBlobMock = new Mock<ICloudBlockBlob>();
                cloudBlobMock
                    .Setup(cb => cb.GetStreamAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(nupkgStream);

                sourceStorageMock
                    .Setup(ss => ss.GetCloudBlockBlobReferenceAsync(It.IsAny<Uri>()))
                    .ReturnsAsync(cloudBlobMock.Object);

                await maker.AddPackageAsync(sourceStorageMock.Object, _nuspecData, _packageId, version, iconFilename, CancellationToken.None);

                var expectedIconUrl = new Uri($"{catalogToDnxStorage.BaseAddress}{_packageId}/{version}/icon");
                Verify(catalogToDnxStorage, expectedIconUrl, imageContent, _expectedCacheControl, expectedContentType);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task DeletePackageAsync_WhenIdIsNullOrEmpty_Throws(string id)
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => maker.DeletePackageAsync(
                    id: id,
                    version: "a",
                    cancellationToken: CancellationToken.None));

            Assert.Equal("id", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task DeletePackageAsync_WhenVersionIsNullOrEmpty_Throws(string version)
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => maker.DeletePackageAsync(
                    id: "a",
                    version: version,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("version", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Fact]
        public async Task DeletePackageAsync_WhenCancelled_Throws()
        {
            var maker = CreateDnxMaker();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => maker.DeletePackageAsync(
                    id: "a",
                    version: "b",
                    cancellationToken: new CancellationToken(canceled: true)));
        }

        [Theory]
        [MemberData(nameof(PackageVersions))]
        public async Task DeletePackageAsync_WithValidVersion_RemovesNupkgAndNuspecFromStorage(string version)
        {
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            var maker = new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());

            using (var nupkgStream = CreateFakePackageStream(_nupkgData))
            {
                var dnxEntry = await maker.AddPackageAsync(nupkgStream, _nuspecData, _packageId, version, null, CancellationToken.None);

                var storageForPackage = (MemoryStorage)catalogToDnxStorageFactory.Create(_packageId);

                Assert.Equal(2, catalogToDnxStorage.Content.Count);
                Assert.Equal(2, storageForPackage.Content.Count);

                await maker.DeletePackageAsync(_packageId, version, CancellationToken.None);

                Assert.Equal(0, catalogToDnxStorage.Content.Count);
                Assert.Equal(0, storageForPackage.Content.Count);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task UpdatePackageVersionIndexAsync_WhenIdIsNullOrEmpty_Throws(string id)
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => maker.UpdatePackageVersionIndexAsync(
                    id: id,
                    updateAction: _ => { },
                    cancellationToken: CancellationToken.None));

            Assert.Equal("id", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Fact]
        public async Task UpdatePackageVersionIndexAsync_WhenVersionIsNullOrEmpty_Throws()
        {
            var maker = CreateDnxMaker();

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => maker.UpdatePackageVersionIndexAsync(
                    id: "a",
                    updateAction: null,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("updateAction", exception.ParamName);
        }

        [Fact]
        public async Task UpdatePackageVersionIndexAsync_WhenCancelled_Throws()
        {
            var maker = CreateDnxMaker();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => maker.UpdatePackageVersionIndexAsync(
                    id: "a",
                    updateAction: _ => { },
                    cancellationToken: new CancellationToken(canceled: true)));
        }

        [Theory]
        [MemberData(nameof(PackageVersions))]
        public async Task UpdatePackageVersionIndexAsync_WithValidVersion_CreatesIndex(string version)
        {
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            var maker = new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());
            var normalizedVersion = NuGetVersionUtility.NormalizeVersion(version);

            await maker.UpdatePackageVersionIndexAsync(_packageId, v => v.Add(NuGetVersion.Parse(version)), CancellationToken.None);

            var storageForPackage = (MemoryStorage)catalogToDnxStorageFactory.Create(_packageId);
            var indexJsonUri = new Uri(storageForPackage.BaseAddress, "index.json");
            var indexJson = await storageForPackage.LoadAsync(indexJsonUri, CancellationToken.None);
            var indexObject = JObject.Parse(indexJson.GetContentString());
            var versions = indexObject["versions"].ToObject<string[]>();
            var expectedContent = GetExpectedIndexJsonContent(normalizedVersion);

            Assert.Equal(1, catalogToDnxStorage.Content.Count);
            Assert.Equal(1, storageForPackage.Content.Count);

            Verify(catalogToDnxStorage, indexJsonUri, expectedContent, _expectedPackageVersionIndexJsonCacheControl, _expectedPackageVersionIndexJsonContentType);
            Verify(storageForPackage, indexJsonUri, expectedContent, _expectedPackageVersionIndexJsonCacheControl, _expectedPackageVersionIndexJsonContentType);

            Assert.Equal(new[] { normalizedVersion }, versions);
        }

        [Fact]
        public async Task UpdatePackageVersionIndexAsync_WhenLastVersionRemoved_RemovesIndex()
        {
            var version = NuGetVersion.Parse("1.0.0");
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            var maker = new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());

            await maker.UpdatePackageVersionIndexAsync(_packageId, v => v.Add(version), CancellationToken.None);

            var storageForPackage = (MemoryStorage)catalogToDnxStorageFactory.Create(_packageId);
            var indexJsonUri = new Uri(storageForPackage.BaseAddress, "index.json");
            var indexJson = await storageForPackage.LoadAsync(indexJsonUri, CancellationToken.None);

            Assert.NotNull(indexJson);
            Assert.Equal(1, catalogToDnxStorage.Content.Count);
            Assert.Equal(1, storageForPackage.Content.Count);

            await maker.UpdatePackageVersionIndexAsync(_packageId, v => v.Remove(version), CancellationToken.None);

            indexJson = await storageForPackage.LoadAsync(indexJsonUri, CancellationToken.None);

            Assert.Null(indexJson);
            Assert.Equal(0, catalogToDnxStorage.Content.Count);
            Assert.Equal(0, storageForPackage.Content.Count);
        }

        [Fact]
        public async Task UpdatePackageVersionIndexAsync_WithNoVersions_DoesNotCreateIndex()
        {
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            var maker = new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());

            await maker.UpdatePackageVersionIndexAsync(_packageId, v => { }, CancellationToken.None);

            var storageForPackage = (MemoryStorage)catalogToDnxStorageFactory.Create(_packageId);
            var indexJsonUri = new Uri(storageForPackage.BaseAddress, "index.json");
            var indexJson = await storageForPackage.LoadAsync(indexJsonUri, CancellationToken.None);

            Assert.Null(indexJson);
            Assert.Equal(0, catalogToDnxStorage.Content.Count);
            Assert.Equal(0, storageForPackage.Content.Count);
        }

        [Fact]
        public async Task UpdatePackageVersionIndexAsync_WithMultipleVersions_SortsVersion()
        {
            var unorderedVersions = new[]
            {
                NuGetVersion.Parse("3.0.0"),
                NuGetVersion.Parse("1.1.0"),
                NuGetVersion.Parse("1.0.0"),
                NuGetVersion.Parse("1.0.1"),
                NuGetVersion.Parse("2.0.0"),
                NuGetVersion.Parse("1.0.2")
            };
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));
            var maker = new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());

            await maker.UpdatePackageVersionIndexAsync(_packageId, v => v.UnionWith(unorderedVersions), CancellationToken.None);

            var storageForPackage = (MemoryStorage)catalogToDnxStorageFactory.Create(_packageId);
            var indexJsonUri = new Uri(storageForPackage.BaseAddress, "index.json");
            var indexJson = await storageForPackage.LoadAsync(indexJsonUri, CancellationToken.None);
            var indexObject = JObject.Parse(indexJson.GetContentString());
            var versions = indexObject["versions"].ToObject<string[]>();

            Assert.Equal(1, catalogToDnxStorage.Content.Count);
            Assert.Equal(1, storageForPackage.Content.Count);
            Assert.Collection(
                versions,
                version => Assert.Equal(unorderedVersions[2].ToNormalizedString(), version),
                version => Assert.Equal(unorderedVersions[3].ToNormalizedString(), version),
                version => Assert.Equal(unorderedVersions[5].ToNormalizedString(), version),
                version => Assert.Equal(unorderedVersions[1].ToNormalizedString(), version),
                version => Assert.Equal(unorderedVersions[4].ToNormalizedString(), version),
                version => Assert.Equal(unorderedVersions[0].ToNormalizedString(), version));
        }

        public static IEnumerable<object[]> PackageVersions
        {
            get
            {
                // normalized versions
                yield return new object[] { "1.2.0" };
                yield return new object[] { "0.1.2" };
                yield return new object[] { "1.2.3.4" };
                yield return new object[] { "1.2.3-beta1" };
                yield return new object[] { "1.2.3-beta.1" };

                // non-normalized versions
                yield return new object[] { "1.2" };
                yield return new object[] { "1.2.3.0" };
                yield return new object[] { "1.02.3" };
            }
        }

        private static DnxMaker CreateDnxMaker()
        {
            var catalogToDnxStorage = new MemoryStorage();
            var catalogToDnxStorageFactory = new TestStorageFactory(name => catalogToDnxStorage.WithName(name));

            return new DnxMaker(catalogToDnxStorageFactory, Mock.Of<ITelemetryService>(), Mock.Of<ILogger>());
        }

        private static MemoryStream CreateFakePackageStream(string content)
        {
            var stream = new MemoryStream();

            using (var writer = new StreamWriter(stream, new UTF8Encoding(), bufferSize: 4096, leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();
            }

            stream.Position = 0;

            return stream;
        }

        private static string GetExpectedIndexJsonContent(string version)
        {
            return $"{{\r\n  \"versions\": [\r\n    \"{version}\"\r\n  ]\r\n}}";
        }

        private static void Verify(
            MemoryStorage storage,
            Uri uri,
            string expectedContent,
            string expectedCacheControl,
            string expectedContentType)
        {
            Assert.True(storage.Content.TryGetValue(uri, out var content));
            Assert.Equal(expectedCacheControl, content.CacheControl);
            Assert.Equal(expectedContentType, content.ContentType);

            Assert.True(storage.ContentBytes.TryGetValue(uri, out var bytes));
            Assert.Equal(Encoding.UTF8.GetBytes(expectedContent), bytes);

            var isExpected = storage.BaseAddress != new Uri("http://tempuri.org/");

            Assert.Equal(isExpected, storage.ListMock.TryGetValue(uri, out var list));

            if (isExpected)
            {
                Assert.Equal(uri, list.Uri);

                var utc = DateTime.UtcNow;
                Assert.NotNull(list.LastModifiedUtc);
                Assert.InRange(list.LastModifiedUtc.Value, utc.AddMinutes(-1), utc);
            }
        }

        private static async Task<Stream> CreateNupkgStreamWithIcon(string iconFilename, byte[] imageDataBuffer)
        {
            var nupkgStream = new MemoryStream();
            using (var archive = new ZipArchive(nupkgStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry(iconFilename);
                using (var entryStream = entry.Open())
                {
                    await entryStream.WriteAsync(imageDataBuffer, 0, imageDataBuffer.Length);
                }
            }

            nupkgStream.Seek(0, SeekOrigin.Begin);
            return nupkgStream;
        }

        private sealed class AzureStorageStub : MemoryStorage, IAzureStorage
        {
            internal AzureStorageStub()
            {
            }

            private AzureStorageStub(
                Uri baseAddress,
                ConcurrentDictionary<Uri, StorageContent> content,
                ConcurrentDictionary<Uri, byte[]> contentBytes)
                : base(baseAddress, content, contentBytes)
            {
            }

            public override Storage WithName(string name)
            {
                return new AzureStorageStub(
                    new Uri(BaseAddress + name),
                    Content,
                    ContentBytes);
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
                throw new NotImplementedException();
            }

            protected override Task OnCopyAsync(
                Uri sourceUri,
                IStorage destinationStorage,
                Uri destinationUri,
                IReadOnlyDictionary<string, string> destinationProperties,
                CancellationToken cancellationToken)
            {
                var destinationMemoryStorage = (AzureStorageStub)destinationStorage;

                string cacheControl = null;
                string contentType = null;

                destinationProperties?.TryGetValue(StorageConstants.CacheControl, out cacheControl);
                destinationProperties?.TryGetValue(StorageConstants.ContentType, out contentType);

                destinationMemoryStorage.Content[destinationUri] = new StringStorageContent(sourceUri.AbsoluteUri, contentType, cacheControl);
                destinationMemoryStorage.ContentBytes[destinationUri] = Encoding.UTF8.GetBytes(sourceUri.AbsoluteUri);
                destinationMemoryStorage.ListMock[destinationUri] = new StorageListItem(destinationUri, DateTime.UtcNow);

                return Task.FromResult(0);
            }
        }
    }
}
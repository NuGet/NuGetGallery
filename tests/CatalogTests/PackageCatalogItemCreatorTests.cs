// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests
{
    public class PackageCatalogItemCreatorTests
    {
        private const string _packageHash = "bq5DjCtCJpy9R5rsEeQlKz8qGF1Bh3wGaJKMlRwmCoKZ8WUCIFtU3JlyMOdAkSn66KCehCCAxMZFOQD4nNnH/w==";

        private static readonly MockTelemetryService _telemetryService = new MockTelemetryService();

        [Fact]
        public void Create_WhenHttpClientIsNull_Throws()
        {
            HttpClient httpClient = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => PackageCatalogItemCreator.Create(
                    httpClient,
                    Mock.Of<ITelemetryService>(),
                    Mock.Of<ILogger>(),
                    Mock.Of<IStorage>()));

            Assert.Equal("httpClient", exception.ParamName);
        }

        [Fact]
        public void Create_WhenTelemetryServiceIsNull_Throws()
        {
            ITelemetryService telemetryService = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => PackageCatalogItemCreator.Create(
                    Mock.Of<HttpClient>(),
                    telemetryService,
                    Mock.Of<ILogger>(),
                    Mock.Of<IStorage>()));

            Assert.Equal("telemetryService", exception.ParamName);
        }

        [Fact]
        public void Create_WhenLoggerIsNull_Throws()
        {
            ILogger logger = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => PackageCatalogItemCreator.Create(
                    Mock.Of<HttpClient>(),
                    Mock.Of<ITelemetryService>(),
                    logger,
                    Mock.Of<IStorage>()));

            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public void Create_WhenStorageIsNull_ReturnsInstance()
        {
            var creator = PackageCatalogItemCreator.Create(
                Mock.Of<HttpClient>(),
                Mock.Of<ITelemetryService>(),
                Mock.Of<ILogger>(),
                storage: null);

            Assert.NotNull(creator);
        }

        [Fact]
        public async Task CreateAsync_WhenPackageItemIsNull_Throws()
        {
            var creator = PackageCatalogItemCreator.Create(
                Mock.Of<HttpClient>(),
                _telemetryService,
                Mock.Of<ILogger>(),
                storage: null);

            FeedPackageDetails packageItem = null;

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => creator.CreateAsync(packageItem, DateTime.UtcNow, CancellationToken.None));

            Assert.Equal("packageItem", exception.ParamName);
        }

        [Fact]
        public async Task CreateAsync_WhenCancellationTokenIsCancelled_Throws()
        {
            var creator = PackageCatalogItemCreator.Create(
                Mock.Of<HttpClient>(),
                _telemetryService,
                Mock.Of<ILogger>(),
                storage: null);
            var packageItem = new FeedPackageDetails(
                new Uri("https://nuget.test"),
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow,
                packageId: "a",
                packageNormalizedVersion: "1.0.0",
                packageFullVersion: "1.0.0");

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => creator.CreateAsync(packageItem, DateTime.UtcNow, new CancellationToken(canceled: true)));
        }

        public class WhenStorageIsNotIAzureStorage
        {
            [Fact]
            public async Task CreateAsync_WhenHttpPackageSourceReturnsPackage_ReturnsInstance()
            {
                using (var test = new Test(useStorage: false))
                {
                    var item = await test.Creator.CreateAsync(test.FeedPackageDetails, test.Timestamp, CancellationToken.None);

                    AssertCorrect(item, test.FeedPackageDetails);

                    Assert.Equal(1, test.TelemetryService.TrackDurationCalls.Count);

                    var call = test.TelemetryService.TrackDurationCalls[0];

                    Assert.Equal("PackageDownloadSeconds", call.Name);
                    Assert.Equal(2, call.Properties.Count);
                    Assert.Equal(test.PackageId.ToLowerInvariant(), call.Properties["Id"]);
                    Assert.Equal(test.PackageVersion.ToLowerInvariant(), call.Properties["Version"]);
                }
            }
        }

        public class WhenStorageIsIAzureStorage
        {
            [Fact]
            public async Task CreateAsync_WhenAzureStorageReturnsNonexistantBlob_ReturnsInstanceFromHttpPackageSource()
            {
                using (var test = new Test())
                {
                    test.Storage.Setup(x => x.ResolveUri(test.PackageFileName))
                        .Returns(test.ContentUri);

                    test.Storage.Setup(x => x.GetCloudBlockBlobReferenceAsync(
                            It.Is<Uri>(uri => uri == test.ContentUri)))
                        .ReturnsAsync(test.Blob.Object);

                    test.Blob.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(false);

                    test.Blob.SetupGet(x => x.Uri)
                        .Returns(test.ContentUri);

                    var item = await test.Creator.CreateAsync(
                        test.FeedPackageDetails,
                        test.Timestamp,
                        CancellationToken.None);

                    AssertCorrect(item, test.FeedPackageDetails);
                    Assert.Equal(1, test.Handler.Requests.Count);

                    Assert.Equal(1, test.TelemetryService.TrackMetricCalls.Count);

                    var call = test.TelemetryService.TrackMetricCalls[0];

                    Assert.Equal("NonExistentBlob", call.Name);
                    Assert.Equal(1UL, call.Metric);
                    Assert.Equal(3, call.Properties.Count);
                    Assert.Equal(test.PackageId.ToLowerInvariant(), call.Properties["Id"]);
                    Assert.Equal(test.PackageVersion.ToLowerInvariant(), call.Properties["Version"]);
                    Assert.Equal(test.ContentUri.AbsoluteUri, call.Properties["Uri"]);
                }
            }

            [Fact]
            public async Task CreateAsync_WhenBlobDoesNotHavePackageHash_ReturnsInstanceFromHttpPackageSource()
            {
                using (var test = new Test())
                {
                    test.Storage.Setup(x => x.ResolveUri(test.PackageFileName))
                        .Returns(test.ContentUri);

                    test.Storage.Setup(x => x.GetCloudBlockBlobReferenceAsync(
                            It.Is<Uri>(uri => uri == test.ContentUri)))
                        .ReturnsAsync(test.Blob.Object);

                    test.Blob.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    test.Blob.Setup(x => x.FetchAttributesAsync(It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult(0));

                    test.Blob.SetupGet(x => x.ETag)
                        .Returns("0");

                    test.Blob.Setup(x => x.GetMetadataAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Dictionary<string, string>());

                    test.Blob.SetupGet(x => x.Uri)
                        .Returns(test.ContentUri);

                    var item = await test.Creator.CreateAsync(
                        test.FeedPackageDetails,
                        test.Timestamp,
                        CancellationToken.None);

                    AssertCorrect(item, test.FeedPackageDetails);
                    Assert.Equal(1, test.Handler.Requests.Count);

                    Assert.Equal(1, test.TelemetryService.TrackMetricCalls.Count);

                    var call = test.TelemetryService.TrackMetricCalls[0];

                    Assert.Equal("NonExistentPackageHash", call.Name);
                    Assert.Equal(1UL, call.Metric);
                    Assert.Equal(3, call.Properties.Count);
                    Assert.Equal(test.PackageId.ToLowerInvariant(), call.Properties["Id"]);
                    Assert.Equal(test.PackageVersion.ToLowerInvariant(), call.Properties["Version"]);
                    Assert.Equal(test.ContentUri.AbsoluteUri, call.Properties["Uri"]);
                }
            }

            [Fact]
            public async Task CreateAsync_WhenBlobHasPackageHash_ReturnsInstanceFromBlobStorage()
            {
                using (var test = new Test())
                {
                    test.Storage.Setup(x => x.ResolveUri(test.PackageFileName))
                        .Returns(test.ContentUri);

                    test.Storage.Setup(x => x.GetCloudBlockBlobReferenceAsync(
                            It.Is<Uri>(uri => uri == test.ContentUri)))
                        .ReturnsAsync(test.Blob.Object);

                    test.Blob.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    test.Blob.Setup(x => x.FetchAttributesAsync(It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult(0));

                    test.Blob.SetupGet(x => x.ETag)
                        .Returns("0");

                    test.Blob.Setup(x => x.GetMetadataAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Dictionary<string, string>()
                        {
                            { "SHA512", _packageHash }
                        });

                    test.Blob.Setup(x => x.GetStreamAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(TestHelper.GetStream(test.PackageFileName + ".testdata"));

                    test.Blob.Setup(x => x.FetchAttributesAsync(It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult(0));

                    test.Blob.SetupGet(x => x.ETag)
                        .Returns("0");

                    var item = await test.Creator.CreateAsync(
                        test.FeedPackageDetails,
                        test.Timestamp,
                        CancellationToken.None);

                    AssertCorrect(item, test.FeedPackageDetails);
                    Assert.Empty(test.Handler.Requests);
                    Assert.Empty(test.TelemetryService.TrackMetricCalls);
                }
            }

            [Fact]
            public async Task CreateAsync_WhenBlobChangesBetweenReads_ReturnsInstanceFromHttpPackageSource()
            {
                using (var test = new Test())
                {
                    test.Storage.Setup(x => x.ResolveUri(test.PackageFileName))
                        .Returns(test.ContentUri);

                    test.Storage.Setup(x => x.GetCloudBlockBlobReferenceAsync(
                            It.Is<Uri>(uri => uri == test.ContentUri)))
                        .ReturnsAsync(test.Blob.Object);

                    test.Blob.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    test.Blob.Setup(x => x.FetchAttributesAsync(It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult(0));

                    test.Blob.SetupSequence(x => x.ETag)
                        .Returns("0")
                        .Returns("1");

                    test.Blob.Setup(x => x.GetMetadataAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Dictionary<string, string>()
                        {
                            { "SHA512", _packageHash }
                        });

                    test.Blob.Setup(x => x.GetStreamAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(TestHelper.GetStream(test.PackageFileName + ".testdata"));

                    test.Blob.Setup(x => x.FetchAttributesAsync(It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult(0));

                    test.Blob.SetupGet(x => x.Uri)
                        .Returns(test.ContentUri);

                    var item = await test.Creator.CreateAsync(
                        test.FeedPackageDetails,
                        test.Timestamp,
                        CancellationToken.None);

                    AssertCorrect(item, test.FeedPackageDetails);
                    Assert.Single(test.Handler.Requests);

                    Assert.Single(test.TelemetryService.TrackMetricCalls);

                    var call = test.TelemetryService.TrackMetricCalls[0];

                    Assert.Equal("BlobModified", call.Name);
                    Assert.Equal(1UL, call.Metric);
                    Assert.Equal(3, call.Properties.Count);
                    Assert.Equal(test.PackageId.ToLowerInvariant(), call.Properties["Id"]);
                    Assert.Equal(test.PackageVersion.ToLowerInvariant(), call.Properties["Version"]);
                    Assert.Equal(test.ContentUri.AbsoluteUri, call.Properties["Uri"]);
                }
            }
        }

        private static void AssertCorrect(PackageCatalogItem item, FeedPackageDetails feedPackageDetails)
        {
            Assert.NotNull(item);
            Assert.Equal(18, item.NupkgMetadata.Entries.Count());
            Assert.NotNull(item.NupkgMetadata.Nuspec);
            Assert.Equal(_packageHash, item.NupkgMetadata.PackageHash);
            Assert.Equal(1871318, item.NupkgMetadata.PackageSize);
            Assert.Equal(feedPackageDetails.CreatedDate, item.CreatedDate);
            Assert.Equal(feedPackageDetails.LastEditedDate, item.LastEditedDate);
            Assert.Equal(feedPackageDetails.PublishedDate, item.PublishedDate);
        }

        private sealed class Test : IDisposable
        {
            private readonly HttpClient _httpClient;
            private bool _isDisposed;

            internal Mock<ICloudBlockBlob> Blob { get; }
            internal Uri ContentUri { get; }
            internal PackageCatalogItemCreator Creator { get; }
            internal FeedPackageDetails FeedPackageDetails { get; }
            internal MockServerHttpClientHandler Handler { get; }
            internal string PackageFileName { get; }
            internal string PackageId => "Newtonsoft.Json";
            internal Uri PackageUri { get; }
            internal string PackageVersion => "9.0.2-beta1";
            internal Mock<IAzureStorage> Storage { get; }
            internal MockTelemetryService TelemetryService { get; }
            internal DateTime Timestamp { get; }

            internal Test(bool useStorage = true)
            {
                Handler = new MockServerHttpClientHandler();
                _httpClient = new HttpClient(Handler);

                PackageFileName = $"{PackageId.ToLowerInvariant()}.{PackageVersion.ToLowerInvariant()}.nupkg";
                Timestamp = DateTime.UtcNow;
                ContentUri = new Uri($"https://nuget.test/packages/{PackageFileName}");
                PackageUri = Utilities.GetNugetCacheBustingUri(ContentUri, Timestamp.ToString("O"));
                Storage = new Mock<IAzureStorage>(MockBehavior.Strict);
                Blob = new Mock<ICloudBlockBlob>(MockBehavior.Strict);
                FeedPackageDetails = new FeedPackageDetails(
                    ContentUri,
                    Timestamp.AddHours(-3),
                    Timestamp.AddHours(-2),
                    Timestamp.AddHours(-1),
                    PackageId,
                    PackageVersion,
                    PackageVersion);

                var stream = TestHelper.GetStream(PackageFileName + ".testdata");

                Handler.SetAction(
                    PackageUri.PathAndQuery,
                    request => Task.FromResult(
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StreamContent(stream)
                        }));

                TelemetryService = new MockTelemetryService();

                Creator = PackageCatalogItemCreator.Create(
                    _httpClient,
                    TelemetryService,
                    Mock.Of<ILogger>(),
                    useStorage ? Storage.Object : null);
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    Blob.VerifyAll();
                    Storage.VerifyAll();

                    Handler.Dispose();
                    _httpClient.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }
        }
    }
}
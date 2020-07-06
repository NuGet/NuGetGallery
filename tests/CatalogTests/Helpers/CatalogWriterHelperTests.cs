// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Helpers
{
    public class CatalogWriterHelperTests
    {
        public class TheWritePackageDetailsToCatalogAsyncMethod
        {
            [Fact]
            public async Task WhenPackageCatalogItemCreatorIsNull_Throws()
            {
                IPackageCatalogItemCreator creator = null;

                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => CatalogWriterHelper.WritePackageDetailsToCatalogAsync(
                        creator,
                        new SortedList<DateTime, IList<FeedPackageDetails>>(),
                        Mock.Of<IStorage>(),
                        DateTime.MinValue,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        maxDegreeOfParallelism: 1,
                        createdPackages: null,
                        updateCreatedFromEdited: false,
                        cancellationToken: CancellationToken.None,
                        telemetryService: Mock.Of<ITelemetryService>(),
                        logger: Mock.Of<ILogger>()));

                Assert.Equal("packageCatalogItemCreator", exception.ParamName);
            }

            [Fact]
            public async Task WhenPackagesIsNull_Throws()
            {
                SortedList<DateTime, IList<FeedPackageDetails>> packages = null;

                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => CatalogWriterHelper.WritePackageDetailsToCatalogAsync(
                        Mock.Of<IPackageCatalogItemCreator>(),
                        packages,
                        Mock.Of<IStorage>(),
                        DateTime.MinValue,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        maxDegreeOfParallelism: 1,
                        createdPackages: null,
                        updateCreatedFromEdited: false,
                        cancellationToken: CancellationToken.None,
                        telemetryService: Mock.Of<ITelemetryService>(),
                        logger: Mock.Of<ILogger>()));

                Assert.Equal("packages", exception.ParamName);
            }

            [Fact]
            public async Task WhenStorageIsNull_Throws()
            {
                IStorage storage = null;

                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => CatalogWriterHelper.WritePackageDetailsToCatalogAsync(
                        Mock.Of<IPackageCatalogItemCreator>(),
                        new SortedList<DateTime, IList<FeedPackageDetails>>(),
                        storage,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        maxDegreeOfParallelism: 1,
                        createdPackages: null,
                        updateCreatedFromEdited: false,
                        cancellationToken: CancellationToken.None,
                        telemetryService: Mock.Of<ITelemetryService>(),
                        logger: Mock.Of<ILogger>()));

                Assert.Equal("storage", exception.ParamName);
            }

            [Theory]
            [InlineData(-1)]
            [InlineData(0)]
            public async Task WhenMaxDegreeOfParallelismIsOutOfRange_Throws(int maxDegreeOfParallelism)
            {
                var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => CatalogWriterHelper.WritePackageDetailsToCatalogAsync(
                        Mock.Of<IPackageCatalogItemCreator>(),
                        new SortedList<DateTime, IList<FeedPackageDetails>>(),
                        Mock.Of<IStorage>(),
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        maxDegreeOfParallelism,
                        createdPackages: false,
                        updateCreatedFromEdited: false,
                        cancellationToken: CancellationToken.None,
                        telemetryService: Mock.Of<ITelemetryService>(),
                        logger: Mock.Of<ILogger>()));

                Assert.Equal("maxDegreeOfParallelism", exception.ParamName);
                Assert.StartsWith($"The argument must be within the range from 1 (inclusive) to {int.MaxValue} (inclusive).", exception.Message);
            }

            [Fact]
            public async Task WhenTelemetryServiceIsNull_Throws()
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => CatalogWriterHelper.WritePackageDetailsToCatalogAsync(
                        Mock.Of<IPackageCatalogItemCreator>(),
                        new SortedList<DateTime, IList<FeedPackageDetails>>(),
                        Mock.Of<IStorage>(),
                        DateTime.MinValue,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        maxDegreeOfParallelism: 1,
                        createdPackages: null,
                        updateCreatedFromEdited: false,
                        cancellationToken: CancellationToken.None,
                        telemetryService: null,
                        logger: Mock.Of<ILogger>()));

                Assert.Equal("telemetryService", exception.ParamName);
            }

            [Fact]
            public async Task WhenLoggerIsNull_Throws()
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => CatalogWriterHelper.WritePackageDetailsToCatalogAsync(
                        Mock.Of<IPackageCatalogItemCreator>(),
                        new SortedList<DateTime, IList<FeedPackageDetails>>(),
                        Mock.Of<IStorage>(),
                        DateTime.MinValue,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        maxDegreeOfParallelism: 1,
                        createdPackages: null,
                        updateCreatedFromEdited: false,
                        cancellationToken: CancellationToken.None,
                        telemetryService: Mock.Of<ITelemetryService>(),
                        logger: null));

                Assert.Equal("logger", exception.ParamName);
            }

            [Fact]
            public async Task WhenCancellationTokenIsCancelled_Throws()
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => CatalogWriterHelper.WritePackageDetailsToCatalogAsync(
                        Mock.Of<IPackageCatalogItemCreator>(),
                        new SortedList<DateTime, IList<FeedPackageDetails>>(),
                        Mock.Of<IStorage>(),
                        DateTime.MinValue,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        maxDegreeOfParallelism: 1,
                        createdPackages: null,
                        updateCreatedFromEdited: false,
                        cancellationToken: new CancellationToken(canceled: true),
                        telemetryService: Mock.Of<ITelemetryService>(),
                        logger: Mock.Of<ILogger>()));
            }

            [Fact]
            public async Task WhenCreatedPackagesIsNull_WithNoPackages_ReturnsDateTimeMinValue()
            {
                using (var test = new WritePackageDetailsToCatalogAsyncTest())
                {
                    test.CreatedPackages = null;

                    var result = await test.WritePackageDetailsToCatalogAsync();

                    Assert.Equal(DateTime.MinValue, result);
                }
            }

            [Fact]
            public async Task WhenCreatedPackagesIsFalse_WithNoPackages_ReturnsLastEdited()
            {
                using (var test = new WritePackageDetailsToCatalogAsyncTest())
                {
                    test.CreatedPackages = false;

                    var result = await test.WritePackageDetailsToCatalogAsync();

                    Assert.Equal(test.LastEdited, result);
                }
            }

            [Fact]
            public async Task WhenCreatedPackagesIsTrue_WithNoPackages_ReturnsLastCreated()
            {
                using (var test = new WritePackageDetailsToCatalogAsyncTest())
                {
                    test.CreatedPackages = true;

                    var result = await test.WritePackageDetailsToCatalogAsync();

                    Assert.Equal(test.LastCreated, result);
                }
            }

            public static IEnumerable<object[]> WithOnePackage_UpdatesStorage_Data
            {
                get
                {
                    foreach (var createdPackages in new[] { false, true })
                    {
                        foreach (var updateCreatedFromEdited in new[] { false, true })
                        {
                            foreach (var deprecationState in Enum.GetValues(typeof(PackageDeprecationItemState)).Cast<PackageDeprecationItemState>())
                            {
                                for (var vulnerabilityCount = 0; vulnerabilityCount < 3; vulnerabilityCount++)
                                {
                                    yield return new object[] { createdPackages, updateCreatedFromEdited, deprecationState, vulnerabilityCount };
                                }
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(WithOnePackage_UpdatesStorage_Data))]
            public async Task WithOnePackage_UpdatesStorage(bool createdPackages, bool updateCreatedFromEdited, PackageDeprecationItemState deprecationState, int vulnerabilityCount)
            {
                // Arrange
                using (var test = new WritePackageDetailsToCatalogAsyncTest())
                {
                    test.CreatedPackages = createdPackages;
                    test.UpdateCreatedFromEdited = updateCreatedFromEdited;
                    test.Packages.Add(test.UtcNow, new List<FeedPackageDetails>()
                    {
                        test.FeedPackageDetails
                    });

                    NupkgMetadata nupkgMetadata = GetNupkgMetadata("Newtonsoft.Json.9.0.2-beta1.nupkg");

                    var deprecationItem = GetPackageDeprecationItemFromState(deprecationState);
                    var vulnerabilities = CreateTestPackageVulnerabilityItems(vulnerabilityCount);
                    var packageCatalogItem = new PackageCatalogItem(
                        nupkgMetadata,
                        test.FeedPackageDetails.CreatedDate,
                        test.FeedPackageDetails.LastEditedDate,
                        test.FeedPackageDetails.PublishedDate,
                        deprecation: deprecationItem,
                        vulnerabilities: vulnerabilities);

                    test.PackageCatalogItemCreator.Setup(x => x.CreateAsync(
                            It.Is<FeedPackageDetails>(details => details == test.FeedPackageDetails),
                            It.Is<DateTime>(timestamp => timestamp == test.UtcNow),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(packageCatalogItem);

                    test.Storage.SetupGet(x => x.BaseAddress)
                        .Returns(test.CatalogBaseUri);

                    var blobs = new List<CatalogBlob>();

                    test.Storage.Setup(x => x.SaveAsync(
                            It.IsNotNull<Uri>(),
                            It.IsNotNull<StorageContent>(),
                            It.IsAny<CancellationToken>()))
                        .Callback<Uri, StorageContent, CancellationToken>((uri, content, token) =>
                        {
                            blobs.Add(new CatalogBlob(uri, content));
                        })
                        .Returns(Task.FromResult(0));

                    test.Storage.Setup(x => x.LoadStringAsync(
                            It.Is<Uri>(uri => uri == test.CatalogIndexUri),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(CatalogTestData.GetBeforeIndex(test.CatalogIndexUri).ToString());

                    test.TelemetryService.Setup(x => x.TrackCatalogIndexWriteDuration(
                        It.Is<TimeSpan>(duration => duration > TimeSpan.Zero),
                        It.Is<Uri>(uri => uri == test.CatalogIndexUri)));

                    // Act
                    var result = await test.WritePackageDetailsToCatalogAsync();

                    // Assert
                    Assert.Equal(test.UtcNow, result);

                    Assert.Equal(3, blobs.Count);

                    var catalogPackageDetailsUri = new Uri($"{test.CatalogBaseUri}data/{packageCatalogItem.TimeStamp.ToString("yyyy.MM.dd.HH.mm.ss")}/newtonsoft.json.9.0.2-beta1.json");
                    var catalogPageUri = new Uri($"{test.CatalogBaseUri}page0.json");

                    // Verify package details blob
                    Assert.Equal(catalogPackageDetailsUri, blobs[0].Uri);
                    Assert.IsType<StringStorageContent>(blobs[0].Content);

                    var stringContent = (StringStorageContent)blobs[0].Content;

                    Assert.Equal("no-store", stringContent.CacheControl);
                    Assert.Equal("application/json", stringContent.ContentType);

                    var expectedContent = CatalogTestData.GetPackageDetails(
                        catalogPackageDetailsUri,
                        packageCatalogItem.CommitId,
                        packageCatalogItem.TimeStamp,
                        packageCatalogItem.CreatedDate.Value,
                        packageCatalogItem.LastEditedDate.Value,
                        packageCatalogItem.PublishedDate.Value,
                        deprecationItem,
                        vulnerabilities);

                    var actualContent = JObject.Parse(stringContent.Content);

                    Assert.Equal(expectedContent.ToString(), actualContent.ToString());

                    // Verify page blob
                    Assert.Equal(catalogPageUri, blobs[1].Uri);
                    Assert.IsType<JTokenStorageContent>(blobs[1].Content);

                    var jtokenContent = (JTokenStorageContent)blobs[1].Content;

                    expectedContent = CatalogTestData.GetPage(
                        catalogPageUri,
                        packageCatalogItem.CommitId,
                        packageCatalogItem.TimeStamp,
                        test.CatalogIndexUri,
                        catalogPackageDetailsUri);

                    Assert.Equal("no-store", jtokenContent.CacheControl);
                    Assert.Equal("application/json", jtokenContent.ContentType);
                    Assert.Equal(expectedContent.ToString(), jtokenContent.Content.ToString());

                    // Verify index blob
                    Assert.Equal(test.CatalogIndexUri, blobs[2].Uri);
                    Assert.IsType<JTokenStorageContent>(blobs[2].Content);

                    jtokenContent = (JTokenStorageContent)blobs[2].Content;

                    var lastEdited = createdPackages ? test.LastEdited : test.UtcNow;
                    var lastCreated = updateCreatedFromEdited ? lastEdited : (createdPackages ? test.UtcNow : test.LastCreated);

                    expectedContent = CatalogTestData.GetAfterIndex(
                        test.CatalogIndexUri,
                        packageCatalogItem.CommitId,
                        packageCatalogItem.TimeStamp,
                        lastCreated,
                        test.LastDeleted,
                        lastEdited,
                        catalogPageUri);

                    Assert.Equal("no-store", jtokenContent.CacheControl);
                    Assert.Equal("application/json", jtokenContent.ContentType);
                    Assert.Equal(expectedContent.ToString(), jtokenContent.Content.ToString());
                }
            }
        }

        private sealed class WritePackageDetailsToCatalogAsyncTest : IDisposable
        {
            private bool _isDisposed;

            internal DateTime UtcNow { get; }
            internal DateTime LastCreated { get; }
            internal DateTime LastEdited { get; }
            internal DateTime LastDeleted { get; }
            internal bool? CreatedPackages { get; set; }
            internal bool UpdateCreatedFromEdited { get; set; }
            internal Mock<IPackageCatalogItemCreator> PackageCatalogItemCreator { get; }
            internal SortedList<DateTime, IList<FeedPackageDetails>> Packages { get; }
            internal Mock<IStorage> Storage { get; }
            internal Mock<ITelemetryService> TelemetryService { get; }
            internal Mock<ILogger> Logger { get; }
            internal FeedPackageDetails FeedPackageDetails { get; }
            internal Uri CatalogBaseUri { get; }
            internal Uri CatalogIndexUri { get; }

            internal WritePackageDetailsToCatalogAsyncTest()
            {
                UtcNow = DateTime.UtcNow;
                LastCreated = UtcNow.AddHours(-3);
                LastEdited = UtcNow.AddHours(-2);
                LastDeleted = UtcNow.AddHours(-1);

                Packages = new SortedList<DateTime, IList<FeedPackageDetails>>();

                PackageCatalogItemCreator = new Mock<IPackageCatalogItemCreator>(MockBehavior.Strict);
                Storage = new Mock<IStorage>(MockBehavior.Strict);
                TelemetryService = new Mock<ITelemetryService>(MockBehavior.Strict);
                Logger = new Mock<ILogger>();

                CatalogBaseUri = new Uri("https://nuget.test/v3-catalog0/");
                CatalogIndexUri = new Uri(CatalogBaseUri, "index.json");

                Storage.Setup(x => x.ResolveUri(
                        It.Is<string>(relativeUri => relativeUri == "index.json")))
                    .Returns(CatalogIndexUri);

                FeedPackageDetails = new FeedPackageDetails(
                    new Uri("https://nuget.test/packages/a"),
                    UtcNow.AddMinutes(-45),
                    UtcNow.AddMinutes(-30),
                    UtcNow.AddMinutes(-15),
                    packageId: "a",
                    packageNormalizedVersion: "1.0.0",
                    packageFullVersion: "1.0.0.0");
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    PackageCatalogItemCreator.VerifyAll();
                    Storage.VerifyAll();
                    TelemetryService.VerifyAll();
                    Logger.VerifyAll();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }

            internal Task<DateTime> WritePackageDetailsToCatalogAsync()
            {
                const int maxDegreeOfParallelism = 1;

                return CatalogWriterHelper.WritePackageDetailsToCatalogAsync(
                  PackageCatalogItemCreator.Object,
                  Packages,
                  Storage.Object,
                  LastCreated,
                  LastEdited,
                  LastDeleted,
                  maxDegreeOfParallelism,
                  CreatedPackages,
                  UpdateCreatedFromEdited,
                  CancellationToken.None,
                  TelemetryService.Object,
                  Logger.Object);
            }
        }

        private sealed class CatalogBlob
        {
            internal Uri Uri { get; }
            internal StorageContent Content { get; }

            internal CatalogBlob(Uri uri, StorageContent content)
            {
                Uri = uri;
                Content = content;
            }
        }

        public enum PackageDeprecationItemState
        {
            NotDeprecated,
            DeprecatedWithSingleReason,
            DeprecatedWithMessage,
            DeprecatedWithAlternate
        }

        private static PackageDeprecationItem GetPackageDeprecationItemFromState(PackageDeprecationItemState deprecationState)
        {
            if (deprecationState == PackageDeprecationItemState.NotDeprecated)
            {
                return null;
            }

            var reasons = new[] { "first", "second" };
            string message = null;
            string altId = null;
            string altVersion = null;
            if (deprecationState == PackageDeprecationItemState.DeprecatedWithSingleReason)
            {
                reasons = reasons.Take(1).ToArray();
            }

            if (deprecationState == PackageDeprecationItemState.DeprecatedWithMessage)
            {
                message = "this is the message";
            }

            if (deprecationState == PackageDeprecationItemState.DeprecatedWithAlternate)
            {
                altId = "theId";
                altVersion = "[2.4.5, 2.4.5]";
            }

            return new PackageDeprecationItem(reasons, message, altId, altVersion);
        }

        private static IList<PackageVulnerabilityItem> CreateTestPackageVulnerabilityItems(int vulnerabilityCount)
        {
            if (vulnerabilityCount == 0)
            {
                return null;
            }

            var vulnerabilities = new List<PackageVulnerabilityItem>();
            for (int i = 0; i < vulnerabilityCount; i++)
            {
                vulnerabilities.Add(new PackageVulnerabilityItem("" + 100 + i, "http://www.foo.com/advisory" + i + ".html", "" + i));
            }

            return vulnerabilities;
        }

        private static NupkgMetadata GetNupkgMetadata(string resourceName)
        {
            using (var stream = TestHelper.GetStream(resourceName))
            {
                return Utils.GetNupkgMetadata(stream, packageHash: null);
            }
        }
    }
}
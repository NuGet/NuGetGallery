// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Catalog;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.V3;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class AzureSearchCollectorLogicFacts
    {
        public class CreateBatchesAsync : BaseFacts
        {
            public CreateBatchesAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SingleBatchWithAllItems()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: null,
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri>(),
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                    new CatalogCommitItem(
                        uri: null,
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 2),
                        types: null,
                        typeUris: new List<Uri>(),
                        packageIdentity: new PackageIdentity("NuGet.Frameworks", NuGetVersion.Parse("2.0.0"))),
                };

                var batches = await _target.CreateBatchesAsync(items);

                var batch = Assert.Single(batches);
                Assert.Equal(2, batch.Items.Count);
                Assert.Equal(new DateTime(2018, 1, 2), batch.CommitTimeStamp);
                Assert.Equal(items[0], batch.Items[0]);
                Assert.Equal(items[1], batch.Items[1]);
            }

            [Fact]
            public async Task ReturnsEmptyItemLIsResultsInEmptyBatchList()
            {
                var items = new CatalogCommitItem[0];

                var batches = await _target.CreateBatchesAsync(items);

                Assert.Empty(batches);
            }
        }

        public class OnProcessBatchAsync : BaseFacts
        {
            public OnProcessBatchAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task DoesNotFetchLeavesForDeleteEntries()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                    new CatalogCommitItem(
                        uri: new Uri("https://example/1"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 2),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDelete },
                        packageIdentity: new PackageIdentity("NuGet.Frameworks", NuGetVersion.Parse("2.0.0"))),
                };

                await _target.OnProcessBatchAsync(items);

                _catalogClient.Verify(x => x.GetPackageDetailsLeafAsync("https://example/0"), Times.Once);
                _catalogClient.Verify(x => x.GetPackageDetailsLeafAsync(It.IsAny<string>()), Times.Exactly(1));
                _catalogClient.Verify(x => x.GetPackageDeleteLeafAsync(It.IsAny<string>()), Times.Never);

                _catalogIndexActionBuilder.Verify(
                    x => x.AddCatalogEntriesAsync(
                        "NuGet.Versioning",
                        It.Is<IReadOnlyList<CatalogCommitItem>>(
                            y => y.Count == 1),
                        It.Is<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>>(
                            y => y.Count == 1)),
                    Times.Once);
                _catalogIndexActionBuilder.Verify(
                    x => x.AddCatalogEntriesAsync(
                        "NuGet.Frameworks",
                        It.Is<IReadOnlyList<CatalogCommitItem>>(
                            y => y.Count == 1),
                        It.Is<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>>(
                            y => y.Count == 0)),
                    Times.Once);

                _batchPusher.Verify(
                    x => x.EnqueueIndexActions("NuGet.Versioning", It.IsAny<IndexActions>()),
                    Times.Once);
                _batchPusher.Verify(
                    x => x.EnqueueIndexActions("NuGet.Frameworks", It.IsAny<IndexActions>()),
                    Times.Once);
                _batchPusher.Verify(
                    x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()),
                    Times.Exactly(2));

                _batchPusher.Verify(x => x.TryFinishAsync(), Times.Once);
            }

            [Fact]
            public async Task OperatesOnLatestPerPackageIdentityAndGroupsById()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                    new CatalogCommitItem(
                        uri: new Uri("https://example/1"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 2),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                    new CatalogCommitItem(
                        uri: new Uri("https://example/2"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 2),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("2.0.0"))),
                    new CatalogCommitItem(
                        uri: new Uri("https://example/3"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 2),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Frameworks", NuGetVersion.Parse("1.0.0"))),
                };

                await _target.OnProcessBatchAsync(items);

                _catalogClient.Verify(x => x.GetPackageDetailsLeafAsync("https://example/1"), Times.Once);
                _catalogClient.Verify(x => x.GetPackageDetailsLeafAsync("https://example/2"), Times.Once);
                _catalogClient.Verify(x => x.GetPackageDetailsLeafAsync("https://example/3"), Times.Once);
                _catalogClient.Verify(x => x.GetPackageDetailsLeafAsync(It.IsAny<string>()), Times.Exactly(3));

                _catalogIndexActionBuilder.Verify(
                    x => x.AddCatalogEntriesAsync(
                        "NuGet.Versioning",
                        It.Is<IReadOnlyList<CatalogCommitItem>>(
                            y => y.Count == 2),
                        It.Is<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>>(
                            y => y.Count == 2)),
                    Times.Once);
                _catalogIndexActionBuilder.Verify(
                    x => x.AddCatalogEntriesAsync(
                        "NuGet.Frameworks",
                        It.Is<IReadOnlyList<CatalogCommitItem>>(
                            y => y.Count == 1),
                        It.Is<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>>(
                            y => y.Count == 1)),
                    Times.Once);

                _batchPusher.Verify(
                    x => x.EnqueueIndexActions("NuGet.Versioning", It.IsAny<IndexActions>()),
                    Times.Once);
                _batchPusher.Verify(
                    x => x.EnqueueIndexActions("NuGet.Frameworks", It.IsAny<IndexActions>()),
                    Times.Once);
                _batchPusher.Verify(
                    x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()),
                    Times.Exactly(2));

                _batchPusher.Verify(x => x.TryFinishAsync(), Times.Once);
            }

            [Fact]
            public async Task RejectsMultipleLeavesForTheSamePackageAtTheSameTime()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                    new CatalogCommitItem(
                        uri: new Uri("https://example/1"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                };

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.OnProcessBatchAsync(items));

                Assert.Equal(
                    "There are multiple catalog leaves for a single package at one time.",
                    ex.Message);
                _batchPusher.Verify(
                    x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()),
                    Times.Never);
            }

            [Fact]
            public async Task DoesNotCallFixUpEvaluatorForWhenExceptionTypeDoesNotMatch()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                };

                var otherException = new ArgumentException("Not so fast, buddy.");
                _batchPusher
                    .Setup(x => x.TryFinishAsync())
                    .ThrowsAsync(otherException);

                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => _target.OnProcessBatchAsync(items));

                Assert.Same(otherException, ex);
                _fixUpEvaluator.Verify(
                    x => x.TryFixUpAsync(
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<ConcurrentBag<IdAndValue<IndexActions>>>(),
                        It.IsAny<InvalidOperationException>()),
                    Times.Never);
                _batchPusher.Verify(x => x.TryFinishAsync(), Times.Once);
            }

            [Fact]
            public async Task ThrowsOriginalExceptionIfFixUpIsNotApplicable()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                };

                var otherException = new InvalidOperationException("Not so fast, buddy.");
                _batchPusher
                    .Setup(x => x.TryFinishAsync())
                    .ThrowsAsync(otherException);

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.OnProcessBatchAsync(items));

                Assert.Same(otherException, ex);
                _fixUpEvaluator.Verify(
                    x => x.TryFixUpAsync(
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<ConcurrentBag<IdAndValue<IndexActions>>>(),
                        It.IsAny<InvalidOperationException>()),
                    Times.Once);
                _batchPusher.Verify(x => x.TryFinishAsync(), Times.Once);
            }

            [Fact]
            public async Task ThrowsOriginalExceptionIfFixFailsThreeTimes()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                };

                var otherException = new InvalidOperationException("Not so fast, buddy.");
                _batchPusher
                    .Setup(x => x.TryFinishAsync())
                    .ThrowsAsync(otherException);
                _fixUpEvaluator
                    .Setup(x => x.TryFixUpAsync(
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<ConcurrentBag<IdAndValue<IndexActions>>>(),
                        It.IsAny<InvalidOperationException>()))
                    .ReturnsAsync(() => DocumentFixUp.IsApplicable(new List<CatalogCommitItem>()));

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.OnProcessBatchAsync(items));

                Assert.Same(otherException, ex);
                _fixUpEvaluator.Verify(
                    x => x.TryFixUpAsync(
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<ConcurrentBag<IdAndValue<IndexActions>>>(),
                        It.IsAny<InvalidOperationException>()),
                    Times.Exactly(2));
                _batchPusher.Verify(x => x.TryFinishAsync(), Times.Exactly(3));
            }

            [Fact]
            public async Task ThrowsExceptionIfPackageIdsFailThreeTimes()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                };

                _batchPusher
                    .Setup(x => x.TryFinishAsync())
                    .ReturnsAsync(new BatchPusherResult(new[] { "NuGet.Versioning" }));

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.OnProcessBatchAsync(items));

                Assert.Equal("The index operations for the following package IDs failed due to version list concurrency: NuGet.Versioning", ex.Message);
                _fixUpEvaluator.Verify(
                    x => x.TryFixUpAsync(
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<ConcurrentBag<IdAndValue<IndexActions>>>(),
                        It.IsAny<InvalidOperationException>()),
                    Times.Never);
                _batchPusher.Verify(x => x.TryFinishAsync(), Times.Exactly(3));
            }

            [Fact]
            public async Task ThrowsOriginalExceptionWithMixOfFixUpAndFailedPackageIds()
            {
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                };

                var otherException = new InvalidOperationException("Not so fast, buddy.");
                _batchPusher
                    .SetupSequence(x => x.TryFinishAsync())
                    .ThrowsAsync(new InvalidOperationException())
                    .ReturnsAsync(new BatchPusherResult(new[] { "NuGet.Versioning" }))
                    .ThrowsAsync(otherException);
                _fixUpEvaluator
                    .Setup(x => x.TryFixUpAsync(
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<ConcurrentBag<IdAndValue<IndexActions>>>(),
                        It.IsAny<InvalidOperationException>()))
                    .ReturnsAsync(() => DocumentFixUp.IsApplicable(new List<CatalogCommitItem>()));

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.OnProcessBatchAsync(items));

                Assert.Same(otherException, ex);
                _fixUpEvaluator.Verify(
                    x => x.TryFixUpAsync(
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<ConcurrentBag<IdAndValue<IndexActions>>>(),
                        It.IsAny<InvalidOperationException>()),
                    Times.Once);
                _batchPusher.Verify(x => x.TryFinishAsync(), Times.Exactly(3));
            }

            [Fact]
            public async Task RetriesOfFixUpAndFailedPackageIdsCanSucceedWithinRetries()
            {
                var itemsA = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                };

                var itemsB = new List<CatalogCommitItem>
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/1"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2019, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                };

                var otherException = new InvalidOperationException("Not so fast, buddy.");
                _batchPusher
                    .SetupSequence(x => x.TryFinishAsync())
                    .ReturnsAsync(new BatchPusherResult(new[] { "NuGet.Versioning" }))
                    .ThrowsAsync(new InvalidOperationException())
                    .ReturnsAsync(new BatchPusherResult());
                _fixUpEvaluator
                    .Setup(x => x.TryFixUpAsync(
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<ConcurrentBag<IdAndValue<IndexActions>>>(),
                        It.IsAny<InvalidOperationException>()))
                    .ReturnsAsync(() => DocumentFixUp.IsApplicable(new List<CatalogCommitItem>(itemsB)));

                await _target.OnProcessBatchAsync(itemsA);

                _fixUpEvaluator.Verify(
                    x => x.TryFixUpAsync(
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<ConcurrentBag<IdAndValue<IndexActions>>>(),
                        It.IsAny<InvalidOperationException>()),
                    Times.Once);
                _batchPusher.Verify(x => x.TryFinishAsync(), Times.Exactly(3));

                _catalogClient.Verify(x => x.GetPackageDetailsLeafAsync("https://example/0"), Times.Exactly(2));
                _catalogClient.Verify(x => x.GetPackageDetailsLeafAsync("https://example/1"), Times.Once);
            }

            [Fact]
            public async Task UsesFixUpCommitItems()
            {
                var itemsA = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/0"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                };

                var itemsB = new List<CatalogCommitItem>
                {
                    new CatalogCommitItem(
                        uri: new Uri("https://example/1"),
                        commitId: null,
                        commitTimeStamp: new DateTime(2019, 1, 1),
                        types: null,
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"))),
                };

                var otherException = new InvalidOperationException("Not so fast, buddy.");
                _batchPusher
                    .Setup(x => x.TryFinishAsync())
                    .ThrowsAsync(otherException);
                _fixUpEvaluator
                    .Setup(x => x.TryFixUpAsync(
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<ConcurrentBag<IdAndValue<IndexActions>>>(),
                        It.IsAny<InvalidOperationException>()))
                    .ReturnsAsync(() => DocumentFixUp.IsApplicable(itemsB));

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.OnProcessBatchAsync(itemsA));

                _catalogClient.Verify(x => x.GetPackageDetailsLeafAsync("https://example/0"), Times.Once);
                _catalogClient.Verify(x => x.GetPackageDetailsLeafAsync("https://example/1"), Times.Exactly(2));
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<ICatalogClient> _catalogClient;
            protected readonly Mock<ICatalogIndexActionBuilder> _catalogIndexActionBuilder;
            protected readonly Mock<IBatchPusher> _batchPusher;
            protected readonly Mock<IDocumentFixUpEvaluator> _fixUpEvaluator;
            protected readonly CommitCollectorUtility _utility;
            protected readonly Mock<IOptionsSnapshot<CommitCollectorConfiguration>> _utilityOptions;
            protected readonly Mock<IOptionsSnapshot<Catalog2AzureSearchConfiguration>> _collectorOptions;
            protected readonly CommitCollectorConfiguration _utilityConfig;
            protected readonly Catalog2AzureSearchConfiguration _collectorConfig;
            protected readonly Mock<IAzureSearchTelemetryService> _telemetryService;
            protected readonly Mock<IV3TelemetryService> _v3TelemetryService;
            protected readonly RecordingLogger<AzureSearchCollectorLogic> _logger;
            protected readonly RecordingLogger<CommitCollectorUtility> _utilityLogger;
            protected readonly AzureSearchCollectorLogic _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _catalogClient = new Mock<ICatalogClient>();
                _catalogIndexActionBuilder = new Mock<ICatalogIndexActionBuilder>();
                _batchPusher = new Mock<IBatchPusher>();
                _fixUpEvaluator = new Mock<IDocumentFixUpEvaluator>();
                _utilityOptions = new Mock<IOptionsSnapshot<CommitCollectorConfiguration>>();
                _collectorOptions = new Mock<IOptionsSnapshot<Catalog2AzureSearchConfiguration>>();
                _utilityConfig = new CommitCollectorConfiguration();
                _collectorConfig = new Catalog2AzureSearchConfiguration();
                _telemetryService = new Mock<IAzureSearchTelemetryService>();
                _v3TelemetryService = new Mock<IV3TelemetryService>();
                _logger = output.GetLogger<AzureSearchCollectorLogic>();
                _utilityLogger = output.GetLogger<CommitCollectorUtility>();

                _batchPusher.SetReturnsDefault(Task.FromResult(new BatchPusherResult()));
                _utilityOptions.Setup(x => x.Value).Returns(() => _utilityConfig);
                _utilityConfig.MaxConcurrentCatalogLeafDownloads = 1;
                _collectorOptions.Setup(x => x.Value).Returns(() => _collectorConfig);
                _collectorConfig.MaxConcurrentBatches = 1;
                _fixUpEvaluator
                    .Setup(x => x.TryFixUpAsync(
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<ConcurrentBag<IdAndValue<IndexActions>>>(),
                        It.IsAny<InvalidOperationException>()))
                    .ReturnsAsync(() => DocumentFixUp.IsNotApplicable());

                _utility = new CommitCollectorUtility(
                    _catalogClient.Object,
                    _v3TelemetryService.Object,
                    _utilityOptions.Object,
                    _utilityLogger);

                _target = new AzureSearchCollectorLogic(
                    _catalogIndexActionBuilder.Object,
                    () => _batchPusher.Object,
                    _fixUpEvaluator.Object,
                    _utility,
                    _collectorOptions.Object,
                    _telemetryService.Object,
                    _logger);
            }
        }

    }
}

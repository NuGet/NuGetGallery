// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.Metadata.Catalog;
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

                _batchPusher.Verify(x => x.FinishAsync(), Times.Once);
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

                _batchPusher.Verify(x => x.FinishAsync(), Times.Once);
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
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<ICatalogClient> _catalogClient;
            protected readonly Mock<ICatalogIndexActionBuilder> _catalogIndexActionBuilder;
            protected readonly Mock<IBatchPusher> _batchPusher;
            protected readonly Mock<IOptionsSnapshot<Catalog2AzureSearchConfiguration>> _options;
            protected readonly Catalog2AzureSearchConfiguration _config;
            protected readonly RecordingLogger<AzureSearchCollectorLogic> _logger;
            protected readonly AzureSearchCollectorLogic _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _catalogClient = new Mock<ICatalogClient>();
                _catalogIndexActionBuilder = new Mock<ICatalogIndexActionBuilder>();
                _batchPusher = new Mock<IBatchPusher>();
                _options = new Mock<IOptionsSnapshot<Catalog2AzureSearchConfiguration>>();
                _config = new Catalog2AzureSearchConfiguration();
                _logger = output.GetLogger<AzureSearchCollectorLogic>();

                _options.Setup(x => x.Value).Returns(() => _config);
                _config.MaxConcurrentBatches = 1;

                _target = new AzureSearchCollectorLogic(
                    _catalogClient.Object,
                    _catalogIndexActionBuilder.Object,
                    () => _batchPusher.Object,
                    _options.Object,
                    _logger);
            }
        }

    }
}

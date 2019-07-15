// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.Entities;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class Db2AzureSearchCommandFacts
    {
        private readonly Mock<INewPackageRegistrationProducer> _producer;
        private readonly Mock<IPackageEntityIndexActionBuilder> _builder;
        private readonly Mock<IBlobContainerBuilder> _blobContainerBuilder;
        private readonly Mock<IIndexBuilder> _indexBuilder;
        private readonly Mock<IBatchPusher> _batchPusher;
        private readonly Mock<ICatalogClient> _catalogClient;
        private readonly Mock<IStorageFactory> _storageFactory;
        private readonly Mock<IOwnerDataClient> _ownerDataClient;
        private readonly Mock<IDownloadDataClient> _downloadDataClient;
        private readonly Mock<IOptionsSnapshot<Db2AzureSearchConfiguration>> _options;
        private readonly Db2AzureSearchConfiguration _config;
        private readonly TestCursorStorage _storage;
        private readonly RecordingLogger<Db2AzureSearchCommand> _logger;
        private readonly Db2AzureSearchCommand _target;

        public Db2AzureSearchCommandFacts(ITestOutputHelper output)
        {
            _producer = new Mock<INewPackageRegistrationProducer>();
            _builder = new Mock<IPackageEntityIndexActionBuilder>();
            _blobContainerBuilder = new Mock<IBlobContainerBuilder>();
            _indexBuilder = new Mock<IIndexBuilder>();
            _batchPusher = new Mock<IBatchPusher>();
            _catalogClient = new Mock<ICatalogClient>();
            _storageFactory = new Mock<IStorageFactory>();
            _ownerDataClient = new Mock<IOwnerDataClient>();
            _downloadDataClient = new Mock<IDownloadDataClient>();
            _options = new Mock<IOptionsSnapshot<Db2AzureSearchConfiguration>>();
            _logger = output.GetLogger<Db2AzureSearchCommand>();

            _config = new Db2AzureSearchConfiguration
            {
                MaxConcurrentBatches = 1,
                StorageContainer = "container-name",
            };
            _storage = new TestCursorStorage(new Uri("https://example/base/"));

            _options
                .Setup(x => x.Value)
                .Returns(() => _config);
            _builder
                .Setup(x => x.AddNewPackageRegistration(It.IsAny<NewPackageRegistration>()))
                .Returns(() => new IndexActions(
                    new IndexAction<KeyedDocument>[0],
                    new IndexAction<KeyedDocument>[0],
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        AccessConditionWrapper.GenerateEmptyCondition())));
            _catalogClient
                .Setup(x => x.GetIndexAsync(It.IsAny<string>()))
                .ReturnsAsync(new CatalogIndex());
            _storageFactory
                .Setup(x => x.Create(It.IsAny<string>()))
                .Returns(() => _storage);
            _blobContainerBuilder
                .Setup(x => x.DeleteIfExistsAsync())
                .ReturnsAsync(true);

            _target = new Db2AzureSearchCommand(
                _producer.Object,
                _builder.Object,
                _blobContainerBuilder.Object,
                _indexBuilder.Object,
                () => _batchPusher.Object,
                _catalogClient.Object,
                _storageFactory.Object,
                _ownerDataClient.Object,
                _downloadDataClient.Object,
                _options.Object,
                _logger);
        }

        [Fact]
        public async Task SavesCatalogCommitTimestamp()
        {
            var initial = new DateTimeOffset(2017, 1, 1, 12, 0, 0, TimeSpan.FromHours(4));
            _catalogClient
                .Setup(x => x.GetIndexAsync(It.IsAny<string>()))
                .ReturnsAsync(new CatalogIndex { CommitTimestamp = initial });

            await _target.ExecuteAsync();

            Assert.Equal(new DateTime(2017, 1, 1, 8, 0, 0), _storage.CursorValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ObservesReplaceIndexesAndContainersOption(bool replace)
        {
            _config.ReplaceContainersAndIndexes = replace;
            var replaceTimes = replace ? Times.Once() : Times.Never();
            var retryOnConflict = replace;

            await _target.ExecuteAsync();

            _blobContainerBuilder.Verify(x => x.DeleteIfExistsAsync(), replaceTimes);
            _indexBuilder.Verify(x => x.DeleteSearchIndexIfExistsAsync(), replaceTimes);
            _indexBuilder.Verify(x => x.DeleteHijackIndexIfExistsAsync(), replaceTimes);
            _blobContainerBuilder.Verify(x => x.CreateAsync(retryOnConflict), Times.Once);
            _indexBuilder.Verify(x => x.CreateSearchIndexAsync(), Times.Once);
            _indexBuilder.Verify(x => x.CreateHijackIndexAsync(), Times.Once);
        }

        [Fact]
        public async Task PushesToIndexesUsingMaximumBatchSize()
        {
            _config.AzureSearchBatchSize = 2;
            _producer
                .Setup(x => x.ProduceWorkAsync(It.IsAny<ConcurrentBag<NewPackageRegistration>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<ConcurrentBag<NewPackageRegistration>, CancellationToken>((w, _) =>
                {
                    w.Add(new NewPackageRegistration("A", 0, new string[0], new Package[0]));
                    w.Add(new NewPackageRegistration("B", 0, new string[0], new Package[0]));
                    w.Add(new NewPackageRegistration("C", 0, new string[0], new Package[0]));
                    w.Add(new NewPackageRegistration("D", 0, new string[0], new Package[0]));
                    w.Add(new NewPackageRegistration("E", 0, new string[0], new Package[0]));
                });
            _builder
                .Setup(x => x.AddNewPackageRegistration(It.IsAny<NewPackageRegistration>()))
                .Returns<NewPackageRegistration>(x => new IndexActions(
                    new List<IndexAction<KeyedDocument>> { IndexAction.Upload(new KeyedDocument { Key = x.PackageId }) },
                    new List<IndexAction<KeyedDocument>>(),
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        AccessConditionWrapper.GenerateEmptyCondition())));

            var enqueuedIndexActions = new List<KeyValuePair<string, IndexActions>>();
            _batchPusher
                .Setup(x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()))
                .Callback<string, IndexActions>((id, actions) =>
                {
                    enqueuedIndexActions.Add(KeyValuePair.Create(id, actions));
                });

            await _target.ExecuteAsync();

            Assert.Equal(5, enqueuedIndexActions.Count);
            var keys = enqueuedIndexActions
                .Select(x => x.Key)
                .OrderBy(x => x)
                .ToArray();
            Assert.Equal(
                new[] { "A", "B", "C", "D", "E" },
                keys);

            _batchPusher.Verify(x => x.PushFullBatchesAsync(), Times.Exactly(5));
            _batchPusher.Verify(x => x.FinishAsync(), Times.Once);
        }

        [Fact]
        public async Task DoesNotEnqueueChangesForNoIndexActions()
        {
            _config.AzureSearchBatchSize = 2;
            _producer
                .Setup(x => x.ProduceWorkAsync(It.IsAny<ConcurrentBag<NewPackageRegistration>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<ConcurrentBag<NewPackageRegistration>, CancellationToken>((w, _) =>
                {
                    w.Add(new NewPackageRegistration("A", 0, new[] { "Microsoft", "EntityFramework" }, new Package[0]));
                    w.Add(new NewPackageRegistration("B", 0, new[] { "nuget" }, new Package[0]));
                    w.Add(new NewPackageRegistration("C", 0, new[] { "aspnet" }, new Package[0]));
                });

            // Return empty index action for ID "B". This package ID will not be pushed to Azure Search but will appear
            // in the initial owners data file.
            _builder
                .Setup(x => x.AddNewPackageRegistration(It.Is<NewPackageRegistration>(y => y.PackageId != "B")))
                .Returns<NewPackageRegistration>(x => new IndexActions(
                    new List<IndexAction<KeyedDocument>> { IndexAction.Upload(new KeyedDocument { Key = x.PackageId }) },
                    new List<IndexAction<KeyedDocument>>(),
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        AccessConditionWrapper.GenerateEmptyCondition())));
            _builder
                .Setup(x => x.AddNewPackageRegistration(It.Is<NewPackageRegistration>(y => y.PackageId == "B")))
                .Returns<NewPackageRegistration>(x => new IndexActions(
                    new List<IndexAction<KeyedDocument>>(),
                    new List<IndexAction<KeyedDocument>>(),
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        AccessConditionWrapper.GenerateEmptyCondition())));

            var enqueuedIndexActions = new List<KeyValuePair<string, IndexActions>>();
            _batchPusher
                .Setup(x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()))
                .Callback<string, IndexActions>((id, actions) =>
                {
                    enqueuedIndexActions.Add(KeyValuePair.Create(id, actions));
                });

            SortedDictionary<string, SortedSet<string>> data = null;
            _ownerDataClient
                .Setup(x => x.ReplaceLatestIndexedAsync(It.IsAny<SortedDictionary<string, SortedSet<string>>>(), It.IsAny<IAccessCondition>()))
                .Returns(Task.CompletedTask)
                .Callback<SortedDictionary<string, SortedSet<string>>, IAccessCondition>((d, _) => data = d);

            await _target.ExecuteAsync();

            Assert.Equal(2, enqueuedIndexActions.Count);
            var keys = enqueuedIndexActions
                .Select(x => x.Key)
                .OrderBy(x => x)
                .ToArray();
            Assert.Equal(
                new[] { "A", "C" },
                keys);

            Assert.Equal(new[] { "A", "B", "C" }, data.Keys.ToArray());
            Assert.Equal(new[] { "EntityFramework", "Microsoft" }, data["A"].ToArray());
            Assert.Equal(new[] { "nuget" }, data["B"].ToArray());
            Assert.Equal(new[] { "aspnet" }, data["C"].ToArray());
        }

        [Fact]
        public async Task PushesOwnerData()
        {
            _producer
                .Setup(x => x.ProduceWorkAsync(It.IsAny<ConcurrentBag<NewPackageRegistration>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<ConcurrentBag<NewPackageRegistration>, CancellationToken>((w, _) =>
                {
                    w.Add(new NewPackageRegistration("A", 0, new[] { "Microsoft", "EntityFramework" }, new Package[0]));
                    w.Add(new NewPackageRegistration("B", 0, new string[0], new Package[0]));
                    w.Add(new NewPackageRegistration("C", 0, new[] { "nuget" }, new Package[0]));
                });

            SortedDictionary<string, SortedSet<string>> data = null;
            IAccessCondition accessCondition = null;
            _ownerDataClient
                .Setup(x => x.ReplaceLatestIndexedAsync(It.IsAny<SortedDictionary<string, SortedSet<string>>>(), It.IsAny<IAccessCondition>()))
                .Returns(Task.CompletedTask)
                .Callback<SortedDictionary<string, SortedSet<string>>, IAccessCondition>((d, a) =>
               {
                   data = d;
                   accessCondition = a;
               });

            await _target.ExecuteAsync();

            Assert.Equal(new[] { "A", "C" }, data.Keys.ToArray());
            Assert.Equal(new[] { "EntityFramework", "Microsoft" }, data["A"].ToArray());
            Assert.Equal(new[] { "nuget" }, data["C"].ToArray());

            Assert.Equal("*", accessCondition.IfNoneMatchETag);
            Assert.Null(accessCondition.IfMatchETag);

            _ownerDataClient.Verify(
                x => x.ReplaceLatestIndexedAsync(It.IsAny<SortedDictionary<string, SortedSet<string>>>(), It.IsAny<IAccessCondition>()),
                Times.Once);
        }

        [Fact]
        public async Task PushesDownloadData()
        {
            _producer
                .Setup(x => x.ProduceWorkAsync(It.IsAny<ConcurrentBag<NewPackageRegistration>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<ConcurrentBag<NewPackageRegistration>, CancellationToken>((w, _) =>
                {
                    w.Add(new NewPackageRegistration(
                        "A",
                        0,
                        new string[0],
                        new Package[0]));
                    w.Add(new NewPackageRegistration(
                        "B",
                        0,
                        new string[0],
                        new[]
                        {
                            new Package { NormalizedVersion = "1.0.0", DownloadCount = 23 },
                        }));
                    w.Add(new NewPackageRegistration(
                        "C",
                        0,
                        new string[0],
                        new[]
                        {
                            new Package { NormalizedVersion = "1.0.0", DownloadCount = 42 },
                            new Package { NormalizedVersion = "2.0.0-ALPHA", DownloadCount = 43 },
                        }));
                });

            DownloadData data = null;
            IAccessCondition accessCondition = null;
            _downloadDataClient
                .Setup(x => x.ReplaceLatestIndexedAsync(It.IsAny<DownloadData>(), It.IsAny<IAccessCondition>()))
                .Returns(Task.CompletedTask)
                .Callback<DownloadData, IAccessCondition>((d, a) =>
                {
                    data = d;
                    accessCondition = a;
                });

            await _target.ExecuteAsync();

            Assert.Equal(new[] { "B", "C" }, data.Keys.ToArray());
            Assert.Equal(23, data.GetDownloadCount("B", "1.0.0"));
            Assert.Equal(42, data.GetDownloadCount("C", "1.0.0"));
            Assert.Equal(43, data.GetDownloadCount("C", "2.0.0-ALPHA"));

            Assert.Equal("*", accessCondition.IfNoneMatchETag);
            Assert.Null(accessCondition.IfMatchETag);

            _downloadDataClient.Verify(
                x => x.ReplaceLatestIndexedAsync(It.IsAny<DownloadData>(), It.IsAny<IAccessCondition>()),
                Times.Once);
        }
    }
}

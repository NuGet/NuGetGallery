// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Wrappers;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch.Integration
{
    public class PopularityTransferIntegrationTests
    {
        private readonly InMemoryCloudBlobClient _blobClient;
        private readonly InMemoryCloudBlobContainer _auxilliaryContainer;
        private readonly InMemoryCloudBlobContainer _storageContainer;

        private readonly Mock<IDocumentsOperationsWrapper> _searchOperations;
        private readonly Mock<IFeatureFlagService> _featureFlags;
        private readonly Auxiliary2AzureSearchConfiguration _config;
        private readonly AzureSearchJobDevelopmentConfiguration _developmentConfig;
        private readonly Mock<IAzureSearchTelemetryService> _telemetry;
        private readonly UpdateDownloadsCommand _target;

        private readonly PopularityTransferData _newPopularityTransfers;

        private IndexBatch<KeyedDocument> _indexedBatch;

        public PopularityTransferIntegrationTests(ITestOutputHelper output)
        {
            _featureFlags = new Mock<IFeatureFlagService>();
            _telemetry = new Mock<IAzureSearchTelemetryService>();

            _config = new Auxiliary2AzureSearchConfiguration
            {
                AuxiliaryDataStorageContainer = "auxiliary-container",
                StorageContainer = "storage-container",
                Scoring = new AzureSearchScoringConfiguration()
            };

            var options = new Mock<IOptionsSnapshot<Auxiliary2AzureSearchConfiguration>>();
            options
                .Setup(x => x.Value)
                .Returns(_config);

            _developmentConfig = new AzureSearchJobDevelopmentConfiguration();
            var developmentOptions = new Mock<IOptionsSnapshot<AzureSearchJobDevelopmentConfiguration>>();
            developmentOptions
                .Setup(x => x.Value)
                .Returns(_developmentConfig);

            var auxiliaryConfig = new AuxiliaryDataStorageConfiguration
            {
                AuxiliaryDataStorageContainer = "auxiliary-container",
                AuxiliaryDataStorageDownloadsPath = "downloads.json",
                AuxiliaryDataStorageDownloadOverridesPath = "downloadOverrides.json",
                AuxiliaryDataStorageExcludedPackagesPath = "excludedPackages.json",
            };

            var auxiliaryOptions = new Mock<IOptionsSnapshot<AuxiliaryDataStorageConfiguration>>();
            auxiliaryOptions
                .Setup(x => x.Value)
                .Returns(auxiliaryConfig);

            _auxilliaryContainer = new InMemoryCloudBlobContainer();
            _storageContainer = new InMemoryCloudBlobContainer();

            _blobClient = new InMemoryCloudBlobClient();
            _blobClient.Containers["auxiliary-container"] = _auxilliaryContainer;
            _blobClient.Containers["storage-container"] = _storageContainer;

            var auxiliaryFileClient = new AuxiliaryFileClient(
                _blobClient,
                auxiliaryOptions.Object,
                _telemetry.Object,
                output.GetLogger<AuxiliaryFileClient>());

            _newPopularityTransfers = new PopularityTransferData();
            var databaseFetcher = new Mock<IDatabaseAuxiliaryDataFetcher>();
            databaseFetcher
                .Setup(x => x.GetPopularityTransfersAsync())
                .ReturnsAsync(_newPopularityTransfers);

            var downloadDataClient = new DownloadDataClient(
                _blobClient,
                options.Object,
                _telemetry.Object,
                output.GetLogger<DownloadDataClient>());

            var popularityTransferDataClient = new PopularityTransferDataClient(
                _blobClient,
                options.Object,
                _telemetry.Object,
                output.GetLogger<PopularityTransferDataClient>());

            var versionListDataClient = new VersionListDataClient(
                _blobClient,
                options.Object,
                output.GetLogger<VersionListDataClient>());

            var downloadComparer = new DownloadSetComparer(
                _telemetry.Object,
                options.Object,
                output.GetLogger<DownloadSetComparer>());

            var dataComparer = new DataSetComparer(
                _telemetry.Object,
                output.GetLogger<DataSetComparer>());

            var downloadTransferrer = new DownloadTransferrer(
                dataComparer,
                options.Object,
                output.GetLogger<DownloadTransferrer>());

            var baseDocumentBuilder = new BaseDocumentBuilder(options.Object);
            var searchDocumentBuilder = new SearchDocumentBuilder(baseDocumentBuilder);
            var searchIndexActionBuilder = new SearchIndexActionBuilder(
                versionListDataClient,
                output.GetLogger<SearchIndexActionBuilder>());

            _searchOperations = new Mock<IDocumentsOperationsWrapper>();
            _searchOperations
                .Setup(x => x.IndexAsync(It.IsAny<IndexBatch<KeyedDocument>>()))
                .Callback<IndexBatch<KeyedDocument>>(batch =>
                {
                    _indexedBatch = batch;
                })
                .ReturnsAsync(new DocumentIndexResult());

            var hijackIndexClient = new Mock<ISearchIndexClientWrapper>();
            var searchIndexClient = new Mock<ISearchIndexClientWrapper>();
            searchIndexClient
                .Setup(x => x.Documents)
                .Returns(_searchOperations.Object);

            var batchPusher = new BatchPusher(
                searchIndexClient.Object,
                hijackIndexClient.Object,
                versionListDataClient,
                options.Object,
                developmentOptions.Object,
                _telemetry.Object,
                output.GetLogger<BatchPusher>());

            Func<IBatchPusher> batchPusherFactory = () => batchPusher;

            var time = new Mock<ISystemTime>();

            _featureFlags.Setup(x => x.IsPopularityTransferEnabled()).Returns(true);

            _target = new UpdateDownloadsCommand(
                auxiliaryFileClient,
                databaseFetcher.Object,
                downloadDataClient,
                downloadComparer,
                downloadTransferrer,
                popularityTransferDataClient,
                searchDocumentBuilder,
                searchIndexActionBuilder,
                batchPusherFactory,
                time.Object,
                _featureFlags.Object,
                options.Object,
                _telemetry.Object,
                output.GetLogger<Auxiliary2AzureSearchCommand>());
        }

        [Fact]
        public async Task FirstPopularityTransferChangesDownloads()
        {
            SetDownloadOverrides("{}");
            SetExcludedPackagesJson("{}");

            AddVersionList("A", "1.0.0");
            AddVersionList("B", "1.0.0");

            SetOldDownloadsJson(@"
{
  ""A"": { ""1.0.0"": 100 },
  ""B"": { ""1.0.0"": 1 }
}");
            SetNewDownloadsJson(@"
[
  [ ""A"", [ ""1.0.0"", 100 ] ],
  [ ""B"", [ ""1.0.0"", 1 ] ],
]");

            // Old: no rename
            // New: A -> B rename
            SetOldPopularityTransfersJson(@"{}");
            _newPopularityTransfers.AddTransfer("A", "B");

            _config.Scoring.PopularityTransfer = 0.5;

            await _target.ExecuteAsync();

            Assert.NotNull(_indexedBatch);
            var actions = _indexedBatch.Actions.OrderBy(x => x.Document.Key).ToList();
            Assert.Equal(8, actions.Count);

            VerifyUpdateDownloadCountAction("A", 50, actions[0]);
            VerifyUpdateDownloadCountAction("A", 50, actions[1]);
            VerifyUpdateDownloadCountAction("A", 50, actions[2]);
            VerifyUpdateDownloadCountAction("A", 50, actions[3]);
            VerifyUpdateDownloadCountAction("B", 51, actions[4]);
            VerifyUpdateDownloadCountAction("B", 51, actions[5]);
            VerifyUpdateDownloadCountAction("B", 51, actions[6]);
            VerifyUpdateDownloadCountAction("B", 51, actions[7]);
        }

        [Fact]
        public async Task NewPopularityTransferChangesDownloads()
        {
            SetDownloadOverrides("{}");
            SetExcludedPackagesJson("{}");

            AddVersionList("A", "1.0.0");
            AddVersionList("B", "1.0.0");
            AddVersionList("C", "1.0.0");
            AddVersionList("D", "1.0.0");

            SetOldDownloadsJson(@"
{
  ""A"": { ""1.0.0"": 100 },
  ""B"": { ""1.0.0"": 50 },
  ""C"": { ""1.0.0"": 20 },
  ""D"": { ""1.0.0"": 1 }
}");
            SetNewDownloadsJson(@"
[
  [ ""A"", [ ""1.0.0"", 100 ] ],
  [ ""B"", [ ""1.0.0"", 50 ] ],
  [ ""C"", [ ""1.0.0"", 20 ] ],
  [ ""D"", [ ""1.0.0"", 1 ] ]
]");

            // Old: A -> B rename
            // New: A -> B, C -> D rename
            SetOldPopularityTransfersJson(@"{ ""A"": [ ""B"" ] }");
            _newPopularityTransfers.AddTransfer("A", "B");
            _newPopularityTransfers.AddTransfer("C", "D");

            _config.Scoring.PopularityTransfer = 0.5;

            await _target.ExecuteAsync();

            Assert.NotNull(_indexedBatch);
            var actions = _indexedBatch.Actions.OrderBy(x => x.Document.Key).ToList();
            Assert.Equal(8, actions.Count);

            VerifyUpdateDownloadCountAction("C", 10, actions[0]);
            VerifyUpdateDownloadCountAction("C", 10, actions[1]);
            VerifyUpdateDownloadCountAction("C", 10, actions[2]);
            VerifyUpdateDownloadCountAction("C", 10, actions[3]);
            VerifyUpdateDownloadCountAction("D", 11, actions[4]);
            VerifyUpdateDownloadCountAction("D", 11, actions[5]);
            VerifyUpdateDownloadCountAction("D", 11, actions[6]);
            VerifyUpdateDownloadCountAction("D", 11, actions[7]);
        }

        [Fact]
        public async Task UpdatedPopularityTransferChangesDownloads()
        {
            SetDownloadOverrides("{}");
            SetExcludedPackagesJson("{}");

            AddVersionList("A", "1.0.0");
            AddVersionList("B", "1.0.0");
            AddVersionList("C", "1.0.0");

            SetOldDownloadsJson(@"
{
  ""A"": { ""1.0.0"": 100 },
  ""B"": { ""1.0.0"": 20 },
  ""C"": { ""1.0.0"": 1 },
}");
            SetNewDownloadsJson(@"
[
  [ ""A"", [ ""1.0.0"", 100 ] ],
  [ ""B"", [ ""1.0.0"", 20 ] ],
  [ ""C"", [ ""1.0.0"", 1 ] ],
]");

            // Old: A -> B rename
            // New: A -> C rename
            SetOldPopularityTransfersJson(@"{ ""A"": [ ""B"" ] }");
            _newPopularityTransfers.AddTransfer("A", "C");

            _config.Scoring.PopularityTransfer = 0.5;

            await _target.ExecuteAsync();

            Assert.NotNull(_indexedBatch);
            var actions = _indexedBatch.Actions.OrderBy(x => x.Document.Key).ToList();
            Assert.Equal(12, actions.Count);

            VerifyUpdateDownloadCountAction("A", 50, actions[0]);
            VerifyUpdateDownloadCountAction("A", 50, actions[1]);
            VerifyUpdateDownloadCountAction("A", 50, actions[2]);
            VerifyUpdateDownloadCountAction("A", 50, actions[3]);
            VerifyUpdateDownloadCountAction("B", 20, actions[4]);
            VerifyUpdateDownloadCountAction("B", 20, actions[5]);
            VerifyUpdateDownloadCountAction("B", 20, actions[6]);
            VerifyUpdateDownloadCountAction("B", 20, actions[7]);
            VerifyUpdateDownloadCountAction("C", 51, actions[8]);
            VerifyUpdateDownloadCountAction("C", 51, actions[9]);
            VerifyUpdateDownloadCountAction("C", 51, actions[10]);
            VerifyUpdateDownloadCountAction("C", 51, actions[11]);
        }

        [Fact]
        public async Task ReverseTransferChangesDownloads()
        {
            SetDownloadOverrides("{}");
            SetExcludedPackagesJson("{}");

            AddVersionList("A", "1.0.0");
            AddVersionList("B", "1.0.0");

            SetOldDownloadsJson(@"
{
  ""A"": { ""1.0.0"": 100 },
  ""B"": { ""1.0.0"": 20 }
}");
            SetNewDownloadsJson(@"
[
  [ ""A"", [ ""1.0.0"", 100 ] ],
  [ ""B"", [ ""1.0.0"", 20 ] ]
]");

            // Old: A -> B rename
            // New: B -> A rename
            SetOldPopularityTransfersJson(@"{ ""A"": [ ""B"" ] }");
            _newPopularityTransfers.AddTransfer("B", "A");

            _config.Scoring.PopularityTransfer = 0.5;

            await _target.ExecuteAsync();

            Assert.NotNull(_indexedBatch);
            var actions = _indexedBatch.Actions.OrderBy(x => x.Document.Key).ToList();
            Assert.Equal(8, actions.Count);

            VerifyUpdateDownloadCountAction("A", 110, actions[0]);
            VerifyUpdateDownloadCountAction("A", 110, actions[1]);
            VerifyUpdateDownloadCountAction("A", 110, actions[2]);
            VerifyUpdateDownloadCountAction("A", 110, actions[3]);
            VerifyUpdateDownloadCountAction("B", 10, actions[4]);
            VerifyUpdateDownloadCountAction("B", 10, actions[5]);
            VerifyUpdateDownloadCountAction("B", 10, actions[6]);
            VerifyUpdateDownloadCountAction("B", 10, actions[7]);
        }

        [Fact]
        public async Task DisablingPopularityTransferFeatureRemovesTransfers()
        {
            SetExcludedPackagesJson("{}");

            AddVersionList("A", "1.0.0");
            AddVersionList("B", "1.0.0");
            AddVersionList("C", "1.0.0");

            SetOldDownloadsJson(@"
{
  ""A"": { ""1.0.0"": 100 },
  ""B"": { ""1.0.0"": 20 },
  ""C"": { ""1.0.0"": 1 }
}");
            SetNewDownloadsJson(@"
[
  [ ""A"", [ ""1.0.0"", 100 ] ],
  [ ""B"", [ ""1.0.0"", 20 ] ],
  [ ""C"", [ ""1.0.0"", 1 ] ]
]");

            // Old: A -> B rename
            // New: A -> B rename
            SetOldPopularityTransfersJson(@"{ ""A"": [ ""B"" ] }");
            _newPopularityTransfers.AddTransfer("A", "B");

            SetDownloadOverrides(@"{ ""C"": 5 }");

            _config.Scoring.PopularityTransfer = 0.5;
            _featureFlags
                .Setup(x => x.IsPopularityTransferEnabled())
                .Returns(false);

            await _target.ExecuteAsync();

            Assert.NotNull(_indexedBatch);
            var actions = _indexedBatch.Actions.OrderBy(x => x.Document.Key).ToList();
            Assert.Equal(12, actions.Count);

            VerifyUpdateDownloadCountAction("A", 100, actions[0]);
            VerifyUpdateDownloadCountAction("A", 100, actions[1]);
            VerifyUpdateDownloadCountAction("A", 100, actions[2]);
            VerifyUpdateDownloadCountAction("A", 100, actions[3]);
            VerifyUpdateDownloadCountAction("B", 20, actions[4]);
            VerifyUpdateDownloadCountAction("B", 20, actions[5]);
            VerifyUpdateDownloadCountAction("B", 20, actions[6]);
            VerifyUpdateDownloadCountAction("B", 20, actions[7]);
            VerifyUpdateDownloadCountAction("C", 5, actions[8]);
            VerifyUpdateDownloadCountAction("C", 5, actions[9]);
            VerifyUpdateDownloadCountAction("C", 5, actions[10]);
            VerifyUpdateDownloadCountAction("C", 5, actions[11]);
        }

        private void SetOldDownloadsJson(string json)
        {
            _storageContainer.Blobs["downloads/downloads.v2.json"] = new InMemoryCloudBlob(json);
        }

        private void SetNewDownloadsJson(string json)
        {
            _auxilliaryContainer.Blobs["downloads.json"] = new InMemoryCloudBlob(json);
        }

        private void SetDownloadOverrides(string json)
        {
            _auxilliaryContainer.Blobs["downloadOverrides.json"] = new InMemoryCloudBlob(json);
        }

        private void SetExcludedPackagesJson(string json)
        {
            _auxilliaryContainer.Blobs["excludedPackages.json"] = new InMemoryCloudBlob(json);
        }

        private void SetOldPopularityTransfersJson(string json)
        {
            _storageContainer.Blobs["popularity-transfers/popularity-transfers.v1.json"]
                = new InMemoryCloudBlob(json);
        }

        private void AddVersionList(string id, string version)
        {
            _storageContainer.Blobs[$"version-lists/{id.ToLowerInvariant()}.json"] = new InMemoryCloudBlob(@"
{
  ""VersionProperties"": {
    """ + version + @""": { ""Listed"": true }
  }
}");
        }

        private void VerifyUpdateDownloadCountAction(
            string expectedId,
            long expectedDownloads,
            IndexAction<KeyedDocument> action)
        {
            var document = action.Document as SearchDocument.UpdateDownloadCount;

            Assert.NotNull(document);
            Assert.Equal(IndexActionType.Merge, action.ActionType);
            Assert.StartsWith(expectedId, document.Key, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expectedDownloads, document.TotalDownloadCount);
        }
    }
}

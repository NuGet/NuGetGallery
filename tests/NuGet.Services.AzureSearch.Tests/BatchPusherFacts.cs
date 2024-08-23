﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.Wrappers;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch
{
    public class BatchPusherFacts
    {
        public class FinishAsync : BaseFacts
        {
            public FinishAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ReturnsFailedPackageIds()
            {
                _versionListDataClient
                    .Setup(x => x.TryReplaceAsync(IdB, It.IsAny<VersionListData>(), It.IsAny<IAccessCondition>()))
                    .ReturnsAsync(false);

                _config.AzureSearchBatchSize = 7;
                _target.EnqueueIndexActions(IdA, _indexActions);
                _target.EnqueueIndexActions(IdB, _indexActions);
                _target.EnqueueIndexActions(IdC, _indexActions);

                var result = await _target.TryFinishAsync();

                Assert.Equal(new[] { IdB }, result.FailedPackageIds.ToArray());

                Assert.Equal(3, _hijackBatches.Count);
                Assert.Equal(2, _searchBatches.Count);

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdA,
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdB,
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdC,
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Exactly(3));
            }

            [Fact]
            public async Task UpdatesOnlyAllVersionLists()
            {
                _config.AzureSearchBatchSize = 7;
                _target.EnqueueIndexActions(IdA, _indexActions);
                _target.EnqueueIndexActions(IdB, _indexActions);
                _target.EnqueueIndexActions(IdC, _indexActions);

                var result = await _target.TryFinishAsync();

                Assert.Empty(result.FailedPackageIds);

                Assert.Equal(3, _hijackBatches.Count);
                Assert.Equal(2, _searchBatches.Count);

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdA,
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdB,
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdC,
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Exactly(3));
            }

            [Fact]
            public async Task UpdatesVersionListIfAllActionsAreDone()
            {
                _config.AzureSearchBatchSize = 1;
                _target.EnqueueIndexActions(IdA, _indexActions);

                var result = await _target.TryFinishAsync();

                Assert.Empty(result.FailedPackageIds);

                Assert.Equal(5, _hijackBatches.Count);
                Assert.Equal(3, _searchBatches.Count);

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdA,
                        _indexActions.VersionListDataResult.Result,
                        _indexActions.VersionListDataResult.AccessCondition),
                    Times.Once);
            }

            [Fact]
            public async Task SkipsUpdatingVersionListIfDisabled()
            {
                _config.AzureSearchBatchSize = 1;
                _developmentConfig.DisableVersionListWriters = true;
                _target.EnqueueIndexActions(IdA, _indexActions);

                var result = await _target.TryFinishAsync();

                Assert.Empty(result.FailedPackageIds);

                Assert.Equal(5, _hijackBatches.Count);
                Assert.Equal(3, _searchBatches.Count);

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
            }

            [Fact]
            public async Task PushesPartialBatch()
            {
                _config.AzureSearchBatchSize = 1000;
                _target.EnqueueIndexActions(IdA, _indexActions);

                var result = await _target.TryFinishAsync();

                Assert.Empty(result.FailedPackageIds);

                Assert.Single(_hijackBatches);
                Assert.Equal(_hijackDocuments, _hijackBatches[0].Actions.ToArray());
                Assert.Single(_searchBatches);
                Assert.Equal(_searchDocuments, _searchBatches[0].Actions.ToArray());

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdA,
                        _indexActions.VersionListDataResult.Result,
                        _indexActions.VersionListDataResult.AccessCondition),
                    Times.Once);

                Assert.Empty(_target._versionListDataResults);
                Assert.Empty(_target._hijackActions);
                Assert.Empty(_target._searchActions);
                Assert.Empty(_target._idReferenceCount);
            }

            [Fact]
            public async Task PushesFullBatchesThenPartialBatch()
            {
                _config.AzureSearchBatchSize = 2;
                _target.EnqueueIndexActions(IdA, _indexActions);

                var result = await _target.TryFinishAsync();

                Assert.Empty(result.FailedPackageIds);

                Assert.Equal(3, _hijackBatches.Count);
                Assert.Equal(new[] { _hijackDocumentA, _hijackDocumentB }, _hijackBatches[0].Actions.ToArray());
                Assert.Equal(new[] { _hijackDocumentC, _hijackDocumentD }, _hijackBatches[1].Actions.ToArray());
                Assert.Equal(new[] { _hijackDocumentE }, _hijackBatches[2].Actions.ToArray());
                Assert.Equal(2, _searchBatches.Count);
                Assert.Equal(new[] { _searchDocumentA, _searchDocumentB }, _searchBatches[0].Actions.ToArray());
                Assert.Equal(new[] { _searchDocumentC }, _searchBatches[1].Actions.ToArray());

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdA,
                        _indexActions.VersionListDataResult.Result,
                        _indexActions.VersionListDataResult.AccessCondition),
                    Times.Once);

                Assert.Empty(_target._versionListDataResults);
                Assert.Empty(_target._hijackActions);
                Assert.Empty(_target._searchActions);
                Assert.Empty(_target._idReferenceCount);
            }

            [Fact]
            public async Task SplitsBatchesInHalfWhenTooLarge()
            {
                _config.AzureSearchBatchSize = 100;
                _target.EnqueueIndexActions(IdA, _indexActions);
                _hijackIndexWrapper
                    .Setup(x => x.IndexAsync(It.IsAny<IndexDocumentsBatch<KeyedDocument>>()))
                    .Returns<IndexDocumentsBatch<KeyedDocument>>(b =>
                    {
                        _hijackBatches.Add(b);
                        if (b.Actions.Count() > 1)
                        {
                            throw new RequestFailedException((int)HttpStatusCode.RequestEntityTooLarge, "Too big!");
                        }

                        return Task.FromResult(SearchModelFactory.IndexDocumentsResult(new List<IndexingResult>()));
                    });

                var result = await _target.TryFinishAsync();

                Assert.Empty(result.FailedPackageIds);

                Assert.Equal(9, _hijackBatches.Count);
                Assert.Equal(
                    new[] { _hijackDocumentA, _hijackDocumentB, _hijackDocumentC, _hijackDocumentD, _hijackDocumentE },
                    _hijackBatches[0].Actions.ToArray());
                Assert.Equal(
                    new[] { _hijackDocumentA, _hijackDocumentB },
                    _hijackBatches[1].Actions.ToArray());
                Assert.Equal(
                    new[] { _hijackDocumentA },
                    _hijackBatches[2].Actions.ToArray());
                Assert.Equal(
                    new[] { _hijackDocumentB },
                    _hijackBatches[3].Actions.ToArray());
                Assert.Equal(
                    new[] { _hijackDocumentC, _hijackDocumentD, _hijackDocumentE },
                    _hijackBatches[4].Actions.ToArray());
                Assert.Equal(
                    new[] { _hijackDocumentC },
                    _hijackBatches[5].Actions.ToArray());
                Assert.Equal(
                    new[] { _hijackDocumentD, _hijackDocumentE },
                    _hijackBatches[6].Actions.ToArray());
                Assert.Equal(
                    new[] { _hijackDocumentD },
                    _hijackBatches[7].Actions.ToArray());
                Assert.Equal(
                    new[] { _hijackDocumentE },
                    _hijackBatches[8].Actions.ToArray());
            }

            [Fact]
            public async Task StopsSplittingBatchesAtOne()
            {
                _config.AzureSearchBatchSize = 100;
                _target.EnqueueIndexActions(IdA, _indexActions);
                _hijackIndexWrapper
                    .Setup(x => x.IndexAsync(It.IsAny<IndexDocumentsBatch<KeyedDocument>>()))
                    .Returns<IndexDocumentsBatch<KeyedDocument>>(b =>
                    {
                        _hijackBatches.Add(b);
                        throw new RequestFailedException((int)HttpStatusCode.RequestEntityTooLarge, "Too big!");
                    });

                var ex = await Assert.ThrowsAsync<RequestFailedException>(
                    () => _target.TryFinishAsync());

                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (HttpStatusCode)ex.Status);
                Assert.Equal(3, _hijackBatches.Count);
                Assert.Equal(
                    new[] { _hijackDocumentA, _hijackDocumentB, _hijackDocumentC, _hijackDocumentD, _hijackDocumentE },
                    _hijackBatches[0].Actions.ToArray());
                Assert.Equal(
                    new[] { _hijackDocumentA, _hijackDocumentB },
                    _hijackBatches[1].Actions.ToArray());
                Assert.Equal(
                    new[] { _hijackDocumentA },
                    _hijackBatches[2].Actions.ToArray());
            }
        }

        public class PushFullBatchesAsync : BaseFacts
        {
            public PushFullBatchesAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task LogsUpALimitedNumberOfFailedResults()
            {
                _target.EnqueueIndexActions(IdA, _indexActions);
                _searchIndexWrapper
                    .Setup(x => x.IndexAsync(It.IsAny<IndexDocumentsBatch<KeyedDocument>>()))
                    .ReturnsAsync(() => SearchModelFactory.IndexDocumentsResult(new List<IndexingResult>
                    {
                        SearchModelFactory.IndexingResult(key: "A-0", errorMessage: "A-0 message", succeeded: false, status: 0),
                        SearchModelFactory.IndexingResult(key: "A-1", errorMessage: "A-1 message", succeeded: false, status: 1),
                        SearchModelFactory.IndexingResult(key: "A-2", errorMessage: "A-2 message", succeeded: true, status: 2),
                        SearchModelFactory.IndexingResult(key: "A-3", errorMessage: "A-3 message", succeeded: false, status: 3),
                        SearchModelFactory.IndexingResult(key: "A-4", errorMessage: "A-4 message", succeeded: false, status: 4),
                        SearchModelFactory.IndexingResult(key: "A-5", errorMessage: "A-5 message", succeeded: false, status: 5),
                        SearchModelFactory.IndexingResult(key: "A-6", errorMessage: "A-6 message", succeeded: false, status: 6),
                    }));

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.TryPushFullBatchesAsync());

                Assert.Contains("Errors were found when indexing a batch. Up to 5 errors get logged.", ex.Message);
                Assert.Contains("Indexing document with key A-0 failed for index search. 0: A-0 message", _logger.Messages);
                Assert.Contains("Indexing document with key A-1 failed for index search. 1: A-1 message", _logger.Messages);
                Assert.Contains("Indexing document with key A-3 failed for index search. 3: A-3 message", _logger.Messages);
                Assert.Contains("Indexing document with key A-4 failed for index search. 4: A-4 message", _logger.Messages);
                Assert.Contains("Indexing document with key A-5 failed for index search. 5: A-5 message", _logger.Messages);

                Assert.All(_logger.Messages, x => Assert.DoesNotContain("A-2", x));
                Assert.All(_logger.Messages, x => Assert.DoesNotContain("A-6", x));

                var innerEx = Assert.IsType<IndexBatchException>(ex.InnerException);
                Assert.Equal(6, innerEx.IndexingResults.Count(x => !x.Succeeded));
                Assert.Equal(1, innerEx.IndexingResults.Count(x => x.Succeeded));
            }

            [Fact]
            public async Task ReturnsFailedPackageIds()
            {
                _versionListDataClient
                    .Setup(x => x.TryReplaceAsync(IdA, It.IsAny<VersionListData>(), It.IsAny<IAccessCondition>()))
                    .ReturnsAsync(false);

                _config.AzureSearchBatchSize = 7;
                _target.EnqueueIndexActions(IdA, _indexActions);
                _target.EnqueueIndexActions(IdB, _indexActions);
                _target.EnqueueIndexActions(IdC, _indexActions);

                var result = await _target.TryPushFullBatchesAsync();

                Assert.Equal(new[] { IdA }, result.FailedPackageIds.ToArray());

                Assert.Equal(2, _hijackBatches.Count);
                Assert.Single(_searchBatches);

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdA,
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdB,
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdC,
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Exactly(2));
            }

            [Fact]
            public async Task UpdatesOnlyFinishedVersionLists()
            {
                _config.AzureSearchBatchSize = 7;
                _target.EnqueueIndexActions(IdA, _indexActions);
                _target.EnqueueIndexActions(IdB, _indexActions);
                _target.EnqueueIndexActions(IdC, _indexActions);

                var result = await _target.TryPushFullBatchesAsync();

                Assert.Empty(result.FailedPackageIds);

                Assert.Equal(2, _hijackBatches.Count);
                Assert.Single(_searchBatches);

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdA,
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdB,
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdC,
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Exactly(2));
            }

            [Fact]
            public async Task OnlyPushesFullBatches()
            {
                _config.AzureSearchBatchSize = 2;
                _target.EnqueueIndexActions(IdA, _indexActions);

                var result = await _target.TryPushFullBatchesAsync();

                Assert.Empty(result.FailedPackageIds);

                Assert.Equal(2, _hijackBatches.Count);
                Assert.Equal(new[] { _hijackDocumentA, _hijackDocumentB }, _hijackBatches[0].Actions.ToArray());
                Assert.Equal(new[] { _hijackDocumentC, _hijackDocumentD }, _hijackBatches[1].Actions.ToArray());
                Assert.Single(_searchBatches);
                Assert.Equal(new[] { _searchDocumentA, _searchDocumentB }, _searchBatches[0].Actions.ToArray());

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);

                Assert.Single(_target._versionListDataResults);
                Assert.Equal(new[] { _hijackDocumentE }, _target._hijackActions.Select(x => x.Value).ToArray());
                Assert.Equal(new[] { _searchDocumentC }, _target._searchActions.Select(x => x.Value).ToArray());
                Assert.Equal(2, _target._idReferenceCount[IdA]);
            }

            [Fact]
            public async Task AllowsPushingNoBatchesIfNoBatchesAreFull()
            {
                _config.AzureSearchBatchSize = 1000;
                _target.EnqueueIndexActions(IdA, _indexActions);

                var result = await _target.TryPushFullBatchesAsync();

                Assert.Empty(result.FailedPackageIds);

                Assert.Empty(_hijackBatches);
                Assert.Empty(_searchBatches);

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
            }

            [Fact]
            public async Task DoesNotUpdateVersionListIfOnlyOneIndexsActionsAreComplete()
            {
                _config.AzureSearchBatchSize = 3;
                _target.EnqueueIndexActions(IdA, _indexActions);

                var result = await _target.TryPushFullBatchesAsync();

                Assert.Empty(result.FailedPackageIds);

                Assert.Single(_hijackBatches);
                Assert.Equal(new[] { _hijackDocumentA, _hijackDocumentB, _hijackDocumentC }, _hijackBatches[0].Actions.ToArray());
                Assert.Single(_searchBatches);
                Assert.Equal(new[] { _searchDocumentA, _searchDocumentB, _searchDocumentC }, _searchBatches[0].Actions.ToArray());

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
            }

            [Fact]
            public async Task UpdatesVersionListIfAllActionsAreDone()
            {
                _config.AzureSearchBatchSize = 1;
                _target.EnqueueIndexActions(IdA, _indexActions);

                var result = await _target.TryPushFullBatchesAsync();

                Assert.Empty(result.FailedPackageIds);

                Assert.Equal(5, _hijackBatches.Count);
                Assert.Equal(3, _searchBatches.Count);

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdA,
                        _indexActions.VersionListDataResult.Result,
                        _indexActions.VersionListDataResult.AccessCondition),
                    Times.Once);
            }

            [Fact]
            public async Task AllowsReprocessingCompletedActions()
            {
                _config.AzureSearchBatchSize = 1;
                _target.EnqueueIndexActions(IdA, _indexActions);
                await _target.TryPushFullBatchesAsync();
                _target.EnqueueIndexActions(IdA, _indexActions);

                var result = await _target.TryPushFullBatchesAsync();

                Assert.Empty(result.FailedPackageIds);

                Assert.Equal(10, _hijackBatches.Count);
                Assert.Equal(6, _searchBatches.Count);

                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        It.IsAny<string>(),
                        It.IsAny<VersionListData>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Exactly(2));
                _versionListDataClient.Verify(
                    x => x.TryReplaceAsync(
                        IdA,
                        _indexActions.VersionListDataResult.Result,
                        _indexActions.VersionListDataResult.AccessCondition),
                    Times.Exactly(2));
            }
        }

        public class EnqueueIndexActions : BaseFacts
        {
            public EnqueueIndexActions(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public void RejectsDuplicateId()
            {
                _target.EnqueueIndexActions(IdA, _indexActions);

                var ex = Assert.Throws<ArgumentException>(
                    () => _target.EnqueueIndexActions(IdA, _indexActions));
                Assert.Contains("This package ID has already been enqueued.", ex.Message);
            }

            [Fact]
            public void EnqueuesAndIncrements()
            {
                _target.EnqueueIndexActions(IdA, _indexActions);

                Assert.Equal(3, _target._searchActions.Count);
                Assert.Equal(_searchDocuments, _target._searchActions.Select(x => x.Value).ToList());
                Assert.All(_target._searchActions, x => Assert.Equal(IdA, x.Id));

                Assert.Equal(5, _target._hijackActions.Count);
                Assert.Equal(_hijackDocuments, _target._hijackActions.Select(x => x.Value).ToList());
                Assert.All(_target._hijackActions, x => Assert.Equal(IdA, x.Id));

                Assert.Equal(new[] { IdA }, _target._idReferenceCount.Keys.ToArray());
                Assert.Equal(8, _target._idReferenceCount[IdA]);

                Assert.Equal(new[] { IdA }, _target._versionListDataResults.Keys.ToArray());
                Assert.Same(_indexActions.VersionListDataResult, _target._versionListDataResults[IdA]);
            }

            [Fact]
            public void RejectsEmptyEnqueue()
            {
                var emptyIndexActions = new IndexActions(
                    new List<IndexDocumentsAction<KeyedDocument>>(),
                    new List<IndexDocumentsAction<KeyedDocument>>(),
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        AccessConditionWrapper.GenerateEmptyCondition()));

                var ex = Assert.Throws<ArgumentException>(
                    () => _target.EnqueueIndexActions(IdA, emptyIndexActions));
                Assert.Contains("There must be at least one index action.", ex.Message);
                Assert.Empty(_target._searchActions);
                Assert.Empty(_target._hijackActions);
                Assert.Empty(_target._idReferenceCount);
                Assert.Empty(_target._versionListDataResults);
            }
        }

        public abstract class BaseFacts
        {
            protected const string IdA = "NuGet.Versioning";
            protected const string IdB = "NuGet.Frameworks";
            protected const string IdC = "NuGet.Packaging";

            protected readonly RecordingLogger<BatchPusher> _logger;
            protected readonly Mock<ISearchClientWrapper> _searchIndexWrapper;
            protected readonly Mock<ISearchClientWrapper> _hijackIndexWrapper;
            protected readonly Mock<IVersionListDataClient> _versionListDataClient;
            protected readonly AzureSearchJobConfiguration _config;
            protected readonly AzureSearchJobDevelopmentConfiguration _developmentConfig;
            protected readonly Mock<IOptionsSnapshot<AzureSearchJobConfiguration>> _options;
            protected readonly Mock<IOptionsSnapshot<AzureSearchJobDevelopmentConfiguration>> _developmentOptions;
            protected readonly Mock<IAzureSearchTelemetryService> _telemetryService;
            protected readonly IndexActions _indexActions;
            protected readonly BatchPusher _target;

            protected readonly IndexDocumentsAction<KeyedDocument> _searchDocumentA;
            protected readonly IndexDocumentsAction<KeyedDocument> _searchDocumentB;
            protected readonly IndexDocumentsAction<KeyedDocument> _searchDocumentC;
            protected readonly List<IndexDocumentsAction<KeyedDocument>> _searchDocuments;
            protected readonly IndexDocumentsAction<KeyedDocument> _hijackDocumentA;
            protected readonly IndexDocumentsAction<KeyedDocument> _hijackDocumentB;
            protected readonly IndexDocumentsAction<KeyedDocument> _hijackDocumentC;
            protected readonly IndexDocumentsAction<KeyedDocument> _hijackDocumentD;
            protected readonly IndexDocumentsAction<KeyedDocument> _hijackDocumentE;
            protected readonly List<IndexDocumentsAction<KeyedDocument>> _hijackDocuments;
            protected readonly List<IndexDocumentsBatch<KeyedDocument>> _searchBatches;
            protected readonly List<IndexDocumentsBatch<KeyedDocument>> _hijackBatches;

            public BaseFacts(ITestOutputHelper output)
            {
                _logger = output.GetLogger<BatchPusher>();
                _searchIndexWrapper = new Mock<ISearchClientWrapper>();
                _hijackIndexWrapper = new Mock<ISearchClientWrapper>();
                _versionListDataClient = new Mock<IVersionListDataClient>();
                _config = new AzureSearchJobConfiguration();
                _developmentConfig = new AzureSearchJobDevelopmentConfiguration();
                _options = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                _developmentOptions = new Mock<IOptionsSnapshot<AzureSearchJobDevelopmentConfiguration>>();
                _telemetryService = new Mock<IAzureSearchTelemetryService>();

                _searchIndexWrapper.Setup(x => x.IndexName).Returns("search");
                _hijackIndexWrapper.Setup(x => x.IndexName).Returns("hijack");
                _versionListDataClient
                    .Setup(x => x.TryReplaceAsync(It.IsAny<string>(), It.IsAny<VersionListData>(), It.IsAny<IAccessCondition>()))
                    .ReturnsAsync(true);
                _options.Setup(x => x.Value).Returns(() => _config);
                _developmentOptions.Setup(x => x.Value).Returns(() => _developmentConfig);

                _searchBatches = new List<IndexDocumentsBatch<KeyedDocument>>();
                _hijackBatches = new List<IndexDocumentsBatch<KeyedDocument>>();

                _searchIndexWrapper
                    .Setup(x => x.IndexAsync(It.IsAny<IndexDocumentsBatch<KeyedDocument>>()))
                    .ReturnsAsync(() => SearchModelFactory.IndexDocumentsResult(new IndexingResult[0]))
                    .Callback<IndexDocumentsBatch<KeyedDocument>>(b => _searchBatches.Add(b));
                _hijackIndexWrapper
                    .Setup(x => x.IndexAsync(It.IsAny<IndexDocumentsBatch<KeyedDocument>>()))
                    .ReturnsAsync(() => SearchModelFactory.IndexDocumentsResult(new IndexingResult[0]))
                    .Callback<IndexDocumentsBatch<KeyedDocument>>(b => _hijackBatches.Add(b));

                _config.AzureSearchBatchSize = 2;
                _config.MaxConcurrentVersionListWriters = 1;

                _searchDocumentA = IndexDocumentsAction.Upload(new KeyedDocument());
                _searchDocumentB = IndexDocumentsAction.Upload(new KeyedDocument());
                _searchDocumentC = IndexDocumentsAction.Upload(new KeyedDocument());
                _searchDocuments = new List<IndexDocumentsAction<KeyedDocument>>
                {
                    _searchDocumentA,
                    _searchDocumentB,
                    _searchDocumentC,
                };

                _hijackDocumentA = IndexDocumentsAction.Upload(new KeyedDocument());
                _hijackDocumentB = IndexDocumentsAction.Upload(new KeyedDocument());
                _hijackDocumentC = IndexDocumentsAction.Upload(new KeyedDocument());
                _hijackDocumentD = IndexDocumentsAction.Upload(new KeyedDocument());
                _hijackDocumentE = IndexDocumentsAction.Upload(new KeyedDocument());
                _hijackDocuments = new List<IndexDocumentsAction<KeyedDocument>>
                {
                    _hijackDocumentA,
                    _hijackDocumentB,
                    _hijackDocumentC,
                    _hijackDocumentD,
                    _hijackDocumentE,
                };

                _indexActions = new IndexActions(
                    _searchDocuments,
                    _hijackDocuments,
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        AccessConditionWrapper.GenerateEmptyCondition()));

                _target = new BatchPusher(
                    _searchIndexWrapper.Object,
                    _hijackIndexWrapper.Object,
                    _versionListDataClient.Object,
                    _options.Object,
                    _developmentOptions.Object,
                    _telemetryService.Object,
                    _logger);
            }
        }
    }
}

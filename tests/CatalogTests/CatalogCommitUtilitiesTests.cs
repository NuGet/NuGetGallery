// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using Xunit;

namespace CatalogTests
{
    public class CatalogCommitUtilitiesTests
    {
        private const int _maxConcurrentBatches = 1;
        private static readonly CatalogCommitItemBatch _lastBatch;
        private static readonly Task FailedTask = Task.FromException(new Exception());
        private static readonly PackageIdentity _packageIdentitya = new PackageIdentity(id: "a", version: new NuGetVersion("1.0.0"));
        private static readonly PackageIdentity _packageIdentityb = new PackageIdentity(id: "b", version: new NuGetVersion("1.0.0"));

        static CatalogCommitUtilitiesTests()
        {
            var commitTimeStamp = DateTime.UtcNow;
            var commit = TestUtility.CreateCatalogCommitItem(commitTimeStamp, _packageIdentitya);

            _lastBatch = new CatalogCommitItemBatch(DateTime.UtcNow, _packageIdentitya.Id, new[] { commit });
        }

        [Fact]
        public void CreateCommitItemBatches_WhenCatalogItemsIsNull_Throws()
        {
            IEnumerable<CatalogCommitItem> catalogItems = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.CreateCommitItemBatches(
                    catalogItems,
                    CatalogCommitUtilities.GetPackageIdKey)
                    .ToArray()); // force evaluation

            Assert.Equal("catalogItems", exception.ParamName);
        }

        [Fact]
        public void CreateCommitItemBatches_WhenGetCatalogCommitItemKeyIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.CreateCommitItemBatches(
                    Enumerable.Empty<CatalogCommitItem>(),
                    getCatalogCommitItemKey: null)
                    .ToArray()); // force evaluation

            Assert.Equal("getCatalogCommitItemKey", exception.ParamName);
        }

        [Fact]
        public void CreateCommitItemBatches_WhenMultipleCommitsShareCommitTimeStampButNotCommitId_Throws()
        {
            var commitTimeStamp = DateTime.UtcNow;
            var context = TestUtility.CreateCatalogContextJObject();
            var commitItem0 = CatalogCommitItem.Create(
                context,
                TestUtility.CreateCatalogCommitItemJObject(commitTimeStamp, _packageIdentitya));
            var commitItem1 = CatalogCommitItem.Create(
                context,
                TestUtility.CreateCatalogCommitItemJObject(commitTimeStamp, _packageIdentityb));
            var commitItems = new[] { commitItem0, commitItem1 };

            var exception = Assert.Throws<ArgumentException>(() =>
                CatalogCommitUtilities.CreateCommitItemBatches(
                    commitItems,
                    CatalogCommitUtilities.GetPackageIdKey)
                    .ToArray()); // force evaluation

            Assert.Equal("catalogItems", exception.ParamName);
            Assert.StartsWith("Multiple commits exist with the same commit timestamp but different commit ID's:  " +
                $"{{ CommitId = {commitItem0.CommitId}, CommitTimeStamp = {commitItem0.CommitTimeStamp.ToString("O")} }}, " +
                $"{{ CommitId = {commitItem1.CommitId}, CommitTimeStamp = {commitItem1.CommitTimeStamp.ToString("O")} }}.",
                exception.Message);
        }

        [Fact]
        public void CreateCommitItemBatches_WhenPackageIdsVaryInCasing_GroupsUsingProvidedGetKeyFunction()
        {
            var now = DateTime.UtcNow;
            var packageIdentityA0 = new PackageIdentity(id: "a", version: new NuGetVersion("1.0.0"));
            var packageIdentityA1 = new PackageIdentity(id: "A", version: new NuGetVersion("2.0.0"));
            var packageIdentityA2 = new PackageIdentity(id: "A", version: new NuGetVersion("3.0.0"));
            var packageIdentityB0 = new PackageIdentity(id: "b", version: new NuGetVersion("1.0.0"));
            var packageIdentityB1 = new PackageIdentity(id: "B", version: new NuGetVersion("2.0.0"));
            var commitId0 = Guid.NewGuid().ToString();
            var commitId1 = Guid.NewGuid().ToString();
            var commitId2 = Guid.NewGuid().ToString();
            var commitItem0 = TestUtility.CreateCatalogCommitItem(now, packageIdentityA0, commitId0);
            var commitItem1 = TestUtility.CreateCatalogCommitItem(now.AddMinutes(1), packageIdentityA1, commitId1);
            var commitItem2 = TestUtility.CreateCatalogCommitItem(now.AddMinutes(2), packageIdentityA2, commitId2);
            var commitItem3 = TestUtility.CreateCatalogCommitItem(now, packageIdentityB0, commitId0);
            var commitItem4 = TestUtility.CreateCatalogCommitItem(now.AddMinutes(1), packageIdentityB1, commitId1);

            // not in alphanumeric or chronological order
            var commitItems = new[] { commitItem4, commitItem2, commitItem0, commitItem3, commitItem1 };

            var batches = CatalogCommitUtilities.CreateCommitItemBatches(
                commitItems,
                CatalogCommitUtilities.GetPackageIdKey);

            Assert.Collection(
                batches,
                batch =>
                {
                    Assert.Equal(commitItem3.CommitTimeStamp, batch.CommitTimeStamp);
                    Assert.Collection(
                        batch.Items,
                        commit => Assert.True(ReferenceEquals(commit, commitItem3)),
                        commit => Assert.True(ReferenceEquals(commit, commitItem4)));
                },
                batch =>
                {
                    Assert.Equal(commitItem0.CommitTimeStamp, batch.CommitTimeStamp);
                    Assert.Collection(
                        batch.Items,
                        commit => Assert.True(ReferenceEquals(commit, commitItem0)),
                        commit => Assert.True(ReferenceEquals(commit, commitItem1)),
                        commit => Assert.True(ReferenceEquals(commit, commitItem2)));
                });
        }

        [Fact]
        public void CreateCommitItemBatches_WhenCommitItemsContainMultipleCommitsForSamePackageIdentity_ReturnsOnlyLatestCommitForEachPackageIdentity()
        {
            var now = DateTime.UtcNow;
            var commitItem0 = TestUtility.CreateCatalogCommitItem(now, _packageIdentitya);
            var commitItem1 = TestUtility.CreateCatalogCommitItem(now.AddMinutes(1), _packageIdentitya);
            var commitItem2 = TestUtility.CreateCatalogCommitItem(now.AddMinutes(2), _packageIdentitya);
            var commitItems = new[] { commitItem0, commitItem1, commitItem2 };

            var batches = CatalogCommitUtilities.CreateCommitItemBatches(commitItems, CatalogCommitUtilities.GetPackageIdKey);

            Assert.Collection(
                batches,
                batch =>
                {
                    Assert.Equal(commitItem0.CommitTimeStamp, batch.CommitTimeStamp);
                    Assert.Collection(
                        batch.Items,
                        commit => Assert.True(ReferenceEquals(commit, commitItem2)));
                });
        }

        [Fact]
        public void CreateCommitBatchTasksMap_WhenBatchesIsNull_Throws()
        {
            IEnumerable<CatalogCommitItemBatch> batches = null;

            var exception = Assert.Throws<ArgumentException>(
                () => CatalogCommitUtilities.CreateCommitBatchTasksMap(batches));

            Assert.Equal("batches", exception.ParamName);
        }

        [Fact]
        public void CreateCommitBatchTasksMap_WhenBatchesIsEmpty_Throws()
        {
            var batches = Enumerable.Empty<CatalogCommitItemBatch>();

            var exception = Assert.Throws<ArgumentException>(
                () => CatalogCommitUtilities.CreateCommitBatchTasksMap(batches));

            Assert.Equal("batches", exception.ParamName);
        }

        [Fact]
        public void CreateCommitBatchTasksMap_WhenArgumentsAreValid_ReturnsMap()
        {
            var commitTimeStamp = DateTime.UtcNow;

            var commit0 = TestUtility.CreateCatalogCommitItem(commitTimeStamp, _packageIdentitya);
            var commit1 = TestUtility.CreateCatalogCommitItem(commitTimeStamp.AddMinutes(1), _packageIdentitya);
            var commitBatch0 = new CatalogCommitItemBatch(commit0.CommitTimeStamp, _packageIdentitya.Id, new[] { commit0, commit1 });

            var commit2 = TestUtility.CreateCatalogCommitItem(commitTimeStamp.AddMinutes(1), _packageIdentityb);
            var commit3 = TestUtility.CreateCatalogCommitItem(commitTimeStamp.AddMinutes(2), _packageIdentityb);
            var commitBatch1 = new CatalogCommitItemBatch(commit2.CommitTimeStamp, _packageIdentityb.Id, new[] { commit2, commit3 });

            var commitBatches = new[] { commitBatch0, commitBatch1 };

            var map = CatalogCommitUtilities.CreateCommitBatchTasksMap(commitBatches);

            Assert.Collection(
                map,
                element =>
                {
                    Assert.Equal(commitTimeStamp, element.Key.ToUniversalTime());
                    Assert.Equal(commitTimeStamp, element.Value.CommitTimeStamp.ToUniversalTime());
                    Assert.Single(element.Value.BatchTasks);

                    var batchTask = element.Value.BatchTasks.Single();

                    Assert.Equal(commitBatch0.CommitTimeStamp, batchTask.MinCommitTimeStamp);
                    Assert.Equal(_packageIdentitya.Id, batchTask.Key);
                },
                element =>
                {
                    var expectedCommitTimeStamp = commitTimeStamp.AddMinutes(1);

                    Assert.Equal(expectedCommitTimeStamp, element.Key.ToUniversalTime());
                    Assert.Equal(expectedCommitTimeStamp, element.Value.CommitTimeStamp.ToUniversalTime());
                    Assert.Single(element.Value.BatchTasks);

                    var batchTask = element.Value.BatchTasks.Single();

                    Assert.Equal(commitBatch1.CommitTimeStamp, batchTask.MinCommitTimeStamp);
                    Assert.Equal(_packageIdentityb.Id, batchTask.Key);
                });
        }

        [Fact]
        public void DequeueBatchesWhileMatches_WhenBatchesIsNull_Throws()
        {
            Queue<CatalogCommitBatchTask> batches = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.DequeueBatchesWhileMatches(batches, _ => true));

            Assert.Equal("batches", exception.ParamName);
        }

        [Fact]
        public void DequeueBatchesWhileMatches_WhenIsMatchIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.DequeueBatchesWhileMatches(
                    new Queue<CatalogCommitBatchTask>(),
                    isMatch: null));

            Assert.Equal("isMatch", exception.ParamName);
        }

        [Fact]
        public void DequeueBatchesWhileMatches_WhenQueueIsEmpty_NoOps()
        {
            var batches = new Queue<CatalogCommitBatchTask>();

            CatalogCommitUtilities.DequeueBatchesWhileMatches(batches, batch => true);

            Assert.Empty(batches);
        }

        [Fact]
        public void DequeueBatchesWhileMatches_WhenNoMatchIsFound_NoOps()
        {
            var now = DateTime.UtcNow;
            var id0 = "a";
            var id1 = "b";
            var commitBatchTask0 = new CatalogCommitBatchTask(now, id0);
            var commitBatchTask1 = new CatalogCommitBatchTask(now.AddMinutes(1), id1);
            var commitBatchTask2 = new CatalogCommitBatchTask(now.AddMinutes(2), id0);

            var batches = new Queue<CatalogCommitBatchTask>();

            batches.Enqueue(commitBatchTask0);
            batches.Enqueue(commitBatchTask1);
            batches.Enqueue(commitBatchTask2);

            CatalogCommitUtilities.DequeueBatchesWhileMatches(batches, batch => false);

            Assert.Equal(3, batches.Count);
        }

        [Fact]
        public void DequeueBatchesWhileMatches_WhenMatchIsFound_Dequeues()
        {
            var now = DateTime.UtcNow;
            var id0 = "a";
            var id1 = "b";
            var commitBatchTask0 = new CatalogCommitBatchTask(now, id0);
            var commitBatchTask1 = new CatalogCommitBatchTask(now.AddMinutes(1), id1);
            var commitBatchTask2 = new CatalogCommitBatchTask(now.AddMinutes(2), id0);

            var batches = new Queue<CatalogCommitBatchTask>();

            batches.Enqueue(commitBatchTask0);
            batches.Enqueue(commitBatchTask1);
            batches.Enqueue(commitBatchTask2);

            CatalogCommitUtilities.DequeueBatchesWhileMatches(batches, batch => batch.Key == id0);

            Assert.Equal(2, batches.Count);
            Assert.Same(commitBatchTask1, batches.Dequeue());
            Assert.Same(commitBatchTask2, batches.Dequeue());
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenClientIsNull_Throws()
        {
            const CollectorHttpClient client = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    client,
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("client", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenContextIsNull_Throws()
        {
            const JToken context = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    context,
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("context", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenCommitBatchTasksMapIsNull_Throws()
        {
            const SortedDictionary<DateTime, CatalogCommitBatchTasks> commitBatchTasksMap = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    commitBatchTasksMap,
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("commitBatchTasksMap", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenUnprocessedBatchesIsNull_Throws()
        {
            const Queue<CatalogCommitItemBatch> unprocessedBatches = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    unprocessedBatches,
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("unprocessedBatches", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenProcessingBatchesIsNull_Throws()
        {
            const Queue<CatalogCommitBatchTask> processingBatches = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    processingBatches,
                    _lastBatch,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("processingBatches", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenLastBatchIsNull_Throws()
        {
            const CatalogCommitItemBatch lastBatch = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    lastBatch,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("lastBatch", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenMaxConcurrentBatchesIsLessThanOne_Throws()
        {
            const int maxConcurrentBatches = 0;

            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("maxConcurrentBatches", exception.ParamName);
        }

        [Fact]
        public void EnqueueBatchesIfNoFailures_WhenProcessCommitItemBatchAsyncIsNull_Throws()
        {
            const ProcessCommitItemBatchAsync processCommitItemBatchAsync = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new SortedDictionary<DateTime, CatalogCommitBatchTasks>(),
                    new Queue<CatalogCommitItemBatch>(),
                    new Queue<CatalogCommitBatchTask>(),
                    _lastBatch,
                    _maxConcurrentBatches,
                    processCommitItemBatchAsync,
                    CancellationToken.None));

            Assert.Equal("processCommitItemBatchAsync", exception.ParamName);
        }

        public class EnqueueBatchesIfNoFailures
        {
            private PackageIdentity _packageIdentityc = new PackageIdentity(id: "c", version: new NuGetVersion("1.0.0"));
            private PackageIdentity _packageIdentityd = new PackageIdentity(id: "d", version: new NuGetVersion("1.0.0"));
            private readonly DateTime _now = DateTime.UtcNow;
            private readonly CatalogCommitItem _commitItem0;
            private readonly CatalogCommitItem _commitItem1;
            private readonly CatalogCommitItem _commitItem2;
            private readonly CatalogCommitItem _commitItem3;

            public EnqueueBatchesIfNoFailures()
            {
                _commitItem0 = TestUtility.CreateCatalogCommitItem(_now, _packageIdentitya);
                _commitItem1 = TestUtility.CreateCatalogCommitItem(_now, _packageIdentityb);
                _commitItem2 = TestUtility.CreateCatalogCommitItem(_now.AddMinutes(1), _packageIdentityc);
                _commitItem3 = TestUtility.CreateCatalogCommitItem(_now.AddMinutes(2), _packageIdentityd);
            }

            [Fact]
            public void EnqueueBatchesIfNoFailures_WhenAnyBatchIsFailed_DoesNotEnqueue()
            {
                var commitItemBatch = new CatalogCommitItemBatch(_now, _packageIdentitya.Id, new[] { _commitItem0, _commitItem1 });
                var commitBatchMap = CatalogCommitUtilities.CreateCommitBatchTasksMap(new[] { commitItemBatch });
                var batchTasks = new CatalogCommitBatchTasks(_now);
                var failedBatchTask = new CatalogCommitBatchTask(_now, _packageIdentitya.Id) { Task = FailedTask };

                batchTasks.BatchTasks.Add(failedBatchTask);

                var unprocessedBatches = new Queue<CatalogCommitItemBatch>();

                unprocessedBatches.Enqueue(commitItemBatch);

                var processingBatches = new Queue<CatalogCommitBatchTask>();

                processingBatches.Enqueue(failedBatchTask);

                CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    commitBatchMap,
                    unprocessedBatches,
                    processingBatches,
                    _lastBatch,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None);

                Assert.Equal(1, unprocessedBatches.Count);
                Assert.Equal(1, processingBatches.Count);
            }

            [Fact]
            public void EnqueueBatchesIfNoFailures_WhenNoBatchIsCancelled_DoesNotEnqueue()
            {
                var commitItemBatch = new CatalogCommitItemBatch(_now, _packageIdentitya.Id, new[] { _commitItem0, _commitItem1 });
                var commitBatchMap = CatalogCommitUtilities.CreateCommitBatchTasksMap(new[] { commitItemBatch });
                var batchTasks = new CatalogCommitBatchTasks(_now);
                var cancelledBatchTask = new CatalogCommitBatchTask(_now, _packageIdentitya.Id)
                {
                    Task = Task.FromCanceled(new CancellationToken(canceled: true))
                };

                batchTasks.BatchTasks.Add(cancelledBatchTask);

                var unprocessedBatches = new Queue<CatalogCommitItemBatch>();

                unprocessedBatches.Enqueue(commitItemBatch);

                var processingBatches = new Queue<CatalogCommitBatchTask>();

                processingBatches.Enqueue(cancelledBatchTask);

                CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    commitBatchMap,
                    unprocessedBatches,
                    processingBatches,
                    _lastBatch,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None);

                Assert.Equal(1, unprocessedBatches.Count);
                Assert.Equal(1, processingBatches.Count);
            }

            [Fact]
            public void EnqueueBatchesIfNoFailures_WhenMaxConcurrencyLimitHit_DoesNotEnqueue()
            {
                var commitItemBatch0 = new CatalogCommitItemBatch(_now, _packageIdentitya.Id, new[] { _commitItem0 });
                var commitItemBatch1 = new CatalogCommitItemBatch(_now, _packageIdentityc.Id, new[] { _commitItem2 });
                var commitBatchMap = CatalogCommitUtilities.CreateCommitBatchTasksMap(new[] { commitItemBatch0, commitItemBatch1 });
                var batchTasks = new CatalogCommitBatchTasks(_now);

                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    var inprocessTask = new CatalogCommitBatchTask(_now, _packageIdentitya.Id)
                    {
                        Task = Task.Delay(TimeSpan.FromMilliseconds(-1), cancellationTokenSource.Token)
                    };

                    var unprocessedBatches = new Queue<CatalogCommitItemBatch>();

                    unprocessedBatches.Enqueue(commitItemBatch1);

                    var processingBatches = new Queue<CatalogCommitBatchTask>();

                    processingBatches.Enqueue(inprocessTask);

                    const int maxConcurrentBatches = 1;

                    CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                        new CollectorHttpClient(),
                        new JObject(),
                        commitBatchMap,
                        unprocessedBatches,
                        processingBatches,
                        _lastBatch,
                        maxConcurrentBatches,
                        NoOpProcessBatchAsync,
                        CancellationToken.None);

                    Assert.Equal(1, unprocessedBatches.Count);
                    Assert.Equal(1, processingBatches.Count);
                }
            }

            [Fact]
            public void EnqueueBatchesIfNoFailures_WhenCanEnqueue_Enqueues()
            {
                var commitItemBatch = new CatalogCommitItemBatch(_now, _packageIdentitya.Id, new[] { _commitItem0 });
                var commitBatchMap = CatalogCommitUtilities.CreateCommitBatchTasksMap(new[] { commitItemBatch });
                var batchTasks = new CatalogCommitBatchTasks(_now);

                var unprocessedBatches = new Queue<CatalogCommitItemBatch>();

                unprocessedBatches.Enqueue(commitItemBatch);

                var processingBatches = new Queue<CatalogCommitBatchTask>();

                CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    commitBatchMap,
                    unprocessedBatches,
                    processingBatches,
                    _lastBatch,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None);

                Assert.Equal(0, unprocessedBatches.Count);
                Assert.Equal(1, processingBatches.Count);
            }

            [Fact]
            public void EnqueueBatchesIfNoFailures_WhenProcessingQueueContainsCompletedTasks_Enqueues()
            {
                var commitItemBatch0 = new CatalogCommitItemBatch(_now, _packageIdentitya.Id, new[] { _commitItem0 });
                var commitItemBatch1 = new CatalogCommitItemBatch(_now, _packageIdentityb.Id, new[] { _commitItem1 });
                var commitBatchMap = CatalogCommitUtilities.CreateCommitBatchTasksMap(new[] { commitItemBatch0, commitItemBatch1 });
                var batchTasks = new CatalogCommitBatchTasks(_now);
                var completedTask = new CatalogCommitBatchTask(_now, _packageIdentitya.Id) { Task = Task.CompletedTask };

                var unprocessedBatches = new Queue<CatalogCommitItemBatch>();

                unprocessedBatches.Enqueue(commitItemBatch1);

                var processingBatches = new Queue<CatalogCommitBatchTask>();

                processingBatches.Enqueue(completedTask);

                CatalogCommitUtilities.EnqueueBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    commitBatchMap,
                    unprocessedBatches,
                    processingBatches,
                    _lastBatch,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None);

                Assert.Equal(0, unprocessedBatches.Count);
                Assert.Equal(2, processingBatches.Count);
            }
        }

        [Fact]
        public void GetPackageIdKey_WhenItemIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.GetPackageIdKey(item: null));

            Assert.Equal("item", exception.ParamName);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("A")]
        public void GetPackageIdKey_WhenPackageIdVariesInCase_ReturnsLowerCase(string packageId)
        {
            var commitItem = TestUtility.CreateCatalogCommitItem(DateTime.UtcNow, new PackageIdentity(packageId, new NuGetVersion("1.0.0")));
            var key = CatalogCommitUtilities.GetPackageIdKey(commitItem);

            Assert.Equal(packageId.ToLowerInvariant(), key);
        }

        private static CatalogCommitItemBatch CreateCatalogCommitBatch(
            DateTime commitTimeStamp,
            PackageIdentity packageIdentity)
        {
            var commit = TestUtility.CreateCatalogCommitItem(commitTimeStamp, packageIdentity);

            return new CatalogCommitItemBatch(commitTimeStamp, packageIdentity.Id, new[] { commit });
        }

        private static Task NoOpProcessBatchAsync(
            CollectorHttpClient client,
            JToken context,
            string packageId,
            CatalogCommitItemBatch batch,
            CatalogCommitItemBatch lastBatch,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
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

            _lastBatch = new CatalogCommitItemBatch(new[] { commit }, _packageIdentitya.Id);
        }

        [Fact]
        public void CreateCommitItemBatches_WhenCatalogItemsIsNull_Throws()
        {
            IEnumerable<CatalogCommitItem> catalogItems = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.CreateCommitItemBatches(
                    catalogItems,
                    CatalogCommitUtilities.GetPackageIdKey));

            Assert.Equal("catalogItems", exception.ParamName);
        }

        [Fact]
        public void CreateCommitItemBatches_WhenGetCatalogCommitItemKeyIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.CreateCommitItemBatches(
                    Enumerable.Empty<CatalogCommitItem>(),
                    getCatalogCommitItemKey: null));

            Assert.Equal("getCatalogCommitItemKey", exception.ParamName);
        }

        [Fact]
        public void CreateCommitItemBatches_WhenMultipleCommitItemsShareCommitTimeStampButNotCommitId_Throws()
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

            var exception = Assert.Throws<ArgumentException>(
                () => CatalogCommitUtilities.CreateCommitItemBatches(
                    commitItems,
                    CatalogCommitUtilities.GetPackageIdKey));

            Assert.Equal("catalogItems", exception.ParamName);
            Assert.StartsWith("Multiple commits exist with the same commit timestamp but different commit ID's:  " +
                $"{{ CommitId = {commitItem0.CommitId}, CommitTimeStamp = {commitItem0.CommitTimeStamp.ToString("O")} }}, " +
                $"{{ CommitId = {commitItem1.CommitId}, CommitTimeStamp = {commitItem1.CommitTimeStamp.ToString("O")} }}.",
                exception.Message);
        }

        [Fact]
        public void CreateCommitItemBatches_WhenMultipleCommitItemsShareCommitTimeStampButNotCommitIdAndLaterCommitExists_DoesNotThrow()
        {
            var commitTimeStamp0 = DateTime.UtcNow;
            var commitTimeStamp1 = commitTimeStamp0.AddMinutes(1);
            var context = TestUtility.CreateCatalogContextJObject();
            var commitItem0 = CatalogCommitItem.Create(
                context,
                TestUtility.CreateCatalogCommitItemJObject(commitTimeStamp0, _packageIdentitya));
            var commitItem1 = CatalogCommitItem.Create(
                context,
                TestUtility.CreateCatalogCommitItemJObject(commitTimeStamp0, _packageIdentityb));
            var commitItem2 = CatalogCommitItem.Create(
                context,
                TestUtility.CreateCatalogCommitItemJObject(commitTimeStamp1, _packageIdentitya));
            var commitItems = new[] { commitItem0, commitItem1, commitItem2 };

            var batches = CatalogCommitUtilities.CreateCommitItemBatches(
                commitItems,
                CatalogCommitUtilities.GetPackageIdKey);

            Assert.Collection(
                batches,
                batch =>
                {
                    Assert.Equal(commitTimeStamp1, batch.CommitTimeStamp.ToUniversalTime());
                    Assert.Collection(
                        batch.Items,
                        commit => Assert.True(ReferenceEquals(commit, commitItem2)));
                },
                batch =>
                {
                    Assert.Equal(commitTimeStamp0, batch.CommitTimeStamp.ToUniversalTime());
                    Assert.Collection(
                        batch.Items,
                        commit => Assert.True(ReferenceEquals(commit, commitItem1)));
                });
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
        public void CreateCommitItemBatches_WhenCommitItemsContainMultipleCommitsForSamePackageIdentity_ReturnsOnlyLastCommitForEachPackageIdentity()
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
                    Assert.Equal(commitItem2.CommitTimeStamp, batch.CommitTimeStamp);
                    Assert.Collection(
                        batch.Items,
                        commit => Assert.True(ReferenceEquals(commit, commitItem2)));
                });
        }

        [Fact]
        public void StartProcessingBatchesIfNoFailures_WhenClientIsNull_Throws()
        {
            const CollectorHttpClient client = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.StartProcessingBatchesIfNoFailures(
                    client,
                    new JObject(),
                    new List<CatalogCommitItemBatch>(),
                    new List<CatalogCommitItemBatchTask>(),
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("client", exception.ParamName);
        }

        [Fact]
        public void StartProcessingBatchesIfNoFailures_WhenContextIsNull_Throws()
        {
            const JToken context = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.StartProcessingBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    context,
                    new List<CatalogCommitItemBatch>(),
                    new List<CatalogCommitItemBatchTask>(),
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("context", exception.ParamName);
        }

        [Fact]
        public void StartProcessingBatchesIfNoFailures_WhenUnprocessedBatchesIsNull_Throws()
        {
            const List<CatalogCommitItemBatch> unprocessedBatches = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.StartProcessingBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    unprocessedBatches,
                    new List<CatalogCommitItemBatchTask>(),
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("unprocessedBatches", exception.ParamName);
        }

        [Fact]
        public void StartProcessingBatchesIfNoFailures_WhenProcessingBatchesIsNull_Throws()
        {
            const List<CatalogCommitItemBatchTask> processingBatches = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.StartProcessingBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new List<CatalogCommitItemBatch>(),
                    processingBatches,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("processingBatches", exception.ParamName);
        }

        [Fact]
        public void StartProcessingBatchesIfNoFailures_WhenMaxConcurrentBatchesIsLessThanOne_Throws()
        {
            const int maxConcurrentBatches = 0;

            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => CatalogCommitUtilities.StartProcessingBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new List<CatalogCommitItemBatch>(),
                    new List<CatalogCommitItemBatchTask>(),
                    maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None));

            Assert.Equal("maxConcurrentBatches", exception.ParamName);
        }

        [Fact]
        public void StartProcessingBatchesIfNoFailures_WhenProcessCommitItemBatchAsyncIsNull_Throws()
        {
            const ProcessCommitItemBatchAsync processCommitItemBatchAsync = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitUtilities.StartProcessingBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    new List<CatalogCommitItemBatch>(),
                    new List<CatalogCommitItemBatchTask>(),
                    _maxConcurrentBatches,
                    processCommitItemBatchAsync,
                    CancellationToken.None));

            Assert.Equal("processCommitItemBatchAsync", exception.ParamName);
        }

        public class StartProcessingBatchesIfNoFailures
        {
            private PackageIdentity _packageIdentityc = new PackageIdentity(id: "c", version: new NuGetVersion("1.0.0"));
            private PackageIdentity _packageIdentityd = new PackageIdentity(id: "d", version: new NuGetVersion("1.0.0"));
            private readonly DateTime _now = DateTime.UtcNow;
            private readonly CatalogCommitItem _commitItem0;
            private readonly CatalogCommitItem _commitItem1;
            private readonly CatalogCommitItem _commitItem2;
            private readonly CatalogCommitItem _commitItem3;

            public StartProcessingBatchesIfNoFailures()
            {
                _commitItem0 = TestUtility.CreateCatalogCommitItem(_now, _packageIdentitya);
                _commitItem1 = TestUtility.CreateCatalogCommitItem(_now, _packageIdentityb);
                _commitItem2 = TestUtility.CreateCatalogCommitItem(_now.AddMinutes(1), _packageIdentityc);
                _commitItem3 = TestUtility.CreateCatalogCommitItem(_now.AddMinutes(2), _packageIdentityd);
            }

            [Fact]
            public void StartProcessingBatchesIfNoFailures_WhenAnyBatchIsFailed_DoesNotStartNewBatch()
            {
                var commitItemBatch = new CatalogCommitItemBatch(
                    new[] { _commitItem0, _commitItem1 },
                    _packageIdentitya.Id);
                var failedBatchTask = new CatalogCommitItemBatchTask(commitItemBatch, FailedTask);
                var unprocessedBatches = new List<CatalogCommitItemBatch>() { commitItemBatch };
                var processingBatches = new List<CatalogCommitItemBatchTask>() { failedBatchTask };

                CatalogCommitUtilities.StartProcessingBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    unprocessedBatches,
                    processingBatches,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None);

                Assert.Single(unprocessedBatches);
                Assert.Single(processingBatches);
            }

            [Fact]
            public void StartProcessingBatchesIfNoFailures_WhenNoBatchIsCancelled_DoesNotStartNewBatch()
            {
                var commitItemBatch = new CatalogCommitItemBatch(
                    new[] { _commitItem0, _commitItem1 },
                    _packageIdentitya.Id);
                var cancelledBatchTask = new CatalogCommitItemBatchTask(
                    commitItemBatch,
                    Task.FromCanceled(new CancellationToken(canceled: true)));
                var unprocessedBatches = new List<CatalogCommitItemBatch>() { commitItemBatch };
                var processingBatches = new List<CatalogCommitItemBatchTask>() { cancelledBatchTask };

                CatalogCommitUtilities.StartProcessingBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    unprocessedBatches,
                    processingBatches,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None);

                Assert.Single(unprocessedBatches);
                Assert.Single(processingBatches);
            }

            [Fact]
            public void StartProcessingBatchesIfNoFailures_WhenMaxConcurrencyLimitHit_DoesNotStartNewBatch()
            {
                var commitItemBatch0 = new CatalogCommitItemBatch(new[] { _commitItem0 }, _packageIdentitya.Id);
                var commitItemBatch1 = new CatalogCommitItemBatch(new[] { _commitItem2 }, _packageIdentityc.Id);

                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    var inProcessTask = new CatalogCommitItemBatchTask(
                        commitItemBatch0,
                        Task.Delay(TimeSpan.FromMilliseconds(-1), cancellationTokenSource.Token));
                    var unprocessedBatches = new List<CatalogCommitItemBatch>() { commitItemBatch1 };
                    var processingBatches = new List<CatalogCommitItemBatchTask>() { inProcessTask };

                    const int maxConcurrentBatches = 1;

                    CatalogCommitUtilities.StartProcessingBatchesIfNoFailures(
                        new CollectorHttpClient(),
                        new JObject(),
                        unprocessedBatches,
                        processingBatches,
                        maxConcurrentBatches,
                        NoOpProcessBatchAsync,
                        CancellationToken.None);

                    Assert.Single(unprocessedBatches);
                    Assert.Single(processingBatches);
                }
            }

            [Fact]
            public void StartProcessingBatchesIfNoFailures_WhenCanStartNewBatch_StartsNewBatch()
            {
                var commitItemBatch = new CatalogCommitItemBatch(new[] { _commitItem0 }, _packageIdentitya.Id);
                var unprocessedBatches = new List<CatalogCommitItemBatch>() { commitItemBatch };
                var processingBatches = new List<CatalogCommitItemBatchTask>();

                CatalogCommitUtilities.StartProcessingBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    unprocessedBatches,
                    processingBatches,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None);

                Assert.Empty(unprocessedBatches);
                Assert.Single(processingBatches);
            }

            [Fact]
            public void StartProcessingBatchesIfNoFailures_WhenProcessingQueueContainsCompletedTasks_StartsNewBatch()
            {
                var commitItemBatch0 = new CatalogCommitItemBatch(new[] { _commitItem0 }, _packageIdentitya.Id);
                var commitItemBatch1 = new CatalogCommitItemBatch(new[] { _commitItem1 }, _packageIdentityb.Id);
                var completedTask = new CatalogCommitItemBatchTask(commitItemBatch0, Task.CompletedTask);
                var unprocessedBatches = new List<CatalogCommitItemBatch>() { commitItemBatch1 };
                var processingBatches = new List<CatalogCommitItemBatchTask>() { completedTask };

                CatalogCommitUtilities.StartProcessingBatchesIfNoFailures(
                    new CollectorHttpClient(),
                    new JObject(),
                    unprocessedBatches,
                    processingBatches,
                    _maxConcurrentBatches,
                    NoOpProcessBatchAsync,
                    CancellationToken.None);

                Assert.Empty(unprocessedBatches);
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
            var commitItem = TestUtility.CreateCatalogCommitItem(
                DateTime.UtcNow,
                new PackageIdentity(packageId, new NuGetVersion("1.0.0")));
            var key = CatalogCommitUtilities.GetPackageIdKey(commitItem);

            Assert.Equal(packageId.ToLowerInvariant(), key);
        }

        private static CatalogCommitItemBatch CreateCatalogCommitBatch(
            DateTime commitTimeStamp,
            PackageIdentity packageIdentity)
        {
            var commit = TestUtility.CreateCatalogCommitItem(commitTimeStamp, packageIdentity);

            return new CatalogCommitItemBatch(new[] { commit }, packageIdentity.Id);
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
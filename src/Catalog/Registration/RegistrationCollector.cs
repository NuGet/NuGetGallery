// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationCollector : SortingGraphCollector
    {
        public const int PartitionSize = 64;
        public const int PackageCountThreshold = 128;

        // This is simply the arbitrary limit that I tested.  There may be a better value.
        public const int DefaultMaxConcurrentBatches = 10;

        private readonly StorageFactory _legacyStorageFactory;
        private readonly StorageFactory _semVer2StorageFactory;
        private readonly ShouldIncludeRegistrationPackage _shouldIncludeSemVer2;
        private readonly int _maxConcurrentBatches;

        public RegistrationCollector(
            Uri index,
            StorageFactory legacyStorageFactory,
            StorageFactory semVer2StorageFactory,
            Uri contentBaseAddress,
            ITelemetryService telemetryService,
            Func<HttpMessageHandler> handlerFunc = null,
            int maxConcurrentBatches = DefaultMaxConcurrentBatches)
            : base(index, new Uri[] { Schema.DataTypes.PackageDetails, Schema.DataTypes.PackageDelete }, telemetryService, handlerFunc)
        {
            _legacyStorageFactory = legacyStorageFactory ?? throw new ArgumentNullException(nameof(legacyStorageFactory));
            _semVer2StorageFactory = semVer2StorageFactory;
            _shouldIncludeSemVer2 = GetShouldIncludeRegistrationPackage(_semVer2StorageFactory);
            ContentBaseAddress = contentBaseAddress;

            if (maxConcurrentBatches < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentBatches),
                    maxConcurrentBatches,
                    string.Format(Strings.ArgumentOutOfRange, 1, int.MaxValue));
            }

            _maxConcurrentBatches = maxConcurrentBatches;
        }

        public Uri ContentBaseAddress { get; }

        protected override Task<IEnumerable<CatalogItemBatch>> CreateBatchesAsync(IEnumerable<CatalogItem> catalogItems)
        {
            // Grouping batches by commit is slow if it contains
            // the same package registration id over and over again.
            // This happens when, for example, a package publish "wave"
            // occurs.
            //
            // If one package registration id is part of 20 batches,
            // we'll have to process all registration leafs 20 times.
            // It would be better to process these leafs only once.
            //
            // So let's batch by package registration id here,
            // ensuring we never write a commit timestamp to the cursor
            // that is higher than the last item currently processed.
            //
            // So, group by id, then make sure the batch key is the
            // *lowest*  timestamp of all commits in that batch.
            // This ensures that on retries, we will retry
            // from the correct location (even though we may have
            // a little rework).

            var batches = catalogItems
                .GroupBy(item => GetKey(item.Value))
                .Select(group => new CatalogItemBatch(
                    group.Min(item => item.CommitTimeStamp),
                    group));

            return Task.FromResult(batches);
        }

        private async Task<CatalogItemBatch[]> CreateBatchesAsync(ReadWriteCursor front, ReadCursor back, JObject page)
        {
            IEnumerable<CatalogItem> pageItems = page["items"]
                .Select(item => new CatalogItem((JObject)item))
                .Where(item => item.CommitTimeStamp > front.Value && item.CommitTimeStamp <= back.Value);

            IEnumerable<CatalogItemBatch> batches = await CreateBatchesAsync(pageItems);

            return batches
                .OrderBy(batch => batch.CommitTimeStamp)
                .ToArray();
        }

        protected override string GetKey(JObject item)
        {
            return item["nuget:id"].ToString().ToLowerInvariant();
        }

        // Summary:
        //
        //      1.  Process one catalog page at a time.
        //      2.  Within a given catalog page, batch catalog commit entries by lower-cased package ID.
        //      3.  Process up to `n` batches in parallel.  Note that the batches may span multiple catalog commits.
        //      4.  Cease processing new batches if a failure has been observed.  This job will eventually retry
        //          batches on its next outermost job loop.
        //      5.  If a failure has been observed, wait for all existing tasks to complete.  Avoid task cancellation
        //          as that could lead to the entirety of a package registration being in an inconsistent state.
        //          To be fair, a well-timed exception could have the same result, but registration updates have never
        //          been transactional.  Actively cancelling tasks would make an inconsistent registration more likely.
        //      6.  Update the cursor if and only if all preceding commits and the current (oldest) commit have been
        //          fully and successfully processed.
        protected override async Task<bool> FetchAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            ReadCursor back,
            CancellationToken cancellationToken)
        {
            IEnumerable<CatalogItem> catalogItems = await FetchCatalogItemsAsync(client, front, cancellationToken);

            var hasAnyBatchFailed = false;
            var hasAnyBatchBeenProcessed = false;

            foreach (CatalogItem catalogItem in catalogItems)
            {
                JObject page = await client.GetJObjectAsync(catalogItem.Uri, cancellationToken);
                JToken context = page["@context"];
                CatalogItemBatch[] batches = await CreateBatchesAsync(front, back, page);
                SortedDictionary<DateTime, CommitBatchTasks> commitBatchTasksMap = CreateCommitBatchTasksMap(batches);

                var unprocessedBatches = new Queue<CatalogItemBatch>(batches);
                var processingBatches = new Queue<BatchTask>();

                CatalogItemBatch lastBatch = unprocessedBatches.LastOrDefault();
                var exceptions = new List<Exception>();

                EnqueueBatchesIfNoFailures(
                    client,
                    context,
                    commitBatchTasksMap,
                    unprocessedBatches,
                    processingBatches,
                    lastBatch,
                    cancellationToken);

                while (processingBatches.Any())
                {
                    await Task.WhenAny(processingBatches.Select(batch => batch.Task));

                    while (!hasAnyBatchFailed && commitBatchTasksMap.Any())
                    {
                        var commitBatchTasks = commitBatchTasksMap.First().Value;
                        var isCommitFullyProcessed = commitBatchTasks.BatchTasks.All(batch => batch.Task != null && batch.Task.IsCompleted);

                        if (!isCommitFullyProcessed)
                        {
                            break;
                        }

                        var isCommitSuccessfullyProcessed = commitBatchTasks.BatchTasks.All(batch => batch.Task.Status == TaskStatus.RanToCompletion);

                        if (isCommitSuccessfullyProcessed)
                        {
                            var commitTimeStamp = commitBatchTasks.CommitTimeStamp;

                            front.Value = commitTimeStamp;

                            await front.SaveAsync(cancellationToken);

                            Trace.TraceInformation($"{nameof(RegistrationCollector)}.{nameof(FetchAsync)} {nameof(front)}.{nameof(front.Value)} saved since timestamp changed from previous: {{0}}", front);

                            DequeueBatchesWhileMatches(processingBatches, batch => batch.CommitTimeStamp == commitTimeStamp);

                            commitBatchTasksMap.Remove(commitTimeStamp);
                        }
                        else // Canceled or Failed
                        {
                            hasAnyBatchFailed = true;

                            exceptions.AddRange(
                                commitBatchTasks.BatchTasks
                                    .Select(batch => batch.Task)
                                    .Where(task => (task.IsFaulted || task.IsCanceled) && task.Exception != null)
                                    .Select(task => task.Exception));
                        }
                    }

                    if (hasAnyBatchFailed)
                    {
                        DequeueBatchesWhileMatches(processingBatches, batch => batch.Task.IsCompleted);
                    }

                    hasAnyBatchBeenProcessed = true;

                    EnqueueBatchesIfNoFailures(
                        client,
                        context,
                        commitBatchTasksMap,
                        unprocessedBatches,
                        processingBatches,
                        lastBatch,
                        cancellationToken);
                }

                if (hasAnyBatchFailed)
                {
                    var innerException = exceptions.Count == 1 ? exceptions.Single() : new AggregateException(exceptions);

                    throw new BatchProcessingException(innerException);
                }
            }

            return hasAnyBatchBeenProcessed;
        }

        protected override async Task ProcessGraphsAsync(
            KeyValuePair<string, IReadOnlyDictionary<string, IGraph>> sortedGraphs,
            CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            using (_telemetryService.TrackDuration(TelemetryConstants.ProcessGraphsSeconds,
                new Dictionary<string, string>()
                {
                    { TelemetryConstants.Id, sortedGraphs.Key.ToLowerInvariant() }
                }))
            {
                var legacyTask = RegistrationMaker.ProcessAsync(
                    registrationKey: new RegistrationKey(sortedGraphs.Key),
                    newItems: sortedGraphs.Value,
                    shouldInclude: _shouldIncludeSemVer2,
                    storageFactory: _legacyStorageFactory,
                    contentBaseAddress: ContentBaseAddress,
                    partitionSize: PartitionSize,
                    packageCountThreshold: PackageCountThreshold,
                    telemetryService: _telemetryService,
                    cancellationToken: cancellationToken);
                tasks.Add(legacyTask);

                if (_semVer2StorageFactory != null)
                {
                    var semVer2Task = RegistrationMaker.ProcessAsync(
                       registrationKey: new RegistrationKey(sortedGraphs.Key),
                       newItems: sortedGraphs.Value,
                       storageFactory: _semVer2StorageFactory,
                       contentBaseAddress: ContentBaseAddress,
                       partitionSize: PartitionSize,
                       packageCountThreshold: PackageCountThreshold,
                       telemetryService: _telemetryService,
                       cancellationToken: cancellationToken);
                    tasks.Add(semVer2Task);
                }

                await Task.WhenAll(tasks);
            }
        }

        public static ShouldIncludeRegistrationPackage GetShouldIncludeRegistrationPackage(StorageFactory semVer2StorageFactory)
        {
            // If SemVer 2.0.0 storage is disabled, put SemVer 2.0.0 registration in the legacy storage factory. In no
            // case should a package be completely ignored. That is, if a package is SemVer 2.0.0 but SemVer 2.0.0
            // storage is not enabled, our only choice is to put SemVer 2.0.0 packages in the legacy storage.
            if (semVer2StorageFactory == null)
            {
                return (k, u, g) => true;
            }

            return (k, u, g) => !NuGetVersionUtility.IsGraphSemVer2(k.Version, u, g);
        }

        private static void DequeueBatchesWhileMatches(Queue<BatchTask> batches, Func<BatchTask, bool> isMatch)
        {
            BatchTask batch;

            while ((batch = batches.FirstOrDefault()) != null)
            {
                if (isMatch(batch))
                {
                    batches.Dequeue();
                }
                else
                {
                    break;
                }
            }
        }

        private void EnqueueBatchesIfNoFailures(
            CollectorHttpClient client,
            JToken context,
            SortedDictionary<DateTime, CommitBatchTasks> commitBatchTasksMap,
            Queue<CatalogItemBatch> unprocessedBatches,
            Queue<BatchTask> processingBatches,
            CatalogItemBatch lastBatch,
            CancellationToken cancellationToken)
        {
            var hasAnyBatchFailed = processingBatches.Any(batch => batch.Task.IsFaulted || batch.Task.IsCanceled);

            if (hasAnyBatchFailed)
            {
                return;
            }

            var batchesToEnqueue = Math.Min(
                _maxConcurrentBatches - processingBatches.Count(batch => !batch.Task.IsCompleted),
                unprocessedBatches.Count);

            for (var i = 0; i < batchesToEnqueue; ++i)
            {
                var batch = unprocessedBatches.Dequeue();
                var batchItem = batch.Items.First();
                var packageId = GetKey(batchItem.Value);

                var batchTask = commitBatchTasksMap[batchItem.CommitTimeStamp].BatchTasks
                    .Single(bt => bt.PackageId == packageId);

                batchTask.Task = ProcessBatchAsync(client, context, packageId, batch, lastBatch, cancellationToken);

                processingBatches.Enqueue(batchTask);
            }
        }

        private async Task ProcessBatchAsync(
            CollectorHttpClient client,
            JToken context,
            string packageId,
            CatalogItemBatch batch,
            CatalogItemBatch lastBatch,
            CancellationToken cancellationToken)
        {
            await Task.Yield();

            using (_telemetryService.TrackDuration(
                TelemetryConstants.ProcessBatchSeconds,
                new Dictionary<string, string>()
                {
                    { TelemetryConstants.Id, packageId },
                    { TelemetryConstants.BatchItemCount, batch.Items.Count.ToString() }
                }))
            {
                await OnProcessBatchAsync(
                    client,
                    batch.Items.Select(item => item.Value),
                    context,
                    batch.CommitTimeStamp,
                    batch.CommitTimeStamp == lastBatch.CommitTimeStamp,
                    cancellationToken);
            }
        }

        private SortedDictionary<DateTime, CommitBatchTasks> CreateCommitBatchTasksMap(CatalogItemBatch[] batches)
        {
            var map = new SortedDictionary<DateTime, CommitBatchTasks>();

            foreach (var batch in batches)
            {
                var jObject = batch.Items.First().Value;
                var packageId = GetKey(jObject);
                var batchTask = new BatchTask(batch.CommitTimeStamp, packageId);

                foreach (var commitTimeStamp in batch.Items.Select(item => item.CommitTimeStamp))
                {
                    CommitBatchTasks commitBatchTasks;

                    if (!map.TryGetValue(commitTimeStamp, out commitBatchTasks))
                    {
                        commitBatchTasks = new CommitBatchTasks(commitTimeStamp);

                        map[commitTimeStamp] = commitBatchTasks;
                    }

                    commitBatchTasks.BatchTasks.Add(batchTask);
                }
            }

            return map;
        }

        private sealed class BatchTask
        {
            internal BatchTask(DateTime commitTimeStamp, string packageId)
            {
                CommitTimeStamp = commitTimeStamp;
                PackageId = packageId;
            }

            internal DateTime CommitTimeStamp { get; }
            internal string PackageId { get; }
            internal Task Task { get; set; }

            public override int GetHashCode()
            {
                return PackageId.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                var other = obj as BatchTask;

                if (ReferenceEquals(other, null))
                {
                    return false;
                }

                return GetHashCode() == other.GetHashCode();
            }
        }

        private sealed class CommitBatchTasks
        {
            internal CommitBatchTasks(DateTime commitTimeStamp)
            {
                BatchTasks = new HashSet<BatchTask>();
                CommitTimeStamp = commitTimeStamp;
            }

            internal HashSet<BatchTask> BatchTasks { get; }
            internal DateTime CommitTimeStamp { get; }
        }
    }
}
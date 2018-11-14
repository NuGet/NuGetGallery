// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ExceptionUtilities = NuGet.Common.ExceptionUtilities;

namespace NuGet.Services.Metadata.Catalog
{
    public static class CatalogCommitUtilities
    {
        private static readonly EventId _eventId = new EventId(id: 0);

        /// <summary>
        /// Creates an enumerable of <see cref="CatalogCommitItemBatch" /> instances.
        /// </summary>
        /// <remarks>
        /// A <see cref="CatalogCommitItemBatch" /> instance contains only the latest commit for each package identity.
        /// </remarks>
        /// <param name="catalogItems">An enumerable of <see cref="CatalogCommitItem" />.</param>
        /// <param name="getCatalogCommitItemKey">A function that returns a key for a <see cref="CatalogCommitItem" />.</param>
        /// <returns>An enumerable of <see cref="CatalogCommitItemBatch" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="catalogItems" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="getCatalogCommitItemKey" /> is <c>null</c>.</exception>
        public static IEnumerable<CatalogCommitItemBatch> CreateCommitItemBatches(
            IEnumerable<CatalogCommitItem> catalogItems,
            GetCatalogCommitItemKey getCatalogCommitItemKey)
        {
            if (catalogItems == null)
            {
                throw new ArgumentNullException(nameof(catalogItems));
            }

            if (getCatalogCommitItemKey == null)
            {
                throw new ArgumentNullException(nameof(getCatalogCommitItemKey));
            }

            AssertNotMoreThanOneCommitIdPerCommitTimeStamp(catalogItems);

            var catalogItemsGroups = catalogItems
                .GroupBy(catalogItem => getCatalogCommitItemKey(catalogItem));

            foreach (var catalogItemsGroup in catalogItemsGroups)
            {
                // Before filtering out all but the latest commit for each package identity, determine the earliest
                // commit timestamp for all items in this batch.  This timestamp is important for processing commits
                // in chronological order.
                var minCommitTimeStamp = catalogItemsGroup.Select(commitItem => commitItem.CommitTimeStamp).Min();
                var catalogItemsWithOnlyLatestCommitForEachPackageIdentity = catalogItemsGroup
                    .GroupBy(commitItem => new
                    {
                        PackageId = commitItem.PackageIdentity.Id.ToLowerInvariant(),
                        PackageVersion = commitItem.PackageIdentity.Version.ToNormalizedString().ToLowerInvariant()
                    })
                    .Select(group => group.OrderBy(item => item.CommitTimeStamp).Last());

                yield return new CatalogCommitItemBatch(
                    minCommitTimeStamp,
                    catalogItemsGroup.Key,
                    catalogItemsWithOnlyLatestCommitForEachPackageIdentity);
            }
        }

        private static void AssertNotMoreThanOneCommitIdPerCommitTimeStamp(IEnumerable<CatalogCommitItem> catalogItems)
        {
            var commitsWithDifferentCommitIds = catalogItems.GroupBy(catalogItem => catalogItem.CommitTimeStamp)
                .Where(group => group.Select(item => item.CommitId).Distinct().Count() > 1);

            if (commitsWithDifferentCommitIds.Any())
            {
                var commits = commitsWithDifferentCommitIds.SelectMany(group => group)
                    .Select(commit => $"{{ CommitId = {commit.CommitId}, CommitTimeStamp = {commit.CommitTimeStamp.ToString("O")} }}");

                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.MultipleCommitIdsForSameCommitTimeStamp,
                        string.Join(", ", commits)),
                    nameof(catalogItems));
            }
        }

        /// <summary>
        /// Generate a map of commit timestamps to commit batch tasks.
        /// </summary>
        /// <remarks>
        /// Where P represents a package and T a commit timestamp, suppose the following catalog commits:
        ///
        ///            P₀   P₁   P₂   P₃
        ///     ^      ------------------
        ///     |      T₃   T₃
        ///     |                     T₂
        ///     |      T₁        T₁
        ///   time     T₀   T₀
        ///
        /// For a fixed catalog commit timestamp range (e.g.:  T₀-T₃), each column will contain all relevant
        /// catalog commits for its corresponding package.
        ///
        /// Each column is represented by a <see cref="CatalogCommitBatchTask" /> instance.  Each group of columns
        /// sharing the same minimum commit timestamp is represented by a <see cref="CatalogCommitBatchTasks" /> instance.
        ///
        /// For the example above, this method would return a map with the following entries
        ///
        ///     { T₀, new CatalogCommitBatchTasks(T₀,
        ///         new[]
        ///         {
        ///             new CatalogCommitBatchTask(T₀, P₀, [T₀, T₁, T₃]),
        ///             new CatalogCommitBatchTask(T₀, P₁, [T₀, T₃])
        ///         }
        ///     },
        ///     { T₁, new CatalogCommitBatchTasks(T₁,
        ///         new[]
        ///         {
        ///             new CatalogCommitBatchTask(T₁, P₂, [T₁])
        ///         }
        ///     },
        ///     { T₂, new CatalogCommitBatchTasks(T₂,
        ///         new[]
        ///         {
        ///             new CatalogCommitBatchTask(T₂, P₃, [P₃])
        ///         }
        ///     }
        ///
        /// Note #1:  typically only the latest commit for each package identity need be processed.  This is true for
        /// Catalog2Dnx and Catalog2Registration jobs.  In those cases all but the latest commits for each package
        /// identity should be excluded BEFORE calling this method.  However, ...
        ///
        /// Note #2:  it is assumed that each <see cref="CatalogCommitItemBatch.CommitTimeStamp" /> is the minimum
        /// unprocessed commit timestamp for package ID (not identity), even <see cref="CatalogCommitItemBatch.Items" />
        /// if contains no item for that commit timestamp (because of Note #1 above).
        ///
        /// For the example above with these notes applied, this method would return a map with the following entries:
        ///
        ///     { T₀, new CatalogCommitBatchTasks(T₀,
        ///         new[]
        ///         {
        ///             new CatalogCommitBatchTask(T₀, P₀, [T₃]),     // P₀-T₀ and P₀-T₁ have been skipped
        ///             new CatalogCommitBatchTask(T₀, P₁, [T₃])] },  // P₁-T₀ has been skipped
        ///         }
        ///     },
        ///     { T₁, new CatalogCommitBatchTasks(T₁,
        ///         new[]
        ///         {
        ///             new CatalogCommitBatchTask(T₁, P₂, [T₁])
        ///         }
        ///     },
        ///     { T₂, new CatalogCommitBatchTasks(T₂,
        ///         new[]
        ///         {
        ///             new CatalogCommitBatchTask(T₂, P₃, [P₃])
        ///         }
        ///     }
        /// </remarks>
        /// <param name="batches">An enumerable of <see cref="CatalogCommitItemBatch" /> instances.</param>
        /// <returns>A map of commit timestamps to commit batch tasks.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="batches" /> is either <c>null</c> or empty.</exception>
        public static SortedDictionary<DateTime, CatalogCommitBatchTasks> CreateCommitBatchTasksMap(
            IEnumerable<CatalogCommitItemBatch> batches)
        {
            if (batches == null || !batches.Any())
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(batches));
            }

            var map = new SortedDictionary<DateTime, CatalogCommitBatchTasks>();

            foreach (var batch in batches)
            {
                var minCommitTimeStamp = batch.CommitTimeStamp;
                var batchTask = new CatalogCommitBatchTask(minCommitTimeStamp, batch.Key);

                CatalogCommitBatchTasks commitBatchTasks;

                if (!map.TryGetValue(minCommitTimeStamp, out commitBatchTasks))
                {
                    commitBatchTasks = new CatalogCommitBatchTasks(minCommitTimeStamp);

                    map[minCommitTimeStamp] = commitBatchTasks;
                }

                commitBatchTasks.BatchTasks.Add(batchTask);
            }

            return map;
        }

        public static void DequeueBatchesWhileMatches(
            Queue<CatalogCommitBatchTask> batches,
            Func<CatalogCommitBatchTask, bool> isMatch)
        {
            if (batches == null)
            {
                throw new ArgumentNullException(nameof(batches));
            }

            if (isMatch == null)
            {
                throw new ArgumentNullException(nameof(isMatch));
            }

            CatalogCommitBatchTask batch;

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

        public static void EnqueueBatchesIfNoFailures(
            CollectorHttpClient client,
            JToken context,
            SortedDictionary<DateTime, CatalogCommitBatchTasks> commitBatchTasksMap,
            Queue<CatalogCommitItemBatch> unprocessedBatches,
            Queue<CatalogCommitBatchTask> processingBatches,
            CatalogCommitItemBatch lastBatch,
            int maxConcurrentBatches,
            ProcessCommitItemBatchAsync processCommitItemBatchAsync,
            CancellationToken cancellationToken)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (commitBatchTasksMap == null)
            {
                throw new ArgumentNullException(nameof(commitBatchTasksMap));
            }

            if (unprocessedBatches == null)
            {
                throw new ArgumentNullException(nameof(unprocessedBatches));
            }

            if (processingBatches == null)
            {
                throw new ArgumentNullException(nameof(processingBatches));
            }

            if (lastBatch == null)
            {
                throw new ArgumentNullException(nameof(lastBatch));
            }

            if (maxConcurrentBatches < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentBatches),
                    maxConcurrentBatches,
                    string.Format(Strings.ArgumentOutOfRange, 1, int.MaxValue));
            }

            if (processCommitItemBatchAsync == null)
            {
                throw new ArgumentNullException(nameof(processCommitItemBatchAsync));
            }

            var hasAnyBatchFailed = processingBatches.Any(batch => batch.Task.IsFaulted || batch.Task.IsCanceled);

            if (hasAnyBatchFailed)
            {
                return;
            }

            var batchesToEnqueue = Math.Min(
                maxConcurrentBatches - processingBatches.Count(batch => !batch.Task.IsCompleted),
                unprocessedBatches.Count);

            for (var i = 0; i < batchesToEnqueue; ++i)
            {
                var batch = unprocessedBatches.Dequeue();

                var batchTask = commitBatchTasksMap[batch.CommitTimeStamp].BatchTasks
                    .Single(bt => bt.Key == batch.Key);

                batchTask.Task = processCommitItemBatchAsync(client, context, batch.Key, batch, lastBatch, cancellationToken);

                processingBatches.Enqueue(batchTask);
            }
        }

        internal static async Task<bool> FetchAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            ReadCursor back,
            FetchCatalogCommitsAsync fetchCatalogCommitsAsync,
            CreateCommitItemBatchesAsync createCommitItemBatchesAsync,
            ProcessCommitItemBatchAsync processCommitItemBatchAsync,
            int maxConcurrentBatches,
            string typeName,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            IEnumerable<CatalogCommit> rootItems = await fetchCatalogCommitsAsync(client, front, cancellationToken);

            var hasAnyBatchFailed = false;
            var hasAnyBatchBeenProcessed = false;

            foreach (CatalogCommit rootItem in rootItems)
            {
                JObject page = await client.GetJObjectAsync(rootItem.Uri, cancellationToken);
                var context = (JObject)page["@context"];
                CatalogCommitItemBatch[] batches = await CreateBatchesForAllAvailableItemsInPageAsync(front, back, page, context, createCommitItemBatchesAsync);

                if (!batches.Any())
                {
                    continue;
                }

                DateTime maxCommitTimeStamp = GetMaxCommitTimeStamp(batches);
                SortedDictionary<DateTime, CatalogCommitBatchTasks> commitBatchTasksMap = CreateCommitBatchTasksMap(batches);

                var unprocessedBatches = new Queue<CatalogCommitItemBatch>(batches);
                var processingBatches = new Queue<CatalogCommitBatchTask>();

                CatalogCommitItemBatch lastBatch = unprocessedBatches.LastOrDefault();
                var exceptions = new List<Exception>();

                EnqueueBatchesIfNoFailures(
                    client,
                    context,
                    commitBatchTasksMap,
                    unprocessedBatches,
                    processingBatches,
                    lastBatch,
                    maxConcurrentBatches,
                    processCommitItemBatchAsync,
                    cancellationToken);

                while (processingBatches.Any())
                {
                    var activeTasks = processingBatches.Where(batch => !batch.Task.IsCompleted)
                        .Select(batch => batch.Task)
                        .DefaultIfEmpty(Task.CompletedTask);

                    await Task.WhenAny(activeTasks);

                    DateTime? newCommitTimeStamp = null;

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
                            // If there were multiple successfully processed commits, keep track of the most recent one
                            // and update the front cursor later after determining the latest successful commit timestamp
                            // to use.  Updating the cursor here for each commit can be very slow.
                            newCommitTimeStamp = commitBatchTasks.CommitTimeStamp;

                            DequeueBatchesWhileMatches(processingBatches, batch => batch.MinCommitTimeStamp == newCommitTimeStamp.Value);

                            commitBatchTasksMap.Remove(newCommitTimeStamp.Value);

                            if (!commitBatchTasksMap.Any())
                            {
                                if (maxCommitTimeStamp > newCommitTimeStamp)
                                {
                                    // Although all commits for the current page have been successfully processed, the
                                    // current CatalogCommitBatchTasks.CommitTimeStamp value is not the maximum commit
                                    // timestamp processed.
                                    newCommitTimeStamp = maxCommitTimeStamp;
                                }
                            }
                        }
                        else // Canceled or Failed
                        {
                            hasAnyBatchFailed = true;

                            exceptions.AddRange(
                                commitBatchTasks.BatchTasks
                                    .Select(batch => batch.Task)
                                    .Where(task => (task.IsFaulted || task.IsCanceled) && task.Exception != null)
                                    .Select(task => ExceptionUtilities.Unwrap(task.Exception)));
                        }
                    }

                    if (newCommitTimeStamp.HasValue)
                    {
                        front.Value = newCommitTimeStamp.Value;

                        await front.SaveAsync(cancellationToken);

                        Trace.TraceInformation($"{typeName}.{nameof(FetchAsync)} {nameof(front)}.{nameof(front.Value)} saved since timestamp changed from previous: {{0}}", front);
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
                        maxConcurrentBatches,
                        processCommitItemBatchAsync,
                        cancellationToken);
                }

                if (hasAnyBatchFailed)
                {
                    foreach (var exception in exceptions)
                    {
                        logger.LogError(_eventId, exception, Strings.BatchProcessingFailure);
                    }

                    var innerException = exceptions.Count == 1 ? exceptions.Single() : new AggregateException(exceptions);

                    throw new BatchProcessingException(innerException);
                }
            }

            return hasAnyBatchBeenProcessed;
        }

        public static string GetPackageIdKey(CatalogCommitItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return item.PackageIdentity.Id.ToLowerInvariant();
        }

        private static async Task<CatalogCommitItemBatch[]> CreateBatchesForAllAvailableItemsInPageAsync(
            ReadWriteCursor front,
            ReadCursor back,
            JObject page,
            JObject context,
            CreateCommitItemBatchesAsync createCommitItemBatchesAsync)
        {
            IEnumerable<CatalogCommitItem> commitItems = page["items"]
                .Select(item => CatalogCommitItem.Create(context, (JObject)item))
                .Where(item => item.CommitTimeStamp > front.Value && item.CommitTimeStamp <= back.Value);

            IEnumerable<CatalogCommitItemBatch> batches = await createCommitItemBatchesAsync(commitItems);

            return batches
                .OrderBy(batch => batch.CommitTimeStamp)
                .ToArray();
        }

        private static DateTime GetMaxCommitTimeStamp(CatalogCommitItemBatch[] batches)
        {
            return batches.SelectMany(batch => batch.Items)
                .Select(item => item.CommitTimeStamp)
                .Max();
        }
    }
}
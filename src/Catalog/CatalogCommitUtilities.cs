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
        /// <returns>An enumerable of <see cref="CatalogCommitItemBatch" /> with no ordering guarantee.</returns>
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

            var catalogItemsGroups = catalogItems
                .GroupBy(catalogItem => getCatalogCommitItemKey(catalogItem));

            var batches = new List<CatalogCommitItemBatch>();

            foreach (var catalogItemsGroup in catalogItemsGroups)
            {
                var catalogItemsWithOnlyLatestCommitForEachPackageIdentity = catalogItemsGroup
                    .GroupBy(commitItem => new
                    {
                        PackageId = commitItem.PackageIdentity.Id.ToLowerInvariant(),
                        PackageVersion = commitItem.PackageIdentity.Version.ToNormalizedString().ToLowerInvariant()
                    })
                    .Select(group => group.OrderBy(item => item.CommitTimeStamp).Last())
                    .ToArray();
                var minCommitTimeStamp = catalogItemsWithOnlyLatestCommitForEachPackageIdentity
                    .Select(catalogItem => catalogItem.CommitTimeStamp)
                    .Min();

                batches.Add(
                    new CatalogCommitItemBatch(
                        catalogItemsWithOnlyLatestCommitForEachPackageIdentity,
                        catalogItemsGroup.Key));
            }

            // Assert only after skipping older commits for each package identity to reduce the likelihood
            // of unnecessary failures.
            AssertNotMoreThanOneCommitIdPerCommitTimeStamp(batches, nameof(catalogItems));

            return batches;
        }

        public static void StartProcessingBatchesIfNoFailures(
            CollectorHttpClient client,
            JToken context,
            List<CatalogCommitItemBatch> unprocessedBatches,
            List<CatalogCommitItemBatchTask> processingBatches,
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

            if (unprocessedBatches == null)
            {
                throw new ArgumentNullException(nameof(unprocessedBatches));
            }

            if (processingBatches == null)
            {
                throw new ArgumentNullException(nameof(processingBatches));
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
                var batch = unprocessedBatches[0];

                unprocessedBatches.RemoveAt(0);

                var task = processCommitItemBatchAsync(
                    client,
                    context,
                    batch.Key,
                    batch,
                    lastBatch: null,
                    cancellationToken: cancellationToken);
                var batchTask = new CatalogCommitItemBatchTask(batch, task);

                processingBatches.Add(batchTask);
            }
        }

        internal static async Task<bool> ProcessCatalogCommitsAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            ReadCursor back,
            FetchCatalogCommitsAsync fetchCatalogCommitsAsync,
            CreateCommitItemBatchesAsync createCommitItemBatchesAsync,
            ProcessCommitItemBatchAsync processCommitItemBatchAsync,
            int maxConcurrentBatches,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var rootItems = await fetchCatalogCommitsAsync(client, front, back, cancellationToken);

            var hasAnyBatchFailed = false;
            var hasAnyBatchBeenProcessed = false;

            foreach (CatalogCommit rootItem in rootItems)
            {
                JObject page = await client.GetJObjectAsync(rootItem.Uri, cancellationToken);
                var context = (JObject)page["@context"];
                CatalogCommitItemBatch[] batches = await CreateBatchesForAllAvailableItemsInPageAsync(
                    front,
                    back,
                    page,
                    context,
                    createCommitItemBatchesAsync);

                if (!batches.Any())
                {
                    continue;
                }

                hasAnyBatchBeenProcessed = true;

                DateTime maxCommitTimeStamp = GetMaxCommitTimeStamp(batches);
                var unprocessedBatches = batches.ToList();
                var processingBatches = new List<CatalogCommitItemBatchTask>();
                var exceptions = new List<Exception>();

                StartProcessingBatchesIfNoFailures(
                    client,
                    context,
                    unprocessedBatches,
                    processingBatches,
                    maxConcurrentBatches,
                    processCommitItemBatchAsync,
                    cancellationToken);

                while (processingBatches.Any())
                {
                    var activeTasks = processingBatches.Where(batch => !batch.Task.IsCompleted)
                        .Select(batch => batch.Task)
                        .DefaultIfEmpty(Task.CompletedTask);

                    await Task.WhenAny(activeTasks);

                    for (var i = 0; i < processingBatches.Count; ++i)
                    {
                        var batch = processingBatches[i];

                        if (batch.Task.IsFaulted || batch.Task.IsCanceled)
                        {
                            hasAnyBatchFailed = true;

                            if (batch.Task.Exception != null)
                            {
                                var exception = ExceptionUtilities.Unwrap(batch.Task.Exception);

                                exceptions.Add(exception);
                            }
                        }

                        if (batch.Task.IsCompleted)
                        {
                            processingBatches.RemoveAt(i);
                            --i;
                        }
                    }

                    if (!hasAnyBatchFailed)
                    {
                        StartProcessingBatchesIfNoFailures(
                            client,
                            context,
                            unprocessedBatches,
                            processingBatches,
                            maxConcurrentBatches,
                            processCommitItemBatchAsync,
                            cancellationToken);
                    }
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

                front.Value = maxCommitTimeStamp;

                await front.SaveAsync(cancellationToken);

                Trace.TraceInformation($"{nameof(CatalogCommitUtilities)}.{nameof(ProcessCatalogCommitsAsync)} " +
                    $"{nameof(front)}.{nameof(front.Value)} saved since timestamp changed from previous: {{0}}", front);
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

        private static void AssertNotMoreThanOneCommitIdPerCommitTimeStamp(
            IEnumerable<CatalogCommitItemBatch> batches,
            string parameterName)
        {
            var commitsWithSameTimeStampButDifferentCommitIds = batches
                .SelectMany(batch => batch.Items)
                .GroupBy(commitItem => commitItem.CommitTimeStamp)
                .Where(group => group.Select(item => item.CommitId).Distinct().Count() > 1);

            if (commitsWithSameTimeStampButDifferentCommitIds.Any())
            {
                var commits = commitsWithSameTimeStampButDifferentCommitIds.SelectMany(group => group)
                    .Select(commit => $"{{ CommitId = {commit.CommitId}, CommitTimeStamp = {commit.CommitTimeStamp.ToString("O")} }}");

                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.MultipleCommitIdsForSameCommitTimeStamp,
                        string.Join(", ", commits)),
                    parameterName);
            }
        }
    }
}
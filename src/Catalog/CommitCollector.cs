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

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class CommitCollector : CollectorBase
    {
        public CommitCollector(
            Uri index,
            ITelemetryService telemetryService,
            Func<HttpMessageHandler> handlerFunc = null,
            TimeSpan? httpClientTimeout = null,
            IHttpRetryStrategy httpRetryStrategy = null)
            : base(index, telemetryService, handlerFunc, httpClientTimeout, httpRetryStrategy)
        {
        }

        protected override async Task<bool> FetchAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            ReadCursor back,
            CancellationToken cancellationToken)
        {
            var commits = await FetchCatalogCommitsAsync(client, front, back, cancellationToken);

            bool acceptNextBatch = false;

            foreach (CatalogCommit commit in commits)
            {
                JObject page = await client.GetJObjectAsync(commit.Uri, cancellationToken);

                JToken context = null;
                page.TryGetValue("@context", out context);

                var batches = await CreateBatchesAsync(page["items"]
                    .Select(item => CatalogCommitItem.Create((JObject)context, (JObject)item))
                    .Where(item => item.CommitTimeStamp > front.Value && item.CommitTimeStamp <= back.Value));

                var orderedBatches = batches
                    .OrderBy(batch => batch.CommitTimeStamp)
                    .ToList();

                var lastBatch = orderedBatches.LastOrDefault();
                DateTime? previousCommitTimeStamp = null;

                foreach (var batch in orderedBatches)
                {
                    // If the commit timestamp has changed from the previous batch, commit. This is important because if
                    // two batches have the same commit timestamp but processing the second fails, we should not
                    // progress the cursor forward.
                    if (previousCommitTimeStamp.HasValue && previousCommitTimeStamp != batch.CommitTimeStamp)
                    {
                        front.Value = previousCommitTimeStamp.Value;
                        await front.SaveAsync(cancellationToken);
                        Trace.TraceInformation("CommitCatalog.Fetch front.Value saved since timestamp changed from previous: {0}", front);
                    }

                    using (_telemetryService.TrackDuration(TelemetryConstants.ProcessBatchSeconds, new Dictionary<string, string>()
                    {
                        { TelemetryConstants.BatchItemCount, batch.Items.Count.ToString() }
                    }))
                    {
                        acceptNextBatch = await OnProcessBatchAsync(
                            client,
                            batch.Items,
                            context,
                            batch.CommitTimeStamp,
                            batch.CommitTimeStamp == lastBatch.CommitTimeStamp,
                            cancellationToken);
                    }

                    // If this is the last batch, commit the cursor.
                    if (ReferenceEquals(batch, lastBatch))
                    {
                        front.Value = batch.CommitTimeStamp;
                        await front.SaveAsync(cancellationToken);
                        Trace.TraceInformation("CommitCatalog.Fetch front.Value saved due to last batch: {0}", front);
                    }

                    previousCommitTimeStamp = batch.CommitTimeStamp;

                    Trace.TraceInformation("CommitCatalog.Fetch front.Value is: {0}", front);

                    if (!acceptNextBatch)
                    {
                        break;
                    }
                }

                if (!acceptNextBatch)
                {
                    break;
                }
            }

            return acceptNextBatch;
        }

        protected async Task<IEnumerable<CatalogCommit>> FetchCatalogCommitsAsync(
            CollectorHttpClient client,
            ReadCursor front,
            ReadCursor back,
            CancellationToken cancellationToken)
        {
            JObject root;

            using (_telemetryService.TrackDuration(
                TelemetryConstants.CatalogIndexReadDurationSeconds,
                new Dictionary<string, string>() { { TelemetryConstants.Uri, Index.AbsoluteUri } }))
            {
                root = await client.GetJObjectAsync(Index, cancellationToken);
            }

            var commits = root["items"].Select(item => CatalogCommit.Create((JObject)item));
            return GetCommitsInRange(commits, front.Value, back.Value);
        }


        public static IEnumerable<CatalogCommit> GetCommitsInRange(
            IEnumerable<CatalogCommit> commits,
            DateTimeOffset minCommitTimestamp,
            DateTimeOffset maxCommitTimestamp)
        {
            // Only consider pages that have a (latest) commit timestamp greater than the minimum bound. If a page has
            // a commit timestamp greater than the minimum bound, then there is at least one item with a commit
            // timestamp greater than the minimum bound. Sort the pages by commit timestamp so that they are
            // in chronological order.
            var upperRange = commits
                .Where(x => x.CommitTimeStamp > minCommitTimestamp)
                .OrderBy(x => x.CommitTimeStamp);

            // Take pages from the sorted list until the (latest) commit timestamp goes past the maximum commit
            // timestamp. This essentially LINQ's TakeWhile plus one more element. Because the maximum bound is
            // inclusive, we need to yield any page that has a (latest) commit timestamp that is less than or
            // equal to the maximum bound.
            //
            // Consider the following pages (bounded by square brackets, labeled P-0 ... P-N) containing commits
            // (C-0 ... C-N). The front cursor (exclusive minimum bound) is marked by the letter "F" and the back
            // cursor (inclusive upper bound) is marked by the letter "B".
            //
            //        ---- P-0 ----     ---- P-1 ----     ---- P-2 ----     ----- P-3 -----
            //      [ C-0, C-1, C-2 ] [ C-3, C-4, C-5 ] [ C-6, C-7, C-8 ] [ C-9, C-10, C-11 ]
            //              |    |       |                 |    |    |
            // Scenario #1: F    |       |                 |    B    |
            //                   |       |                 |         |
            //   P-0, P-1, and P-2 should be downloaded and C-2 to C-7 should be processed. Note that P-3 should not
            //   even be considered because P-2 is the first page with a maximum commit timestamp greater than "B".
            //                   |       |                 |         |
            // Scenario #2:      |       F                 |         B
            //                   |                         |
            //   P-1 and P-2 should be downloaded and C-4 to C-8 should be processed. The concept of a timestamp-based
            //   cursor requires that commit timestamps strictly increase. Additionally, our catalog implementation
            //   never allows a commit to be split across multiple pages. In other words, if C-8 is at the end of P-2
            //   the consumer of the catalog can assume that P-3 only has commits later than C-8 and therefore P-3 need
            //   not be considered.                        |
            //                   |                         |
            // Scenario #3:      F                         B
            //
            //   P-1 and P-2 should be downloaded and C-3 to C-6 should be processed. Because "F" (an exclusive bound)
            //   is pointing to the latest commit timestamp in P-0, that page can be completely ignored.
            foreach (var page in upperRange)
            {
                yield return page;
                
                if (page.CommitTimeStamp >= maxCommitTimestamp)
                {
                    break;
                }
            }
        }

        protected virtual Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(IEnumerable<CatalogCommitItem> catalogItems)
        {
            var batches = catalogItems
                .GroupBy(item => item.CommitTimeStamp)
                .OrderBy(group => group.Key)
                .Select(group => new CatalogCommitItemBatch(group));

            return Task.FromResult(batches);
        }

        protected abstract Task<bool> OnProcessBatchAsync(
            CollectorHttpClient client,
            IEnumerable<CatalogCommitItem> items,
            JToken context,
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken);
    }
}
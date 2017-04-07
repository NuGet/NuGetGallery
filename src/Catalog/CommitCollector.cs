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
        public CommitCollector(Uri index, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
        }

        protected override async Task<bool> Fetch(CollectorHttpClient client, ReadWriteCursor front, ReadCursor back, CancellationToken cancellationToken)
        {
            JObject root = await client.GetJObjectAsync(Index, cancellationToken);

            IEnumerable<CatalogItem> rootItems = root["items"]
                .Select(item => new CatalogItem(item))
                .Where(item => item.CommitTimeStamp > front.Value)
                .OrderBy(item => item.CommitTimeStamp);

            bool acceptNextBatch = false;

            foreach (CatalogItem rootItem in rootItems)
            {
                JObject page = await client.GetJObjectAsync(rootItem.Uri, cancellationToken);

                JToken context = null;
                page.TryGetValue("@context", out context);
                
                var batches = await CreateBatches(page["items"]
                    .Select(item => new CatalogItem(item))
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
                        await front.Save(cancellationToken);
                        Trace.TraceInformation("CommitCatalog.Fetch front.Value saved since timestamp changed from previous: {0}", front);
                    }

                    acceptNextBatch = await OnProcessBatch(
                        client, 
                        batch.Items.Select(item => item.Value), 
                        context, 
                        batch.CommitTimeStamp, 
                        batch.CommitTimeStamp == lastBatch.CommitTimeStamp, 
                        cancellationToken);

                    // If this is the last batch, commit the cursor.
                    if (ReferenceEquals(batch, lastBatch))
                    {
                        front.Value = batch.CommitTimeStamp;
                        await front.Save(cancellationToken);
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

        protected virtual Task<IEnumerable<CatalogItemBatch>> CreateBatches(IEnumerable<CatalogItem> catalogItems)
        {
            var batches = catalogItems
                .GroupBy(item => item.CommitTimeStamp)
                .OrderBy(group => group.Key)
                .Select(group => new CatalogItemBatch(group.Key, group));

            return Task.FromResult(batches);
        }

        protected abstract Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp, bool isLastBatch, CancellationToken cancellationToken);

        protected class CatalogItemBatch : IComparable
        {
            public CatalogItemBatch(DateTime commitTimeStamp, IEnumerable<CatalogItem> items)
            {
                CommitTimeStamp = commitTimeStamp;
                Items = items.ToList();
                Items.Sort();
            }

            public DateTime CommitTimeStamp { get; private set; }
            public List<CatalogItem> Items { get; private set; }

            public int CompareTo(object obj)
            {
                return CommitTimeStamp.CompareTo(((CatalogItem)obj).CommitTimeStamp);
            }
        }

        protected class CatalogItem : IComparable
        {
            public CatalogItem(JToken jtoken)
            {
                CommitTimeStamp = jtoken["commitTimeStamp"].ToObject<DateTime>();
                Uri = jtoken["@id"].ToObject<Uri>();
                Value = jtoken;
            }

            public DateTime CommitTimeStamp { get; private set; }
            public Uri Uri { get; private set; }
            public JToken Value { get; private set; }

            public int CompareTo(object obj)
            {
                return CommitTimeStamp.CompareTo(((CatalogItem)obj).CommitTimeStamp);
            }
        }
    }
}
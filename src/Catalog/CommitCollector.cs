// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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

                foreach (var batch in orderedBatches)
                {
                    acceptNextBatch = await OnProcessBatch(
                        client, 
                        batch.Items.Select(item => item.Value), 
                        context, 
                        batch.CommitTimeStamp, 
                        batch.CommitTimeStamp == lastBatch.CommitTimeStamp, 
                        cancellationToken);

                    front.Value = batch.CommitTimeStamp;
                    await front.Save(cancellationToken);

                    Trace.TraceInformation("CommitCatalog.Fetch front.Save has value: {0}", front);

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
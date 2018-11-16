// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class SortingCollector<T> : CommitCollector where T : IEquatable<T>
    {
        public SortingCollector(
            Uri index,
            ITelemetryService telemetryService,
            Func<HttpMessageHandler> handlerFunc = null,
            IHttpRetryStrategy httpRetryStrategy = null)
            : base(index, telemetryService, handlerFunc, httpRetryStrategy: httpRetryStrategy)
        {
        }

        protected override async Task<bool> OnProcessBatchAsync(
            CollectorHttpClient client,
            IEnumerable<CatalogCommitItem> items,
            JToken context,
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken)
        {
            var sortedItems = new Dictionary<T, IList<CatalogCommitItem>>();

            foreach (CatalogCommitItem item in items)
            {
                T key = GetKey(item);

                IList<CatalogCommitItem> itemList;
                if (!sortedItems.TryGetValue(key, out itemList))
                {
                    itemList = new List<CatalogCommitItem>();
                    sortedItems.Add(key, itemList);
                }

                itemList.Add(item);
            }

            IList<Task> tasks = new List<Task>();

            foreach (KeyValuePair<T, IList<CatalogCommitItem>> sortedBatch in sortedItems)
            {
                Task task = ProcessSortedBatchAsync(client, sortedBatch, context, cancellationToken);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks.ToArray());

            return true;
        }

        protected abstract T GetKey(CatalogCommitItem item);

        protected abstract Task ProcessSortedBatchAsync(
            CollectorHttpClient client,
            KeyValuePair<T, IList<CatalogCommitItem>> sortedBatch,
            JToken context,
            CancellationToken cancellationToken);
    }
}
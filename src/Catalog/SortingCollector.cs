// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class SortingCollector<T> : CommitCollector where T : IEquatable<T>
    {
        public SortingCollector(Uri index, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            Concurrent = true;
        }

        public bool Concurrent { get; set; }

        protected override async Task<bool> OnProcessBatch(
            CollectorHttpClient client, 
            IEnumerable<JToken> items,
            JToken context, 
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken)
        {
            IDictionary<T, IList<JObject>> sortedItems = new Dictionary<T, IList<JObject>>();

            foreach (JObject item in items)
            {
                T key = GetKey(item);

                IList<JObject> itemList;
                if (!sortedItems.TryGetValue(key, out itemList))
                {
                    itemList = new List<JObject>();
                    sortedItems.Add(key, itemList);
                }

                itemList.Add(item);
            }

            IList<Task> tasks = new List<Task>();

            foreach (KeyValuePair<T, IList<JObject>> sortedBatch in sortedItems)
            {
                Task task = ProcessSortedBatch(client, sortedBatch, context, cancellationToken);

                tasks.Add(task);

                if (!Concurrent)
                {
                    task.Wait();
                }
            }

            await Task.WhenAll(tasks.ToArray());

            return true;
        }

        protected abstract T GetKey(JObject item);

        protected abstract Task ProcessSortedBatch(
            CollectorHttpClient client, 
            KeyValuePair<T, IList<JObject>> sortedBatch, 
            JToken context, 
            CancellationToken cancellationToken);
    }
}

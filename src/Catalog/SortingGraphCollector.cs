// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class SortingGraphCollector : SortingCollector
    {
        Uri[] _types;

        public SortingGraphCollector(Uri index, Uri[] types, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _types = types;
        }

        protected override async Task ProcessSortedBatch(
            CollectorHttpClient client,
            KeyValuePair<string, IList<JObject>> sortedBatch,
            JToken context, 
            CancellationToken cancellationToken)
        {
            var graphs = new Dictionary<string, IGraph>();
            var graphTasks = new Dictionary<string, Task<IGraph>>();
            
            foreach (var item in sortedBatch.Value)
            {
                if (Utils.IsType((JObject)context, item, _types))
                {
                    var itemUri = item["@id"].ToString();

                    // Download the graph to a read-only container. This allows operations on each graph to be safely
                    // parallelized.
                    var task = client.GetGraphAsync(new Uri(itemUri), readOnly: true, token: cancellationToken);

                    graphTasks.Add(itemUri, task);

                    if (!Concurrent)
                    {
                        task.Wait(cancellationToken);
                    }
                }
            }

            await Task.WhenAll(graphTasks.Values.ToArray());

            foreach (var task in graphTasks)
            {
                graphs.Add(task.Key, task.Value.Result);
            }

            if (graphs.Count > 0)
            {
                await ProcessGraphs(new KeyValuePair<string, IDictionary<string, IGraph>>(sortedBatch.Key, graphs), cancellationToken);
            }
        }

        protected abstract Task ProcessGraphs(KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs, CancellationToken cancellationToken);
    }
}

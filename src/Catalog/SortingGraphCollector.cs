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
    public abstract class SortingGraphCollector : SortingIdCollector
    {
        private readonly Uri[] _types;

        public SortingGraphCollector(
            Uri index,
            Uri[] types,
            ITelemetryService telemetryService,
            Func<HttpMessageHandler> handlerFunc = null)
            : base(index, telemetryService, handlerFunc)
        {
            _types = types;
        }

        protected override async Task ProcessSortedBatchAsync(
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

                    // Load package details from catalog.
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
                var sortedGraphs = new KeyValuePair<string, IReadOnlyDictionary<string, IGraph>>(sortedBatch.Key, graphs);

                await ProcessGraphsAsync(sortedGraphs, cancellationToken);
            }
        }

        protected abstract Task ProcessGraphsAsync(
            KeyValuePair<string, IReadOnlyDictionary<string, IGraph>> sortedGraphs,
            CancellationToken cancellationToken);
    }
}
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
            Func<HttpMessageHandler> handlerFunc = null,
            IHttpRetryStrategy httpRetryStrategy = null)
            : base(index, telemetryService, handlerFunc, httpRetryStrategy)
        {
            _types = types;
        }

        protected override async Task ProcessSortedBatchAsync(
            CollectorHttpClient client,
            KeyValuePair<string, IList<CatalogCommitItem>> sortedBatch,
            JToken context,
            CancellationToken cancellationToken)
        {
            var graphs = new Dictionary<string, IGraph>();
            var graphTasks = new Dictionary<string, Task<IGraph>>();

            foreach (var item in sortedBatch.Value)
            {
                var isMatch = false;

                foreach (Uri type in _types)
                {
                    if (item.TypeUris.Any(typeUri => typeUri.AbsoluteUri == type.AbsoluteUri))
                    {
                        isMatch = true;
                        break;
                    }
                }

                if (isMatch)
                {
                    // Load package details from catalog.
                    // Download the graph to a read-only container. This allows operations on each graph to be safely
                    // parallelized.
                    var task = client.GetGraphAsync(item.Uri, readOnly: true, token: cancellationToken);

                    graphTasks.Add(item.Uri.AbsoluteUri, task);
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
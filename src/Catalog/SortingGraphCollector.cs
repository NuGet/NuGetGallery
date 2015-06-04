// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

        protected override async Task ProcessSortedBatch(CollectorHttpClient client, KeyValuePair<string, IList<JObject>> sortedBatch, JToken context, CancellationToken cancellationToken)
        {
            IDictionary<string, IGraph> graphs = new Dictionary<string, IGraph>();

            foreach (JObject item in sortedBatch.Value)
            {
                if (Utils.IsType((JObject)context, item, _types))
                {
                    string itemUri = item["@id"].ToString();
                    IGraph graph = await client.GetGraphAsync(new Uri(itemUri), cancellationToken);
                    graphs.Add(itemUri, graph);
                }
            }

            if (graphs.Count > 0)
            {
                await ProcessGraphs(new KeyValuePair<string, IDictionary<string, IGraph>>(sortedBatch.Key, graphs), cancellationToken);
            }
        }

        protected abstract Task ProcessGraphs(KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs, CancellationToken cancellationToken);
    }
}

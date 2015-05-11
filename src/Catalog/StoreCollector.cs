// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class StoreCollector : CommitCollector
    {
        Uri[] _types;

        public StoreCollector(Uri index, Uri[] types, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            Options.InternUris = false;
            _types = types;
        }

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp)
        {
            List<Task<IGraph>> tasks = new List<Task<IGraph>>();

            foreach (JObject item in items)
            {
                if (Utils.IsType((JObject)context, item, _types))
                {
                    Uri itemUri = item["@id"].ToObject<Uri>();
                    tasks.Add(client.GetGraphAsync(itemUri));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());

                TripleStore store = new TripleStore();

                foreach (Task<IGraph> task in tasks)
                {
                    store.Add(task.Result, true);
                }

                await ProcessStore(store);
            }

            return true;
        }

        protected abstract Task ProcessStore(TripleStore store);
    }
}

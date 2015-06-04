// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Test
{
    public class FindCollector : BatchCollector
    {
        public FindCollector(Uri index, Func<HttpMessageHandler> handlerFunc = null, int batchSize = 200)
            : base(index, handlerFunc, batchSize)
        {
            Result = new Dictionary<string, IList<string>>();
        }

        public IDictionary<string, IList<string>> Result
        {
            get; private set;
        }

        protected override Task<bool> OnProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context, CancellationToken cancellationToken)
        {
            foreach (JObject item in items)
            {
                string id = item["nuget:id"].ToString();
                string version = item["nuget:version"].ToString();

                IList<string> versions;
                if (!Result.TryGetValue(id, out versions))
                {
                    versions = new List<string>();
                    Result.Add(id, versions);
                }

                versions.Add(version);
            }

            return Task.FromResult(true);
        }
    }
}

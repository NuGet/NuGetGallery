// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class TestCollector : CommitCollector
    {
        string _name;

        public TestCollector(string name, Uri index, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _name = name;
        }

        protected override Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp, CancellationToken cancellationToken)
        {
            foreach (JObject item in items)
            {
                Console.WriteLine("{0} {1}", _name, item["@id"].ToString());
            }

            return Task.FromResult(true);
        }
    }
}

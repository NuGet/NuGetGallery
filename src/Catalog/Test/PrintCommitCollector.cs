// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Test
{
    public class PrintCommitCollector : CommitCollector
    {
        public PrintCommitCollector(Uri index, Func<HttpMessageHandler> handlerFunc = null) 
            : base(index, handlerFunc)
        {
        }

        protected override Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp, bool isLastBatch, CancellationToken cancellationToken)
        {
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("COMMIT: {0}", commitTimeStamp.ToString("O"));

            foreach (JToken item in items)
            {
                Console.WriteLine("{0} {1}", item["@id"], item["@type"]);
                Console.WriteLine("{0} {1} {2}", item["nuget:id"], item["nuget:version"], item["commitId"]);
            }

            Console.WriteLine("------------------------------------------------");

            if (isLastBatch)
            {
                Console.WriteLine("(last batch)------------------------------------");
            }

            return Task.FromResult(true);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class ReindexTests
    {
        public static async Task Test0Async()
        {
            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new FileSystemEmulatorHandler
                {
                    BaseAddress = new Uri("http://localhost:8000"),
                    RootFolder = @"c:\data\site",
                    InnerHandler = new HttpClientHandler()
                };
            };

            StorageFactory storageFactory = new FileStorageFactory(new Uri("http://localhost:8000/nuspec/"), @"c:\data\site\nuspec");

            CommitCollector collector = new ReindexCatalogCollector(new Uri("http://localhost:8000/full/index.json"), storageFactory, handlerFunc);

            await collector.Run(CancellationToken.None);

            Console.WriteLine("http requests: {0}", collector.RequestCount);
        }

        public static void Test0()
        {
            Test0Async().Wait();
        }
    }
}

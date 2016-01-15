// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ng
{
    public class Catalog2Dnx
    {
        private readonly ILogger _logger;

        public Catalog2Dnx(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Catalog2Registration>();
        }

        public async Task Loop(string source, StorageFactory storageFactory, string contentBaseAddress, bool verbose, int interval, CancellationToken cancellationToken)
        {
            CommitCollector collector = new DnxCatalogCollector(new Uri(source), storageFactory, CommandHelpers.GetHttpMessageHandlerFactory(verbose))
            {
                ContentBaseAddress = contentBaseAddress == null ? null : new Uri(contentBaseAddress)
            };

            Storage storage = storageFactory.Create();
            ReadWriteCursor front = new DurableCursor(storage.ResolveUri("cursor.json"), storage, MemoryCursor.Min.Value);
            ReadCursor back = MemoryCursor.Max;

            while (true)
            {
                bool run = false;
                do
                {
                    run = await collector.Run(front, back, cancellationToken);
                }
                while (run);

                await Task.Delay(interval * 1000, cancellationToken);
                //Thread.Sleep(interval * 1000);
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: ng catalog2dnx -source <catalog> -contentBaseAddress <content-address> -storageBaseAddress <storage-base-address> -storageType file|azure [-storagePath <path>]|[-storageAccountName <azure-acc> -storageKeyValue <azure-key> -storageContainer <azure-container> -storagePath <path>] [-verbose true|false] [-interval <seconds>]");
        }

        public void Run(string[] args, CancellationToken cancellationToken)
        {
            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null || arguments.Count == 0)
            {
                PrintUsage();
                return;
            }

            string source = CommandHelpers.GetSource(arguments);
            if (source == null)
            {
                PrintUsage();
                return;
            }

            bool verbose = CommandHelpers.GetVerbose(arguments);

            int interval = CommandHelpers.GetInterval(arguments);

            string contentBaseAddress = CommandHelpers.GetContentBaseAddress(arguments);

            StorageFactory storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            if (storageFactory == null)
            {
                PrintUsage();
                return;
            }

            if (verbose)
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
                Trace.AutoFlush = true;
            }

            Trace.TraceInformation("CONFIG source: \"{0}\" storage: \"{1}\" interval: {2} seconds", source, storageFactory, interval);

            Loop(source, storageFactory, contentBaseAddress, verbose, interval, cancellationToken).Wait();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ng
{
    public static class Catalog2Lucene
    {
        static async Task Loop(string source, string registration, Lucene.Net.Store.Directory directory, string catalogBaseAddress, string storageBaseAddress, bool verbose, int interval, CancellationToken cancellationToken)
        {
            Func<HttpMessageHandler> handlerFunc = CommandHelpers.GetHttpMessageHandlerFactory(verbose, catalogBaseAddress, storageBaseAddress);

            CommitCollector collector = new SearchIndexFromCatalogCollector(new Uri(source), directory, catalogBaseAddress, handlerFunc);

            ReadWriteCursor front = new LuceneCursor(directory, MemoryCursor.Min.Value);
            
            ReadCursor back = (registration == null) ? (ReadCursor)MemoryCursor.Max : new HttpReadCursor(new Uri(registration), handlerFunc);

            while (true)
            {
                bool run = false;
                do
                {
                    run = await collector.Run(front, back, cancellationToken);
                }
                while (run);

                Thread.Sleep(interval * 1000);
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: ng catalog2lucene -source <catalog> [-registration <registration-root>] -luceneDirectoryType file|azure [-lucenePath <file-path>] | [-luceneStorageAccountName <azure-acc> -luceneStorageKeyValue <azure-key> -luceneStorageContainer <azure-container>] [-verbose true|false] [-interval <seconds>]");
        }

        public static void Run(string[] args, CancellationToken cancellationToken)
        {
            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null || arguments.Count == 0)
            {
                PrintUsage();
                return;
            }

            Lucene.Net.Store.Directory directory = CommandHelpers.GetLuceneDirectory(arguments);
            if (directory == null)
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

            if (verbose)
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
                //Trace.AutoFlush = true;
            }

            int interval = CommandHelpers.GetInterval(arguments);

            string registration = CommandHelpers.GetRegistration(arguments);

            if (registration == null)
            {
                Console.WriteLine("Lucene index will be created up to the end of the catalog (alternatively if you provide a registration it will not pass that)");
            }

            string catalogBaseAddress = CommandHelpers.GetCatalogBaseAddress(arguments);

            if (catalogBaseAddress == null)
            {
                Console.WriteLine("No catalogBaseAddress was specified so the Lucene index will NOT contain the storage paths");
            }

            string storageBaseAddress = CommandHelpers.GetStorageBaseAddress(arguments);

            Trace.TraceInformation("CONFIG source: \"{0}\" registration: \"{1}\" catalogBaseAddress: \"{2}\" storageBaseAddress: \"{3}\" interval: {4} seconds", source, registration ?? "(null)", catalogBaseAddress ?? "(null)", storageBaseAddress ?? "(null)", interval);

            Loop(source, registration, directory, catalogBaseAddress, storageBaseAddress, verbose, interval, cancellationToken).Wait();
        }
    }
}

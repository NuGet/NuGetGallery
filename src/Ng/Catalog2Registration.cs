// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Ng
{
    public class Catalog2Registration
    {
        private readonly ILogger _logger;

        public Catalog2Registration(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Catalog2Registration>();
        }

        public async Task Loop(string source, StorageFactory storageFactory, string contentBaseAddress, bool unlistShouldDelete, bool verbose, int interval, CancellationToken cancellationToken)
        {
            CommitCollector collector = new RegistrationCollector(new Uri(source), storageFactory, CommandHelpers.GetHttpMessageHandlerFactory(verbose))
            {
                ContentBaseAddress = contentBaseAddress == null ? null : new Uri(contentBaseAddress),
                UnlistShouldDelete = unlistShouldDelete
            };

            Storage storage = storageFactory.Create();
            ReadWriteCursor front = new DurableCursor(storage.ResolveUri("cursor.json"), storage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            while (true)
            {
                bool run = false;
                do
                {
                    run |= await collector.Run(front, back, cancellationToken);
                }
                while (run);

                Thread.Sleep(interval * 1000);
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: ng catalog2registration -source <catalog> -contentBaseAddress <content-address> -storageBaseAddress <storage-base-address> -storageType file|azure [-storagePath <path>]|[-storageAccountName <azure-acc> -storageKeyValue <azure-key> -storageContainer <azure-container> -storagePath <path>] [-verbose true|false] [-interval <seconds>]");
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

            bool unlistShouldDelete = CommandHelpers.GetUnlistShouldDelete(arguments);

            bool verbose = CommandHelpers.GetVerbose(arguments);

            int interval = CommandHelpers.GetInterval(arguments);

            string contentBaseAddress = CommandHelpers.GetContentBaseAddress(arguments);

            StorageFactory storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            if (storageFactory == null)
            {
                PrintUsage();
                return;
            }

            StorageFactory compressedStorageFactory = CommandHelpers.CreateCompressedStorageFactory(arguments, verbose);

            if (verbose)
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
                Trace.AutoFlush = true;
            }

            Trace.TraceInformation("CONFIG source: \"{0}\" storage: \"{1}\" interval: {2} seconds", source, storageFactory, interval);

            if (compressedStorageFactory != null)
            {
                Loop(source, new AggregateStorageFactory(storageFactory, new [] { compressedStorageFactory }, SecondaryStorageContentInterceptor), contentBaseAddress, unlistShouldDelete, verbose, interval, cancellationToken).Wait();
            }
            else
            {
                Loop(source, storageFactory, contentBaseAddress, unlistShouldDelete, verbose, interval, cancellationToken).Wait();
            }
        }

        private StorageContent SecondaryStorageContentInterceptor(Uri primaryResourceUri, Uri secondaryResourceUri, StorageContent content)
        {
            var jTokenStorageContent = content as JTokenStorageContent;
            if (jTokenStorageContent != null)
            {
                var token = jTokenStorageContent.Content.DeepClone();

                if (primaryResourceUri.ToString().EndsWith("/index.json"))
                {
                    InterceptRegistrationIndex(primaryResourceUri, secondaryResourceUri, token);
                }
                else
                {
                    InterceptRegistrationPackage(primaryResourceUri, secondaryResourceUri, token);
                }

                return new JTokenStorageContent(token, content.ContentType, content.CacheControl);
            }

            return content;
        }

        private void InterceptRegistrationPackage(Uri primaryResourceUri, Uri secondaryResourceUri, JToken content)
        {
            var tokens = content.SelectTokens("@id")
                .Union(content.SelectTokens("registration"))
                .ToArray();

            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i].Replace(
                    ((string)tokens[i]).Replace(primaryResourceUri.ToString(), secondaryResourceUri.ToString()));
            }
        }

        private void InterceptRegistrationIndex(Uri primaryResourceUri, Uri secondaryResourceUri, JToken content)
        {
            var tokens = content.SelectTokens("@id")
                .Union(content.SelectTokens("items[*].@id"))
                .Union(content.SelectTokens("items[*].catalogEntry.@id"))
                .Union(content.SelectTokens("items[*].registration"))
                .Union(content.SelectTokens("items[*].parent"))
                .Union(content.SelectTokens("items.items[*].@id"))
                .Union(content.SelectTokens("items.items[*].catalogEntry.@id"))
                .Union(content.SelectTokens("items.items[*].registration"))
                .ToArray();

            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i].Replace(
                    ((string)tokens[i]).Replace(primaryResourceUri.ToString(), secondaryResourceUri.ToString()));
            }
        }
    }
}

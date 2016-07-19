// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Index;
using NuGet.Indexing;

namespace Ng
{
    public static class Catalog2Lucene
    {
        static async Task Loop(string source, string registration, Lucene.Net.Store.Directory directory, string catalogBaseAddress, string storageBaseAddress, bool verbose, int interval, CancellationToken cancellationToken)
        {
            Func<HttpMessageHandler> handlerFunc = CommandHelpers.GetHttpMessageHandlerFactory(verbose, catalogBaseAddress, storageBaseAddress);

            while (true)
            {
                using (var indexWriter = CreateIndexWriter(directory))
                {
                    var collector = new SearchIndexFromCatalogCollector(
                        index: new Uri(source),
                        indexWriter: indexWriter,
                        commitEachBatch: false,
                        baseAddress: catalogBaseAddress,
                        handlerFunc: handlerFunc);

                    ReadWriteCursor front = new LuceneCursor(indexWriter, MemoryCursor.MinValue);

                    ReadCursor back = registration == null
                        ? (ReadCursor)MemoryCursor.CreateMax()
                        : new HttpReadCursor(new Uri(registration), handlerFunc);

                    bool run = false;
                    do
                    {
                        run = await collector.Run(front, back, cancellationToken);

                        collector.EnsureCommitted(); // commit after each catalog page
                    }
                    while (run);
                }

                Thread.Sleep(interval * 1000);
            }
        }

        internal static IndexWriter CreateIndexWriter(Lucene.Net.Store.Directory directory)
        {
            bool create = !IndexReader.IndexExists(directory);

            directory.EnsureOpen();

            if (!create)
            {
                if (IndexWriter.IsLocked(directory))
                {
                    IndexWriter.Unlock(directory);
                }
            }

            IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), create, IndexWriter.MaxFieldLength.UNLIMITED);

            NuGetMergePolicyApplyer.ApplyTo(indexWriter);

            indexWriter.SetSimilarity(new CustomSimilarity());

            return indexWriter;
        }

        public static void PrintUsage()
        {
            Console.WriteLine("Usage: ng catalog2lucene "
                + $"-{Constants.Source} <catalog> "
                + $"[-{Constants.Registration} <registration-root>] "
                + $"-{Constants.LuceneDirectoryType} file|azure "
                + $"[-{Constants.LucenePath} <file-path>] "
                + "|"
                + $"[-{Constants.LuceneStorageAccountName} <azure-acc> "
                    + $"-{Constants.LuceneStorageKeyValue} <azure-key> "
                    + $"-{Constants.LuceneStorageContainer} <azure-container> "
                    + $"[-{Constants.VaultName} <keyvault-name> "
                        + $"-{Constants.ClientId} <keyvault-client-id> "
                        + $"-{Constants.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                        + $"[-{Constants.ValidateCertificate} true|false]]] "
                + $"[-{Constants.Verbose} true|false] "
                + $"[-{Constants.Interval} <seconds>]");
        }

        public static void Run(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            Lucene.Net.Store.Directory directory = CommandHelpers.GetLuceneDirectory(arguments);
            string source = CommandHelpers.GetSource(arguments);
            bool verbose = CommandHelpers.GetVerbose(arguments, required: false);

            if (verbose)
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
                //Trace.AutoFlush = true;
            }

            int interval = CommandHelpers.GetInterval(arguments, defaultInterval: Constants.DefaultInterval);

            string registration = CommandHelpers.GetRegistration(arguments, required: false);
            if (registration == null)
            {
                Console.WriteLine("Lucene index will be created up to the end of the catalog (alternatively if you provide a registration it will not pass that)");
            }

            string catalogBaseAddress = CommandHelpers.GetCatalogBaseAddress(arguments, required: false);
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

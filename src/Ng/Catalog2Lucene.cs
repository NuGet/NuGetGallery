using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
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
        static async Task Loop(string source, string registration, Lucene.Net.Store.Directory directory, bool verbose, int interval)
        {
            Func<HttpMessageHandler> handlerFunc = CommandHelpers.GetHttpMessageHandlerFactory(verbose);

            CommitCollector collector = new SearchIndexFromCatalogCollector(new Uri(source), directory, handlerFunc);

            ReadWriteCursor front = new LuceneCursor(directory, MemoryCursor.Min.Value);
            ReadCursor back = new HttpReadCursor(new Uri(registration), MemoryCursor.Max.Value, handlerFunc);

            while (true)
            {
                bool run = false;
                do
                {
                    run = await collector.Run(front, back);
                }
                while (run);

                Thread.Sleep(interval * 1000);
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: ng catalog2lucene -source <catalog> -registration <registration-root> -luceneDirectoryType file|azure [-luceneReset true|false] [-lucenePath <file-path>] | [-luceneStorageAccountName <azure-acc> -luceneStorageKeyValue <azure-key> -luceneStorageContainer <azure-container>] [-verbose true|false] [-interval <seconds>]");
        }

        public static void Run(string[] args)
        {
            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null)
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

            string registration = CommandHelpers.GetRegistration(arguments);
            if (registration == null)
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

            bool luceneReset = CommandHelpers.GetLuceneReset(arguments);
            if (luceneReset)
            {
                if (IndexReader.IndexExists(directory))
                {
                    using (IndexWriter writer = new IndexWriter(directory, new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), true, IndexWriter.MaxFieldLength.UNLIMITED))
                    {
                        writer.DeleteAll();
                        writer.Commit();
                    }
                }

                return;
            }

            Trace.TraceInformation("CONFIG source: \"{0}\" registration: \"{1}\" interval: {2} seconds", source, registration, interval);

            Loop(source, registration, directory, verbose, interval).Wait();
        }
    }
}

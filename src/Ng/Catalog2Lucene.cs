using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using NuGet.Indexing;
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
        static async Task Loop(string source, string registration, Lucene.Net.Store.Directory directory)
        {
            Func<HttpMessageHandler> handlerFunc = () => { return new VerboseHandler(); };

            CommitCollector collector = new SearchIndexFromCatalogCollector(new Uri(source), directory, handlerFunc);

            ReadWriteCursor front = new LuceneCursor(directory, MemoryCursor.Min.Value);
            ReadCursor back = new HttpReadCursor(new Uri(registration), MemoryCursor.Max.Value, handlerFunc);

            while (true)
            {
                bool run = false;
                do
                {
                    Trace.TraceInformation("BEFORE Run cursor @ {0}", front);
                    run = await collector.Run(front, back);
                    Trace.TraceInformation("AFTER Run cursor @ {0}", front);
                }
                while (run);

                Thread.Sleep(3000);
            }
        }

        public static void Run(string[] args)
        {
            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null)
            {
                return;
            }

            Lucene.Net.Store.Directory directory = CommandHelpers.GetLuceneDirectory(arguments);
            if (directory == null)
            {
                return;
            }

            bool luceneReset = CommandHelpers.GetLuceneReset(arguments);
            if (luceneReset)
            {
                //PackageIndexing.CreateNewEmptyIndex(directory);

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

            string source = CommandHelpers.GetSource(arguments);
            if (source == null)
            {
                return;
            }

            string registration = CommandHelpers.GetRegistration(arguments);
            if (registration == null)
            {
                return;
            }

            Trace.TraceInformation("CONFIG source: \"{0}\" registration: \"{1}\"", source, registration);

            Loop(source, registration, directory).Wait();
        }
    }
}

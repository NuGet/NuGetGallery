using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ng
{
    public static class Catalog2Registration
    {
        static async Task Loop(string source, StorageFactory storageFactory, string contentBaseAddress)
        {
            Uri index = new Uri(source);

            Func<HttpMessageHandler> handlerFunc = () => { return new VerboseHandler(); };

            RegistrationCatalogCollector collector = new RegistrationCatalogCollector(index, storageFactory, handlerFunc, 20);

            collector.ContentBaseAddress = new Uri(contentBaseAddress);

            Storage storage = storageFactory.Create();
            ReadWriteCursor cursor = new DurableCursor(storage.ResolveUri("cursor.json"), storage);

            while (true)
            {
                bool run = false;
                do
                {
                    Trace.TraceInformation("BEFORE Run cursor @ {0}", cursor.Value.ToString("O"));
                    run = await collector.Run(cursor, MemoryCursor.Max);
                    Trace.TraceInformation("AFTER Run cursor @ {0} batch count: {1}", cursor.Value.ToString("O"), collector.PreviousRunBatchCount);
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

            string source = CommandHelpers.GetSource(arguments);
            if (source == null)
            {
                return;
            }

            string contentBaseAddress = CommandHelpers.GetContentBaseAddress(arguments);
            if (contentBaseAddress == null)
            {
                return;
            }

            StorageFactory storageFactory = CommandHelpers.CreateStorageFactory(arguments);
            if (storageFactory == null)
            {
                return;
            }

            Trace.TraceInformation("CONFIG source: \"{0}\" storage: \"{1}\"", source, storageFactory);

            Loop(source, storageFactory, contentBaseAddress).Wait();
        }
    }
}

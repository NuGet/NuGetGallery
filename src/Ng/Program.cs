using NuGet.Services.Metadata.Catalog;
using System;
using System.Diagnostics;

namespace Ng
{
    class Program
    {
        static void PrintUsage()
        {
            Console.WriteLine("usage [feed2catalog|catalog2registration|catalog2lucene]");
        }

        static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.AutoFlush = true;

            try
            {
                if (args.Length == 0)
                {
                    PrintUsage();
                    return;
                }

                switch (args[0])
                {
                    case "feed2catalog" :
                        Feed2Catalog.Run(args);
                        break;
                    case "catalog2registration" :
                        Catalog2Registration.Run(args);
                        break;
                    case "catalog2lucene" :
                        Catalog2Lucene.Run(args);
                        break;
                    default:
                        PrintUsage();
                        break;
                }
            }
            catch (Exception e)
            {
                Utils.TraceException(e);
            }
        }
    }
}

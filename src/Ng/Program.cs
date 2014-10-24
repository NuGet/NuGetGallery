using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng
{
    class Program
    {
        static void PrintException(Exception e)
        {
            if (e is AggregateException)
            {
                foreach (Exception ex in ((AggregateException)e).InnerExceptions)
                {
                    PrintException(ex);
                }
            }
            else
            {
                Console.WriteLine("{0} {1}", e.GetType().Name, e.Message);
                Console.WriteLine("{0}", e.StackTrace);
                if (e.InnerException != null)
                {
                    PrintException(e.InnerException);
                }
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("usage [feed2catalog|catalog2registration|catalog2lucene]");
        }

        static void Main(string[] args)
        {
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
                PrintException(e);
            }
        }
    }
}

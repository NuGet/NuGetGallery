using Resolver.Metadata;
using Resolver.Resolver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resolver
{
    class Program
    {
        static void PrintException(Exception e)
        {
            Console.WriteLine(e.Message);
            if (e.InnerException != null)
            {
                PrintException(e.InnerException);
            }
        }

        static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            try
            {
                //TestSemanticVersion.Test0();
                //TestSemanticVersion.Test1();
                //TestSemanticVersion.Test2();

                //TestResolver.Test0().Wait();
                TestResolver.Test1().Wait();
                //TestResolver.Test2().Wait();
            }
            catch (AggregateException g)
            {
                foreach (Exception e in g.InnerExceptions)
                {
                    PrintException(e);
                }
            }
            catch (Exception e)
            {
                PrintException(e);
            }
        }
    }
}

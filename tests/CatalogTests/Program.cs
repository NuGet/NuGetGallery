using System;
using System.Diagnostics;

namespace CatalogTests
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

        static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            try
            {
                DateTime before = DateTime.Now;

                //BuilderTests.Test0();
                //BuilderTests.Test1();
                //BuilderTests.Test2();
                BuilderTests.Test3();

                //CollectorTests.Test0();
                //CollectorTests.Test1();
                //CollectorTests.Test2();
                //CollectorTests.Test4();
                //CollectorTests.Test5();

                //MakeTestCatalog.Test0();

                //PartitioningTests.Test0();
                //PartitioningTests.Test1();
                //RegistrationTests.Test1();
                //RegistrationTests.Test2();
                //RegistrationTests.Test3();
                //RegistrationTests.Test4();
                //RegistrationTests.Test5();
                //RegistrationTests.Test6();
                //RegistrationTests.Test7();

                //WarehouseCatalogTests.Test0();
                //WarehouseCatalogTests.Test1();
                
                //JsonLdCacheTests.Test0();

                //IntegrityTests.Test0();

                //InstallDataBrowser.Test0();

                DateTime after = DateTime.Now;

                Console.WriteLine("Total duration {0} seconds", (after - before).TotalSeconds);
            }
            catch (Exception e)
            {
                PrintException(e);
            }
        }
    }
}

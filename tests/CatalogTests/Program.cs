using System;

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
            try
            {
                DateTime before = DateTime.Now;

                //BuilderTests.Test1();
                //BuilderTests.Test0();

                //BuilderTests.Test5();
                
                CollectorTests.Test0();
                //CollectorTests.Test6();
                //CollectorTests.Test7();

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

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
               
                BuilderTests.Test1();
                //CollectorTests.Test0();

                //PartitioningTests.Test0();
                //PartitioningTests.Test1();

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

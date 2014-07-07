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

                //MakeTestData.Test0();
                //BuilderTests.Test0();
                //BuilderTests.Test1();
                //BuilderTests.Test2();
                //BuilderTests.Test3();
                //BuilderTests.Test4();

                CollectorTests.Test0();
                //CollectorTests.Test1();
                //CollectorTests.Test2();
                //CollectorTests.Test3();
                //CollectorTests.Test4();

                //End2EndTests.Test0();
                //End2EndTests.Test1();

                //CheckResults.Test0();

                //DeleteTests.Test0();
                //DeleteTests.Test1();

                //ExportTests.Test0();
                //ExportTests.Test1();
                //ExportTests.Test2();

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

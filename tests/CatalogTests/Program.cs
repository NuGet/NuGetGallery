using NuGet.Services.Metadata.Catalog;
using System;
using System.Diagnostics;
using System.Linq;

namespace CatalogTests
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length > 0 && args[0].Equals("dbg", StringComparison.OrdinalIgnoreCase))
            {
                Debugger.Launch();
                args = args.Skip(1).ToArray();
            }

            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.AutoFlush = true;

            try
            {
                DateTime before = DateTime.Now;

                //StorageTests.Test0();
                //StorageTests.Test1();
                //StorageTests.Test2();
                //StorageTests.Test3();

                //BuilderTests.Test0();
                //BuilderTests.Test1();
                //BuilderTests.Test2();
                //BuilderTests.Test3();

                //CollectorTests.Test0();
                //CollectorTests.Test1();
                //CollectorTests.Test2();
                //CollectorTests.Test4();
                //CollectorTests.Test5();
                //CollectorTests.Test6();
                //CollectorTests.Test7();

                //MakeTestCatalog.Test0();

                //CursorTests.Test0();
                //CursorTests.Test1();
                //CursorTests.Test2();
                //CursorTests.CreateNewCursor(args);

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

                //Feed2CatalogTests.Test0(args);

                //RegistrationTests.Test0();
                //RegistrationTests.Test1();
                //RegistrationTests.Test2();
                //RegistrationTests.Test3();
                //RegistrationTests.Test4();
                //RegistrationTests.Test5();
                //RegistrationTests.Test6();

                RegistrationMakerTests.Test0Async().Wait();

                DateTime after = DateTime.Now;

                Console.WriteLine("Total duration {0} seconds", (after - before).TotalSeconds);
            }
            catch (Exception e)
            {
                Utils.TraceException(e);
            }
        }
    }
}

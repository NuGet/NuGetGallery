using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class DeleteTests
    {
        public static async Task Test0Async()
        {
            Storage storage = new FileStorage("http://localhost:8000/", @"c:\data\site\test");

            //  first save the delete into the catalog

            using (AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 1000))
            {
                //writer.Add(new DeletePackageCatalogItem("Test.Metadata.Service", "1.0.0"));
                writer.Add(new DeletePackageCatalogItem("Test.Metadata.Service", "2.0.0"));
                //writer.Add(new DeletePackageCatalogItem("Test.Metadata.Service", "3.0.0"));
                await writer.Commit(DateTime.Now);
            }

            //  second perform that delete on the various feeds - in this case the default resolver feed
            
            ResolverDeleteCollector collector = new ResolverDeleteCollector(storage);
            await collector.Run(new Uri("http://localhost:8000/test/catalog/index.json"), DateTime.MinValue);
        }

        public static void Test0()
        {
            Console.WriteLine("DeleteTests.Test0");

            Test0Async().Wait();
        }

        public static async Task Test1Async()
        {
            Storage storage = new FileStorage("http://localhost:8000/", @"c:\data\site\full");

            //  first save the delete into the catalog

            using (AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 1000, true))
            {
                writer.Add(new DeleteRegistrationCatalogItem("abc"));
                await writer.Commit(DateTime.Now);
            }

            //  second perform that delete on the various feeds - in this case the default resolver feed

            ResolverDeleteCollector collector = new ResolverDeleteCollector(storage);
            await collector.Run(new Uri("http://localhost:8000/full/catalog/index.json"), DateTime.MinValue);
        }

        public static void Test1()
        {
            Console.WriteLine("DeleteTests.Test1");

            Test1Async().Wait();
        }
    }
}

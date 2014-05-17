using Catalog.Collecting;
using Catalog.Maintenance;
using Catalog.Persistence;
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
            Storage storage = new FileStorage
            {
                Path = @"c:\data\site\test",
                Container = "test",
                BaseAddress = "http://localhost:8000/"
            };

            //  first save the delete into the catalog

            CatalogContext context = new CatalogContext();
            using (CatalogWriter writer = new CatalogWriter(storage, context, 1000, true))
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
    }
}

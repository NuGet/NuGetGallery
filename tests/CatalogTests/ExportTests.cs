using NuGet.Services.Metadata.Catalog.GalleryIntegration;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class ExportTests
    {
        public static void Test0()
        {
            const int SqlChunkSize = 2000;
            string sqlConnectionString = "";

            const int CatalogBatchSize = 1000;
            const int CatalogMaxPageSize = 1000;
            //Storage storage = new FileStorage
            //{
            //    Path = @"c:\data\site\export",
            //    Container = "export",
            //    BaseAddress = "http://localhost:8000/"
            //};
            Storage storage = new AzureStorage
            {
                AccountName = "nuget3",
                AccountKey = "",
                Container = "export",
                BaseAddress = "http://nuget3.blob.core.windows.net/"
            };

            CatalogWriter writer = new CatalogWriter(storage, new CatalogContext(), CatalogMaxPageSize);

            GalleryExportBatcher batcher = new GalleryExportBatcher(CatalogBatchSize, writer);

            int lastHighestPackageKey = 0;

            //int count = 0;

            while (true)
            {
                Tuple<int, int> range = GalleryExport.GetNextRange(sqlConnectionString, lastHighestPackageKey, SqlChunkSize);

                if (range.Item1 == 0 && range.Item2 == 0)
                {
                    break;
                }

                //if (count++ == 3)
                //{
                //    break;
                //}

                Console.WriteLine("{0} {1}", range.Item1, range.Item2);

                GalleryExport.FetchRange(sqlConnectionString, range, batcher);

                lastHighestPackageKey = range.Item2;
            }

            batcher.Complete();

            Console.WriteLine(batcher.Total);
        }

        public static async Task Test1Async()
        {
            HashSet<int> keys = new HashSet<int>();

            GalleryKeyCollector collector = new GalleryKeyCollector(keys);

            await collector.Run(new Uri("http://localhost:8000/export/catalog/index.json"), DateTime.MinValue);
            Console.WriteLine("http requests: {0}", collector.RequestCount);

            Console.WriteLine(keys.Count);
        }

        public static void Test1()
        {
            Console.WriteLine("ExportTests.Test1");

            Test1Async().Wait();
        }

        public static async Task Test2Async()
        {
            //Storage storage = new FileStorage
            //{
            //    Path = @"c:\data\site\export",
            //    Container = "export",
            //    BaseAddress = "http://localhost:8000/"
            //};
            Storage storage = new AzureStorage
            {
                AccountName = "nuget3",
                AccountKey = "",
                Container = "export",
                BaseAddress = "http://nuget3.blob.core.windows.net/"
            };

            GalleryKeyRangeCollector collector = new GalleryKeyRangeCollector(storage, 200);

            await collector.Run(new Uri("http://nuget3.blob.core.windows.net/export/catalog/index.json"), DateTime.MinValue);
            Console.WriteLine("http requests: {0}", collector.RequestCount);
        }

        public static void Test2()
        {
            Console.WriteLine("ExportTests.Test2");

            Test2Async().Wait();
        }
    }
}

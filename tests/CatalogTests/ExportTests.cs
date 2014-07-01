using NuGet.Services.Metadata.Catalog.GalleryIntegration;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

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
            Storage storage = new AzureStorage(
                CloudStorageAccount.Parse("AccountName=nuget3;AccountKey=;DefaultEndpointsProtocol=https"), "export");

            CatalogWriter writer = new CatalogWriter(storage, new CatalogContext(), CatalogMaxPageSize);

            GalleryExportBatcher batcher = new GalleryExportBatcher(CatalogBatchSize, writer);

            int lastHighestPackageKey = 0;

            //int count = 0;

            while (true)
            {
                Tuple<int, int> range = GalleryExport.GetNextRange(sqlConnectionString, lastHighestPackageKey, SqlChunkSize).Result;

                if (range.Item1 == 0 && range.Item2 == 0)
                {
                    break;
                }

                //if (count++ == 3)
                //{
                //    break;
                //}

                Console.WriteLine("{0} {1}", range.Item1, range.Item2);

                GalleryExport.WriteRange(sqlConnectionString, range, batcher).Wait();

                lastHighestPackageKey = range.Item2;
            }

            batcher.Complete().Wait();

            Console.WriteLine(batcher.Total);
        }
    }
}

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
            const int SqlChunkSize = 8000;
            string sqlConnectionString = "";

            const int CatalogBatchSize = 1000;
            const int CatalogMaxPageSize = 1000;
            Storage storage = new FileStorage
            {
                Path = @"c:\data\site\export2",
                Container = "export2",
                BaseAddress = "http://localhost:8000/"
            };

            CatalogWriter writer = new CatalogWriter(storage, new CatalogContext(), CatalogMaxPageSize);

            GalleryExportBatcher batcher = new GalleryExportBatcher(CatalogBatchSize, writer);

            int lastHighestPackageKey = 0;

            int count = 0;

            while (true)
            {
                Tuple<int, int> range = GalleryExport.GetNextRange(sqlConnectionString, lastHighestPackageKey, SqlChunkSize);

                if (range.Item1 == 0 && range.Item2 == 0)
                {
                    break;
                }

                if (count++ == 3)
                {
                    break;
                }

                Console.WriteLine("{0} {1}", range.Item1, range.Item2);

                GalleryExport.FetchRange(sqlConnectionString, range, batcher);

                lastHighestPackageKey = range.Item2;
            }

            batcher.Complete();

            Console.WriteLine(batcher.Total);
        }
    }
}

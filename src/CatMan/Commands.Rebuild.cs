using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.GalleryIntegration;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using PowerArgs;

namespace CatMan
{
    public class RebuildArgs
    {
        [ArgShortcut("db")]
        [ArgDescription("Database connection string")]
        public string DatabaseConnection { get; set; }

        [ArgShortcut("nuspecs")]
        [ArgDescription("Path to folder filled with nuspecs")]
        public string NuSpecFolder { get; set; }

        [ArgShortcut("base")]
        [ArgDescription("Base address for the generated catalog data")]
        [DefaultValue("http://localhost:3333/catalog")]
        public string BaseAddress { get; set; }

        [ArgRequired]
        [ArgShortcut("d")]
        [ArgDescription("Destination folder for catalog data")]
        public string CatalogFolder { get; set; }
    }

    public partial class Commands
    {
        [ArgActionMethod]
        public void Rebuild(RebuildArgs args)
        {
            if (Directory.Exists(args.CatalogFolder))
            {
                Console.WriteLine("Catalog folder exists. Deleting!");
                Directory.Delete(args.CatalogFolder, recursive: true);
            }

            // Load storage
            Storage storage = new FileStorage(args.BaseAddress, args.CatalogFolder);
            using (var writer = new CatalogWriter(storage, new CatalogContext()))
            {
                if (!String.IsNullOrEmpty(args.DatabaseConnection))
                {
                    var batcher = new GalleryExportBatcher(2000, writer);
                    int lastHighest = 0;
                    while (true)
                    {
                        var range = GalleryExport.GetNextRange(
                            args.DatabaseConnection,
                            lastHighest,
                            2000).Result;
                        if (range.Item1 == 0 && range.Item2 == 0)
                        {
                            break;
                        }
                        Console.WriteLine("Writing packages with Keys {0}-{1} to catalog...", range.Item1, range.Item2);
                        GalleryExport.WriteRange(
                            args.DatabaseConnection,
                            range,
                            batcher).Wait();
                        lastHighest = range.Item2;
                    }
                    batcher.Complete().Wait();
                }
                else
                {
                    throw new NotImplementedException("TODO: Build from NuSpecs");
                }
            }
        }
    }
}

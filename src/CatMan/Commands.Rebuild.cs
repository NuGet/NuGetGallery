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
using NuGet.Services.Metadata.Catalog.Collecting;
using System.Diagnostics;

namespace CatMan
{
    public class RebuildArgs
    {
        [ArgShortcut("db")]
        [ArgDescription("Database connection string")]
        public string DatabaseConnection { get; set; }

        [ArgShortcut("nupkgs")]
        [ArgDescription("Path to folder filled with nupkgs and/or nuspecs")]
        public string NuPkgFolder { get; set; }

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
            const int batchSize = 2000;

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
                    var batcher = new GalleryExportBatcher(batchSize, writer);
                    int lastHighest = 0;
                    while (true)
                    {
                        var range = GalleryExport.GetNextRange(
                            args.DatabaseConnection,
                            lastHighest,
                            batchSize).Result;
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
                else if (!String.IsNullOrEmpty(args.NuPkgFolder))
                {
                    Stopwatch timer = new Stopwatch();
                    timer.Start();

                    // files are sorted by GetFiles
                    Queue<string> files = new Queue<string>(Directory.GetFiles(args.NuPkgFolder, "*.nu*", SearchOption.TopDirectoryOnly)
                        .Where(s => s.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)));

                    int total = files.Count;

                    ParallelOptions options = new ParallelOptions();
                    options.MaxDegreeOfParallelism = 8;

                    Task commitTask = null;

                    while (files.Count > 0)
                    {
                        Queue<PackageCatalogItem> currentBatch = new Queue<PackageCatalogItem>(batchSize);

                        // create the batch
                        while (currentBatch.Count < batchSize && files.Count > 0)
                        {
                            string file = files.Dequeue();

                            if (file.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                            {
                                currentBatch.Enqueue(new NupkgCatalogItem(file));
                            }
                            else
                            {
                                currentBatch.Enqueue(new NuspecPackageCatalogItem(file));
                            }
                        }

                        // process the nupkgs and nuspec files in parallel
                        Parallel.ForEach(currentBatch, options, nupkg =>
                        {
                            nupkg.Load();
                        });

                        // wait for the previous commit to finish before adding more
                        if (commitTask != null)
                        {
                            commitTask.Wait();
                        }

                        // add everything from the queue
                        foreach(PackageCatalogItem item in currentBatch)
                        {
                            writer.Add(item);
                        }

                        // commit
                        commitTask = Task.Run(async () => await writer.Commit(DateTime.UtcNow));
                        Console.WriteLine("committing {0}/{1}", total - files.Count, total);
                    }

                    // wait for the final commit
                    if (commitTask != null)
                    {
                        commitTask.Wait();
                    }

                    timer.Stop();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Committed {0} catalog items in {1}", total, timer.Elapsed);
                    Console.ResetColor();
                }
            }
        }
    }
}

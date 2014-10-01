using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetFeed
{
    public class CatalogStep : BuildStep
    {
        private readonly int _batchSize;
        private readonly Queue<string> _nupkgs;

        public CatalogStep(Config config, IEnumerable<string> nupkgs, int batchSize=1000)
            : base(config, "CreateCatalog")
        {
            _nupkgs = new Queue<string>(nupkgs);
            _batchSize = batchSize;
        }

        protected override void RunCore()
        {
            Config.Catalog.LocalFolder.Create();

            int total = _nupkgs.Count;

            Log("Processing " + total + " nupkgs");

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 8;

            Task commitTask = null;

            using (var writer = new AppendOnlyCatalogWriter(Config.Catalog.Storage, new CatalogContext()))
            {
                while (_nupkgs.Count > 0)
                {
                    Queue<PackageCatalogItem> currentBatch = new Queue<PackageCatalogItem>(_batchSize);

                    // create the batch
                    while (currentBatch.Count < _batchSize && _nupkgs.Count > 0)
                    {
                        string file = _nupkgs.Dequeue();

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
                    foreach (PackageCatalogItem item in currentBatch)
                    {
                        writer.Add(item);
                    }

                    // commit
                    commitTask = Task.Run(async () => await writer.Commit(DateTime.UtcNow));

                    ProgressUpdate(total - _nupkgs.Count, total);
                }

                // wait for the final commit
                if (commitTask != null)
                {
                    commitTask.Wait();
                }
            }
        }

    }
}

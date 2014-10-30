using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class PartitionedRegJob : CollectorJob
    {
        private readonly StorageFactory _factory;
        private readonly CollectorHttpClient _client;

        public PartitionedRegJob(Config config, Storage storage, StorageFactory factory, CollectorHttpClient client)
            : base(config, storage, "partitionedreg")
        {
            _factory = factory;
            _client = client;
        }

        public override async Task RunCore()
        {
            int nextMasterRegId = 0;

            DateTime position = Cursor.Position;

            JToken nextMasterRegIdToken = null;
            if (Cursor.Metadata.TryGetValue("nextMasterRegId", out nextMasterRegIdToken))
            {
                nextMasterRegId = nextMasterRegIdToken.ToObject<int>();
            }

            // Get the catalog index
            Uri catalogIndexUri = new Uri(Config.GetProperty("CatalogIndex"));

            Log("Reading index entries");

            var indexReader = new CatalogIndexReader(catalogIndexUri);

            var indexEntries = await indexReader.GetEntries();

            var context = indexReader.GetContext();

            Log("Finding new or editted entries");

            var changedEntries = new HashSet<string>(indexEntries.Where(e => e.CommitTimeStamp.CompareTo(position) > 0)
                                                                    .Select(e => e.Id.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

            DateTime newPosition = indexEntries.Select(e => e.CommitTimeStamp).OrderByDescending(e => e).FirstOrDefault();

            ConcurrentDictionary<string, ConcurrentBag<Uri>> batches = new ConcurrentDictionary<string, ConcurrentBag<Uri>>(StringComparer.OrdinalIgnoreCase);

            var idComparer = CatalogIndexEntry.IdComparer;

            ParallelOptions options = new ParallelOptions();
             options.MaxDegreeOfParallelism = 8;

            Parallel.ForEach(indexEntries, options, entry =>
            {
                if (changedEntries.Contains(entry.Id))
                {
                    batches.AddOrUpdate(entry.Id, new ConcurrentBag<Uri>() { entry.Uri }, (id, bag) =>
                    {
                        bag.Add(entry.Uri);
                        return bag;
                    });
                }
            });


            Uri contentBaseAddress = new Uri(Config.GetProperty("ContentBaseAddress"));

            if (batches.Count > 0)
            {
                Log("Building registrations from: " + position.ToString("O"));
                options.MaxDegreeOfParallelism = 4;

                for (int i = 0; i < 3 && batches.Count > 0; i++)
                {
                    if (i != 0)
                    {
                        options.MaxDegreeOfParallelism = 1;
                        Console.WriteLine("Single batch run.");
                    }

                    var ids = batches.Keys.OrderBy(s => s).ToArray();

                    Stopwatch buildTimer = new Stopwatch();
                    buildTimer.Start();
                    int startingCount = ids.Length;

                    Parallel.ForEach(ids, options, id =>
                    {
                        try
                        {
                            BatchRegistrationCollector regCollector = new BatchRegistrationCollector(null, _factory);
                            regCollector.ContentBaseAddress = contentBaseAddress;

                            Stopwatch timer = new Stopwatch();
                            timer.Start();

                            var uriGroup = batches[id].ToArray();

                            regCollector.ProcessGraphs(_client, id, uriGroup, context).Wait();

                            int rem = batches.Count;

                            timer.Stop();
                            string log = String.Format("Completed: {0} Duration: {1} Uris: {2} Remaining Ids: {3} Loop: {4}", id, timer.Elapsed, uriGroup.Length, rem, i);
                            Console.WriteLine(log);

                            // stats
                            double perPackage = buildTimer.Elapsed.TotalSeconds / (double)(startingCount - rem + 1);
                            DateTime finish = DateTime.Now.AddSeconds(Math.Ceiling(perPackage * rem));

                            Console.WriteLine("Estimated Finish: " + finish.ToString("O"));

                            ConcurrentBag<Uri> vals;
                            if (!batches.TryRemove(id, out vals))
                            {
                                Console.WriteLine("Unable to remove!");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError("Registration failed: " + id + " " + ex.ToString());
                        }
                    });
                }

                // mark this with the last commit we included
                Cursor.Position = newPosition;

                await Cursor.Save();

                Log("Finished registrations: " + newPosition.ToString("O"));
            }
        }


    }
}

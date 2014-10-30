//using Microsoft.WindowsAzure.Storage.Queue;
//using Newtonsoft.Json.Linq;
//using NuGet.Services.Metadata.Catalog.Collecting;
//using NuGet.Services.Metadata.Catalog.Persistence;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using VDS.RDF;

//namespace NuGet.Canton.jobs
//{
//    public class QueueRegBatchesJob : CollectorJob
//    {
//        private readonly CollectorHttpClient _httpClient;

//        public QueueRegBatchesJob(Config config, Storage storage)
//            : base(config, storage, "queueregbatches")
//        {
//            _httpClient = new CollectorHttpClient();
//        }

//        public override async Task RunCore()
//        {
//            int nextMasterRegId = 0;

//            DateTime position = Cursor.Position;

//            JToken nextMasterRegIdToken = null;
//            if (Cursor.Metadata.TryGetValue("nextMasterRegId", out nextMasterRegIdToken))
//            {
//                nextMasterRegId = nextMasterRegIdToken.ToObject<int>();
//            }

//            // Get the catalog index
//            Uri catalogIndexUri = new Uri(Config.GetProperty("CatalogIndex"));

//            Log("Reading index entries");

//            var indexReader = new CatalogIndexReader(catalogIndexUri);

//            var indexEntries = await indexReader.GetEntries();

//            Log("Finding new or editted entries");

//            var changedEntries = new HashSet<string>(indexEntries.Where(e => e.CommitTimeStamp.CompareTo(position) > 0)
//                                                                    .Select(e => e.Id.ToLowerInvariant()));

//            DateTime newPosition = indexEntries.Select(e => e.CommitTimeStamp).OrderByDescending(e => e).FirstOrDefault();

//            Dictionary<string, Uri[]> batches = new Dictionary<string, Uri[]>(StringComparer.OrdinalIgnoreCase);

//            var idComparer = CatalogIndexEntry.IdComparer;

//            foreach (var entry in changedEntries)
//            {
//                batches.Add(entry, indexEntries.Where(e => StringComparer.OrdinalIgnoreCase.Equals(e.Id, entry))
//                    .OrderBy(e => e.CommitTimeStamp)
//                    .Select(e => e.Uri)
//                    .ToArray());
//            }

//            if (batches.Count > 0)
//            {
//                Log("Uploading batches to the queue.");

//                var queueClient = Account.CreateCloudQueueClient();

//                var batchQueue = queueClient.GetQueueReference(CantonConstants.RegBatchQueue);
//                var masterQueue = queueClient.GetQueueReference(CantonConstants.RegMasterBatchQueue);

//                ParallelOptions options = new ParallelOptions();
//                options.MaxDegreeOfParallelism = 64;

//                int regBatchId = 0;

//                Parallel.ForEach(changedEntries, options, id =>
//                {
//                    int curBatchId = 0;

//                    lock (this)
//                    {
//                        curBatchId = regBatchId;
//                        regBatchId++;
//                    }

//                    JObject summary = new JObject();
//                    summary.Add("submitted", DateTime.UtcNow.ToString("O"));
//                    summary.Add("failures", 0);
//                    summary.Add("host", Host);
//                    summary.Add("cantonRegBatchId", regBatchId);
//                    summary.Add("packageId", id);
//                    summary.Add("cantonMasterRegBatchId", nextMasterRegId);
                    
//                    JArray uris = new JArray();

//                    foreach (var uri in batches[id])
//                    {
//                        uris.Add(uri.AbsoluteUri);
//                    }

//                    summary.Add("uris", uris);

//                    string json = summary.ToString();

//                    byte[] data = CantonUtilities.Compress(json);

//                    CloudQueueMessage message = new CloudQueueMessage(data);
//                    batchQueue.AddMessage(message);
//                });

//                JObject masterRecord = new JObject();
//                masterRecord.Add("submitted", DateTime.UtcNow.ToString("O"));
//                masterRecord.Add("failures", 0);
//                masterRecord.Add("host", Host);
//                masterRecord.Add("highestCantonRegBatchId", (regBatchId - 1));
//                masterRecord.Add("cantonMasterRegBatchId", nextMasterRegId);

//                CloudQueueMessage masterMessage = new CloudQueueMessage(masterRecord.ToString());
//                masterQueue.AddMessage(masterMessage);

//                // mark this with the last commit we included
//                Cursor.Position = newPosition;

//                JObject metadata = new JObject();
//                metadata.Add("nextMasterRegId", nextMasterRegId + 1);
//                Cursor.Metadata = metadata;

//                await Cursor.Save();

//                Log("Finished. masterRegId: " + nextMasterRegId);
//            }
//        }

//        private static int SortPages(Tuple<DateTime, Uri> x, Tuple<DateTime, Uri> y)
//        {
//            return x.Item1.CompareTo(y.Item1);
//        }
//    }
//}

//using Microsoft.WindowsAzure.Storage;
//using Microsoft.WindowsAzure.Storage.Queue;
//using Newtonsoft.Json.Linq;
//using NuGet.Services.Metadata.Catalog.Persistence;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Data.SqlClient;
//using System.Globalization;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace NuGet.Canton
//{
//    /// <summary>
//    /// Reads the gallery DB and queues new packages.
//    /// </summary>
//    public class QueueNewPackagesFromGallery : CollectorJob
//    {
//        public const string CursorName = "queuenewpackagesfromgallery";
//        private const int BatchSize = 2000;
//        private int _cantonCommitId = 0;
//        private ConcurrentQueue<Task> _queueTasks;

//        public QueueNewPackagesFromGallery(Config config, Storage storage)
//            : base(config, storage, CursorName)
//        {
//            _queueTasks = new ConcurrentQueue<Task>();
//        }

//        public override async Task RunCore()
//        {
//            int lastHighest = 0;

//            JToken lastHighestToken = null;
//            if (Cursor.Metadata.TryGetValue("lastHighest", out lastHighestToken))
//            {
//                lastHighest = lastHighestToken.ToObject<int>();
//            }

//            JToken cantonCommitIdToken = null;
//            if (Cursor.Metadata.TryGetValue("cantonCommitId", out cantonCommitIdToken))
//            {
//                _cantonCommitId = cantonCommitIdToken.ToObject<int>();
//            }

//            DateTime end = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(15));

//            var client = Account.CreateCloudQueueClient();
//            var queue = client.GetQueueReference(CantonConstants.GalleryPagesQueue);
//            string dbConnStr = Config.GetProperty("GalleryConnectionString");

//            Action<Uri> handler = (resourceUri) => QueuePage(resourceUri, queue);

//            // Load storage
//            using (var writer = new GalleryPageCreator(Storage, handler))
//            {
//                var batcher = new GalleryExportBatcher(BatchSize, writer);
//                while (_run)
//                {
//                    var range = GalleryExport.GetNextRange(
//                        dbConnStr,
//                        lastHighest,
//                        BatchSize).Result;

//                    if (range.Item1 == 0 && range.Item2 == 0)
//                    {
//                        break;
//                    }

//                    Log(String.Format(CultureInfo.InvariantCulture, "Writing packages with Keys {0}-{1} to catalog...", range.Item1, range.Item2));
//                    GalleryExport.WriteRange(
//                        dbConnStr,
//                        range,
//                        batcher).Wait();
//                    lastHighest = range.Item2;

//                    // make sure the queue is caught up
//                    Task curTask = null;
//                    while (_queueTasks.TryDequeue(out curTask))
//                    {
//                        curTask.Wait();
//                    }

//                    //Log("Just one batch, remove this later!!!");
//                    //break; //one batch at a time REMOVE THIS LATER!!!!
//                }

//                // wait for the batch to write
//                batcher.Complete().Wait();

//                Task.WaitAll(_queueTasks.ToArray());

//                // update the cursor
//                JObject obj = new JObject();
//                obj.Add("lastHighest", lastHighest);

//                // keep track of the order we added these in so that the catalog writer can put them back into order
//                obj.Add("cantonCommitId", _cantonCommitId);

//                Cursor.Position = DateTime.UtcNow;
//                Cursor.Metadata = obj;
//                await Cursor.Save();
//            }
//        }

//        // Add the page that was created to the queue for processing later
//        private void QueuePage(Uri uri, CloudQueue queue)
//        {
//            int curId = 0;

//            lock (this)
//            {
//                curId = _cantonCommitId;
//                _cantonCommitId++;
//            }

//            JObject summary = new JObject();
//            summary.Add("uri", uri.AbsoluteUri);
//            summary.Add("submitted", DateTime.UtcNow.ToString("O"));
//            summary.Add("failures", 0);
//            summary.Add("host", Host);
//            summary.Add("cantonCommitId", curId);

//            _queueTasks.Enqueue(queue.AddMessageAsync(new CloudQueueMessage(summary.ToString())));

//            Log("Gallery page. Commit id: " + curId + " Uri: " + uri.AbsoluteUri);
//        }
//    }
//}

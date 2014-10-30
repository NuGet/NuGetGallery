using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Canton
{
    public class CatalogPageCommitJob : CursorQueueFedJob
    {
        private const int BatchSize = 500;

        public CatalogPageCommitJob(Config config, Storage storage)
            : base(config, storage, CantonConstants.CatalogPageQueue, "catalogpagecommit")
        {

        }

        private Dictionary<int, string> GetWork()
        {
            TimeSpan hold = TimeSpan.FromMinutes(90);

            Dictionary<int, string> all = new Dictionary<int, string>();

            var messages = Queue.GetMessages(32, hold).ToList();

            Stack<Task> tasks = new Stack<Task>(2000);

            while (messages.Count > 0)
            {
                foreach (var message in messages)
                {
                    string s = message.AsString;
                    JObject json = JObject.Parse(s);
                    int curId = json["cantonCommitId"].ToObject<int>();

                    if (!all.ContainsKey(curId))
                    {
                        all.Add(curId, s);
                    }
                    else
                    {
                        Console.WriteLine("Dupe id!");
                    }

                    tasks.Push(Queue.DeleteMessageAsync(message));

                    if (all.Count % 10000 == 0)
                    {
                        Console.WriteLine(all.Count);
                    }
                }

                // stop if we aren't maxing out
                if (messages.Count == 32)
                {
                    messages = Queue.GetMessages(32, hold).ToList();
                }
                else
                {
                    messages = new List<CloudQueueMessage>();
                }

                if (tasks.Count > 2000)
                {
                    Task.WaitAll(tasks.ToArray());
                    tasks.Clear();
                }
            }

            Task.WaitAll(tasks.ToArray());
            tasks.Clear();

            return all;
        }

        public override async Task RunCore()
        {
            TimeSpan hold = TimeSpan.FromMinutes(90);
            int cantonCommitId = 0;

            JToken cantonCommitIdToken = null;
            if (Cursor.Metadata.TryGetValue("cantonCommitId", out cantonCommitIdToken))
            {
                cantonCommitId = cantonCommitIdToken.ToObject<int>();
            }

            Queue<JObject> orderedMessages = new Queue<JObject>();

            var blobClient = Account.CreateCloudBlobClient();

            Stopwatch giveup = new Stopwatch();
            giveup.Start();

            Dictionary<int, string> unQueuedMessages = new Dictionary<int, string>();

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 8;

            ConcurrentBag<CantonCatalogItem> batchItems = new ConcurrentBag<CantonCatalogItem>();
            Task commitTask = null;

            try
            {
                using (AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(Storage, 600))
                {
                    try
                    {
                        // get everything in the queue
                        Log("Getting work");
                        var newWork = GetWork();
                        Log("Done getting work");

                        // everything must run in canton commit order!
                        while (_run && (newWork.Count > 0 || unQueuedMessages.Count > 0 || orderedMessages.Count > 0))
                        {
                            Log(String.Format("New: {0} Waiting: {1} Ordered: {2}", newWork.Count, unQueuedMessages.Count, orderedMessages.Count));

                            int[] newIds = newWork.Keys.ToArray();

                            foreach (int curId in newIds)
                            {
                                string s = newWork[curId];
                                JObject json = JObject.Parse(s);
                                int id = json["cantonCommitId"].ToObject<int>();

                                if (id >= cantonCommitId && !unQueuedMessages.ContainsKey(id))
                                {
                                    unQueuedMessages.Add(id, s);
                                }
                                else
                                {
                                    LogError("Ignoring old cantonCommitId: " + id + " We are on: " + cantonCommitId);
                                }
                            }

                            // load up the next 4096 work items we need
                            while (unQueuedMessages.ContainsKey(cantonCommitId) && orderedMessages.Count < 4096)
                            {
                                JObject json = JObject.Parse(unQueuedMessages[cantonCommitId]);

                                orderedMessages.Enqueue(json);
                                unQueuedMessages.Remove(cantonCommitId);

                                cantonCommitId++;

                                giveup.Restart();
                            }

                            // just take up to the batch size
                            Queue<JObject> currentBatch = new Queue<JObject>();

                            // get up to the batchsize
                            while (currentBatch.Count < BatchSize && orderedMessages.Count > 0 && (currentBatch.Count + batchItems.Count) < BatchSize)
                            {
                                currentBatch.Enqueue(orderedMessages.Dequeue());
                            }

                            if (currentBatch.Count > 0)
                            {
                                Stopwatch timer = new Stopwatch();
                                timer.Start();
                                int before = batchItems.Count;

                                Parallel.ForEach(currentBatch, options, workJson =>
                                {
                                    try
                                    {
                                        int curId = workJson["cantonCommitId"].ToObject<int>();
                                        string resourceUriString = workJson["uri"].ToString();

                                        if (!StringComparer.OrdinalIgnoreCase.Equals(resourceUriString, "https://failed/"))
                                        {
                                            Uri resourceUri = new Uri(resourceUriString);

                                            // the page is loaded from storage in the background
                                            CantonCatalogItem item = new CantonCatalogItem(Account, resourceUri, curId);

                                            // download the graph, this is a blocking call
                                            item.LoadGraph();

                                            // add the item to the batch to be committed in order later
                                            batchItems.Add(item);
                                        }
                                        else
                                        {
                                            Log("Skipping failed page: " + curId);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogError("Unable to create page: " + ex.ToString());
                                    }
                                });

                                timer.Stop();
                                Console.WriteLine(String.Format(CultureInfo.InvariantCulture, "Loaded {0} pre-built pages in {1}", (batchItems.Count - before), timer.Elapsed));
                            }

                            // commit the items
                            if (batchItems.Count >= BatchSize)
                            {
                                CantonCatalogItem[] curItems = batchItems.ToArray();
                                batchItems = new ConcurrentBag<CantonCatalogItem>();

                                if (commitTask != null)
                                {
                                    await commitTask;
                                }

                                // make certain this ALL runs on another thread
                                commitTask = Task.Run(async () => await Commit(writer, curItems));
                            }

                            // get the next work item
                            if (_run)
                            {
                                newWork = GetWork();
                            }
                            else
                            {
                                newWork = new Dictionary<int, string>();
                            }

                            if (newWork.Count < 1 && _run)
                            {
                                // just give up after 5 minutes 
                                // TODO: handle this better
                                if (giveup.Elapsed > TimeSpan.FromMinutes(30) || unQueuedMessages.Count > 20000)
                                {
                                    while (!unQueuedMessages.ContainsKey(cantonCommitId))
                                    {
                                        LogError("Giving up on: " + cantonCommitId);
                                        cantonCommitId++;
                                    }
                                }
                                else
                                {
                                    // avoid getting out of control when the pages aren't ready yet
                                    Log("PageCommitJob Waiting for: " + cantonCommitId);
                                    Thread.Sleep(TimeSpan.FromSeconds(15));
                                }
                            }
                        }
                    }
                    finally
                    {
                        // commit anything that was waiting
                        if (commitTask != null)
                        {
                            commitTask.Wait();
                        }

                        Commit(writer, batchItems.ToArray()).Wait();
                    }
                }
            }
            finally
            {
                Log("returning work to the queue");

                // put everything back into the queue
                ParallelOptions qOpts = new ParallelOptions();
                qOpts.MaxDegreeOfParallelism = 128;

                Parallel.ForEach(orderedMessages, qOpts, json =>
                    {
                        Queue.AddMessage(new CloudQueueMessage(json.ToString()));
                    });

                Parallel.ForEach(unQueuedMessages.Values, qOpts, s =>
                {
                    Queue.AddMessage(new CloudQueueMessage(s));
                });

                Log("returning work to the queue done");
            }
        }

        private async Task Commit(AppendOnlyCatalogWriter writer, CantonCatalogItem[] batchItems)
        {
            var orderedBatch = batchItems.ToList();
            orderedBatch.Sort(CantonCatalogItem.Compare);

            int lastHighestCommit = 0;
            DateTime? latestPublished = null;

            // add the items to the writer
            foreach (var orderedItem in orderedBatch)
            {
                lastHighestCommit = orderedItem.CantonCommitId + 1;
                writer.Add(orderedItem);
            }

            Task cursorTask = null;

            // only save the cursor if we did something
            if (lastHighestCommit > 0)
            {
                // find the most recent package
                latestPublished = batchItems.Select(c => c.Published).OrderByDescending(d => d).FirstOrDefault();

                // update the cursor
                JObject obj = new JObject();
                // add one here since we are already added the current number
                obj.Add("cantonCommitId", lastHighestCommit);
                Log("Cursor cantonCommitId: " + lastHighestCommit);

                Cursor.Position = DateTime.UtcNow;
                Cursor.Metadata = obj;
                cursorTask = Cursor.Save();
            }

            if (writer.Count > 0)
            {
                // perform the commit
                Stopwatch timer = new Stopwatch();
                timer.Start();

                IGraph commitData = PackageCatalog.CreateCommitMetadata(writer.RootUri, latestPublished, latestPublished);

                // commit
                await writer.Commit(DateTime.UtcNow, commitData);

                timer.Stop();
                Console.WriteLine("Commit duration: " + timer.Elapsed);
            }

            if (cursorTask != null)
            {
                await cursorTask;
            }
        }
    }
}

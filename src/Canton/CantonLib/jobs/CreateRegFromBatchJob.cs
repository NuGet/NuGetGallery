//using Microsoft.WindowsAzure.Storage.Queue;
//using Newtonsoft.Json.Linq;
//using NuGet.Services.Metadata.Catalog.Persistence;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Globalization;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace NuGet.Canton.jobs
//{
//    public class CreateRegFromBatchJob : QueueFedJob
//    {
//        private const int GetMessageMax = 1;
//        private ConcurrentDictionary<int, bool> _workQueueStatus;
//        private ConcurrentQueue<Task> _queueTasks;
//        private JObject _context;

//        public CreateRegFromBatchJob(Config config, Storage storage, JObject context)
//            : base(config, storage, CantonConstants.RegBatchQueue)
//        {
//            _workQueueStatus = new ConcurrentDictionary<int, bool>();
//            _queueTasks = new ConcurrentQueue<Task>();
//            _context = context;
//            _context = context;
//        }

//        public override async Task RunCore()
//        {
//            TimeSpan hold = TimeSpan.FromMinutes(200);

//            var qClient = Account.CreateCloudQueueClient();
//            var finishedPagesQueue = qClient.GetQueueReference(CantonConstants.RegBatchPagesQueue);

//            var messages = Queue.GetMessages(GetMessageMax, hold).ToList();

//            TransHttpClient httpClient = new TransHttpClient(Account, Config.GetProperty("BaseAddress"));

//            while (_run && (messages.Count > 0))
//            {

//                foreach (var message in messages)
//                {

//                    JObject work = JObject.Parse(CantonUtilities.Decompress(message.AsBytes));
//                    int cantonRegBatchId = work["cantonRegBatchId"].ToObject<int>();
//                    string packageId = work["packageId"].ToString();

//                    try
//                    {
//                        List<Uri> uris = new List<Uri>();

//                        foreach (var uriToken in work["uris"])
//                        {
//                            uris.Add(new Uri(uriToken.ToString()));
//                        }

//                        Log("started cantonRegBatchId: " + cantonRegBatchId + " package: " + packageId);

//                        string specialPath = String.Format(CultureInfo.InvariantCulture, "registration-{0}",  Guid.NewGuid().ToString());

//                        AzureStorageFactory factory = new AzureStorageFactory(Account, Config.GetProperty("tmp"), specialPath);

//                        BatchRegistrationCollector regCreator = new BatchRegistrationCollector(factory);

//                        Stopwatch timer = new Stopwatch();
//                        timer.Start();

//                        await regCreator.ProcessGraphs(httpClient, packageId, uris, _context);

//                        timer.Stop();

//                        QueuePage(specialPath, cantonRegBatchId, finishedPagesQueue, packageId);

//                        Log("finished cantonRegBatchId: " + cantonRegBatchId + " package: " + packageId + " time: " + timer.Elapsed + " graphs: " + uris.Count);
//                    }
//                    finally
//                    {
//                        // mark as failed
//                        QueuePage("https://failed", cantonRegBatchId, finishedPagesQueue, packageId);
//                    }

//                    // get the next work item
//                    if (_run)
//                    {
//                        messages = Queue.GetMessages(GetMessageMax, hold).ToList();
//                    }
//                    else
//                    {
//                        messages = new List<CloudQueueMessage>();
//                    }
//                }
//            }
//        }

//        private void QueuePage(string specialPath, int cantonRegBatchId, CloudQueue queue, string packageId)
//        {
//            // mark that we sent a message for this
//            _workQueueStatus.AddOrUpdate(cantonRegBatchId, true, (k, v) => true);

//            JObject summary = new JObject();
//            summary.Add("specialPath", specialPath);
//            summary.Add("submitted", DateTime.UtcNow.ToString("O"));
//            summary.Add("failures", 0);
//            summary.Add("host", Host);
//            summary.Add("cantonRegBatchId", cantonRegBatchId);
//            summary.Add("packageId", packageId);

//            _queueTasks.Enqueue(queue.AddMessageAsync(new CloudQueueMessage(summary.ToString())));

//            Log("Registration creation: Batch: " + cantonRegBatchId + " Id: " + packageId);
//        }

//    }
//}

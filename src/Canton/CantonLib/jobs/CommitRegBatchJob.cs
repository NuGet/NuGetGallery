//using Microsoft.WindowsAzure.Storage.Queue;
//using Newtonsoft.Json.Linq;
//using NuGet.Services.Metadata.Catalog.Persistence;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace NuGet.Canton.jobs
//{
//    public class CommitRegBatchJob : CursorQueueFedJob
//    {
//        private CloudQueue _pageBatchQueue;

//        public CommitRegBatchJob(Config config, Storage storage)
//            : base(config, storage, CantonConstants.RegMasterBatchQueue, "commitregbatch")
//        {
//            var qClient = Account.CreateCloudQueueClient();
//            _pageBatchQueue = qClient.GetQueueReference(CantonConstants.RegBatchPagesQueue);
//        }

//        public override async Task RunCore()
//        {
            

//            Dictionary<int, JObject> masterBatches = new Dictionary<int, JObject>();
//            Dictionary<int, List<JObject>> pageBatches = new Dictionary<int, List<JObject>>();

//            // the current batch we are expecting
//            int curMasterRegId = 0;

//            DateTime position = Cursor.Position;

//            JToken curMasterRegIdToken = null;
//            if (Cursor.Metadata.TryGetValue("curMasterRegId", out curMasterRegIdToken))
//            {
//                curMasterRegId = curMasterRegIdToken.ToObject<int>();
//            }

//            int curBatchRegId = 0;

//            JToken curBatchRegIdToken = null;
//            if (Cursor.Metadata.TryGetValue("curBatchRegId", out curBatchRegIdToken))
//            {
//                curBatchRegId = curBatchRegIdToken.ToObject<int>();
//            }

//            try
//            {
//                List<JObject> newWork = GetWork();

//                while (_run && (newWork.Count > 0 || masterBatches.Count > 0))
//                {
//                    foreach (var json in newWork)
//                    {
//                        int masterBatchId = json["cantonMasterRegBatchId"].ToObject<int>();

//                        if (!masterBatches.ContainsKey(masterBatchId))
//                        {
//                            masterBatches.Add(masterBatchId, json);
//                        }
//                        else
//                        {
//                            LogError("Ignoring: " + masterBatchId);
//                        }
//                    }

//                    // check if we are can go yet
//                    if (masterBatches.ContainsKey(curMasterRegId))
//                    {
//                        var masterJson = masterBatches[curMasterRegId];
//                        int expected = masterJson["highestCantonRegBatchId"].ToObject<int>();

//                        var newPageBatches = GetPageBatchWork();

//                        while (_run)
//                        {
//                            foreach (var json in newPageBatches)
//                            {
//                                //int id = json["cantonRegBatchId"].ToObject<int>();
//                                int masterId = json["cantonMasterRegBatchId"].ToObject<int>();

//                                if (!pageBatches.ContainsKey(masterId))
//                                {
//                                    pageBatches.Add(masterId, new List<JObject>());
//                                }
//                                else
//                                {
//                                    pageBatches[masterId].Add(json);
//                                }
//                            }

//                            // TODO: finish this
//                            // start counting up through the batch ids
//                            // commit all the pages

//                            if (_run)
//                            {
//                                newPageBatches = GetPageBatchWork();
//                            }
//                            else
//                            {
//                                newPageBatches = new List<JObject>();
//                            }
//                        }

//                        masterBatches.Remove(curMasterRegId);
//                        curMasterRegId++;
//                    }

//                    if (_run)
//                    {
//                        newWork = GetWork();
//                    }
//                    else
//                    {
//                        newWork = new List<JObject>();
//                    }
//                }
//            }
//            finally
//            {

//            }
//        }

//        private List<JObject> GetPageBatchWork()
//        {
//            List<JObject> work = new List<JObject>();

//            int count = 32;

//            while (count == 32)
//            {
//                Stack<Task> tasks = new Stack<Task>();
//                foreach (var message in _pageBatchQueue.GetMessages(32))
//                {
//                    work.Add(JObject.Parse(message.AsString));
//                    tasks.Push(_pageBatchQueue.DeleteMessageAsync(message));
//                }
//                count = tasks.Count;
//                Task.WaitAll(tasks.ToArray());
//            }

//            return work;
//        }

//        private List<JObject> GetWork()
//        {
//            List<JObject> work = new List<JObject>();
//            Stack<Task> tasks = new Stack<Task>();

//            foreach (var message in Queue.GetMessages(32))
//            {
//                work.Add(JObject.Parse(message.AsString));
//                tasks.Push(Queue.DeleteMessageAsync(message));
//            }

//            Task.WaitAll(tasks.ToArray());

//            return work;
//        }
//    }
//}

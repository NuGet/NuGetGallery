using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Collecting.Test
{
    public class DistinctPackageIdCollector : BatchCollector
    {
        HashSet<string> _result = new HashSet<string>();

        public DistinctPackageIdCollector(int batchSize = 200)
            : base(batchSize)
        {
        }

        public HashSet<string> Result
        {
            get { return _result; }
        }

        protected override async Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            List<Task<JObject>> tasks = new List<Task<JObject>>();

            foreach (JObject item in items)
            {
                Uri itemUri = item["url"].ToObject<Uri>();
                tasks.Add(client.GetJObjectAsync(itemUri));
            }

            await Task.WhenAll(tasks.ToArray());

            //DEBUG
            Trace.TraceInformation("{0}", client.RequestCount);

            foreach (Task<JObject> task in tasks)
            {
                JObject obj = task.Result;
                _result.Add(obj["id"].ToString().ToLowerInvariant());
            }
        }
    }
}

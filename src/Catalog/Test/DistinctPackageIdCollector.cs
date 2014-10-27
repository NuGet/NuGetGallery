using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Test
{
    public class DistinctPackageIdCollector : BatchCollector
    {
        public DistinctPackageIdCollector(Uri index, HttpMessageHandler handler = null, int batchSize = 200)
            : base(index, handler, batchSize)
        {
            Result = new HashSet<string>();
        }

        public HashSet<string> Result
        {
            get; private set;
        }

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            List<Task<JObject>> tasks = new List<Task<JObject>>();

            foreach (JObject item in items)
            {
                Uri itemUri = item["@id"].ToObject<Uri>();
                tasks.Add(client.GetJObjectAsync(itemUri));
            }

            await Task.WhenAll(tasks.ToArray());

            foreach (Task<JObject> task in tasks)
            {
                JObject obj = task.Result;
                Result.Add(obj["id"].ToString().ToLowerInvariant());
            }

            return true;
        }
    }
}

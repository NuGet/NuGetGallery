using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Collecting.Test
{
    public class PackageCollector : BatchCollector
    {
        public PackageCollector(int batchSize) : base(batchSize)
        {
            Result = new Dictionary<string, HashSet<string>>();
        }

        public IDictionary<string, HashSet<string>> Result
        {
            get; private set;
        }

        protected override async Task<bool> ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
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

                string id = obj["id"].ToString();
                string version = obj["version"].ToString();

                HashSet<string> versions;
                if (!Result.TryGetValue(id, out versions))
                {
                    versions = new HashSet<string>();
                    Result.Add(id, versions);
                }

                versions.Add(version);
            }

            return true;
        }
    }
}

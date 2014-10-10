using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Collecting.Test
{
    public class CheckLinksCollector : BatchCollector
    {
        public CheckLinksCollector(int batchSize = 200)
            : base(batchSize)
        {
        }

        protected override async Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            List<Task<string>> tasks = new List<Task<string>>();

            foreach (JObject item in items)
            {
                Uri itemUri = item["@id"].ToObject<Uri>();
                tasks.Add(client.GetStringAsync(itemUri));
            }

            await Task.WhenAll(tasks.ToArray());
        }
    }
}

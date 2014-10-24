using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Collecting.Test
{
    public class PrintCollector : BatchCollector
    {
        public PrintCollector(int batchSize)
            : base(batchSize)
        {
        }

        protected override Task<bool> ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            foreach (JObject item in items)
            {
                Console.WriteLine(item["@id"].ToString());
            }

            return Task.FromResult(true);
        }
    }
}

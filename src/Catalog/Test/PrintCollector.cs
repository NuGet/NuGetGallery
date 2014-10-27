using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Test
{
    public class PrintCollector : BatchCollector
    {
        string _name;

        public PrintCollector(string name, Uri index, HttpMessageHandler handler = null, int batchSize = 200)
            : base(index, handler, batchSize)
        {
            _name = name;
        }

        protected override Task<bool> OnProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            foreach (JObject item in items)
            {
                Console.WriteLine("{0} {1}", _name, item["@id"].ToString());
            }

            return Task.FromResult(true);
        }
    }
}

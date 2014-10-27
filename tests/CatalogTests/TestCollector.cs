using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class TestCollector : BatchCollector
    {
        string _name;

        public TestCollector(string name, Uri index, HttpMessageHandler handler = null, int batchSize = 200)
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

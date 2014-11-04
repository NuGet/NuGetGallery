using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class TestCollector : CommitCollector
    {
        string _name;

        public TestCollector(string name, Uri index, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _name = name;
        }

        protected override Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp)
        {
            foreach (JObject item in items)
            {
                Console.WriteLine("{0} {1}", _name, item["@id"].ToString());
            }

            return Task.FromResult(true);
        }
    }
}

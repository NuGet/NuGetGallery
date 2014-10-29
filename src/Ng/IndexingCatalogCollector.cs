using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ng
{
    //TODO: this is test code

    class IndexingCatalogCollector : BatchCollector
    {
        public IndexingCatalogCollector(Uri index, Lucene.Net.Store.Directory directory, Func<HttpMessageHandler> handlerFunc = null, int batchSize = 200)
            : base(index, handlerFunc, batchSize)
        {
        }

        protected override Task<bool> OnProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            foreach (JObject item in items)
            {
                Console.WriteLine("{0} {1}", item["id"], item["version"]);
            }

            return Task.FromResult(true);
        }
    }
}

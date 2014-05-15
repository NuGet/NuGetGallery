using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using VDS.RDF;

namespace Catalog.Collecting
{
    public class CollectorHttpClient : HttpClient
    {
        public CollectorHttpClient()
            : base(new WebRequestHandler { AllowPipelining = true })
        {
        }

        public Task<JObject> GetJObjectAsync(Uri address)
        {
            Task<string> task = GetStringAsync(address);
            return task.ContinueWith<JObject>((t) =>
            {
                return JObject.Parse(t.Result);
            });
        }

        public Task<IGraph> GetGraphAsync(Uri address)
        {
            Task<JObject> task = GetJObjectAsync(address);
            return task.ContinueWith<IGraph>((t) =>
            {
                return Utils.CreateGraph(t.Result);
            });
        }
    }
}

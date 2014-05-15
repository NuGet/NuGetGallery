using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VDS.RDF;

namespace Catalog.Collecting
{
    public class CollectorHttpClient : HttpClient
    {
        int _requestCount;

        public CollectorHttpClient()
            : base(new WebRequestHandler { AllowPipelining = true })
        {
            _requestCount = 0;
        }

        public int RequestCount
        {
            get { return _requestCount; }
        }

        public Task<JObject> GetJObjectAsync(Uri address)
        {
            Interlocked.Increment(ref _requestCount);

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

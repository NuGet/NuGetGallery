using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class CollectorHttpClient : HttpClient
    {
        int _requestCount;

        public CollectorHttpClient()
            : this(new WebRequestHandler { AllowPipelining = true }) { }

        public CollectorHttpClient(HttpMessageHandler handler)
            : base(handler)
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
                try
                {
                    return JObject.Parse(t.Result);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("GetJObjectAsync({0})", address), e);
                }
            });
        }

        public Task<IGraph> GetGraphAsync(Uri address)
        {
            Task<JObject> task = GetJObjectAsync(address);
            return task.ContinueWith<IGraph>((t) =>
            {
                try
                {
                    return Utils.CreateGraph(t.Result);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("GetGraphAsync({0})", address), e);
                }
            });
        }
    }
}

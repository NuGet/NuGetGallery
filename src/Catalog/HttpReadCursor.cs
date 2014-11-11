using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class HttpReadCursor : ReadCursor
    {
        Uri _address;
        Func<HttpMessageHandler> _handlerFunc;

        public HttpReadCursor(Uri address, Func<HttpMessageHandler> handlerFunc = null)
        {
            _address = address;
            _handlerFunc = handlerFunc;
        }

        public override async Task Load()
        {
            HttpMessageHandler handler = (_handlerFunc != null) ? _handlerFunc() : new WebRequestHandler { AllowPipelining = true };

            using (HttpClient client = new HttpClient(handler))
            {
                HttpResponseMessage response = await client.GetAsync(_address);

                Trace.TraceInformation("HttpReadCursor.Load {0}", response.StatusCode);

                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();

                JObject obj = JObject.Parse(json);
                Value = obj["value"].ToObject<DateTime>();
            }

            Trace.TraceInformation("HttpReadCursor.Load: {0}", this);
        }
    }
}

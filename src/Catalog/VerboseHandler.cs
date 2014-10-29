using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class VerboseHandler : DelegatingHandler
    {
        public VerboseHandler() : base(new HttpClientHandler()) { }
        public VerboseHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("HTTP {0} {1}", request.Method, request.RequestUri);
            return base.SendAsync(request, cancellationToken);
        }
    }
}

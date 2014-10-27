using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Test
{
    public class VerboseHandler : DelegatingHandler
    {
        public VerboseHandler() : base(new HttpClientHandler()) { }
        public VerboseHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Console.WriteLine(request.RequestUri);
            return base.SendAsync(request, cancellationToken);
        }
    }
}

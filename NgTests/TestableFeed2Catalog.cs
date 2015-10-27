using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ng;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests
{
    public class TestableFeed2Catalog
        : Feed2Catalog
    {
        private readonly HttpMessageHandler _handler;

        public TestableFeed2Catalog(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        protected override HttpClient CreateHttpClient(bool verbose)
        {
            return new HttpClient(_handler);
        }

        public async Task InvokeProcessFeed(string gallery, Storage catalogStorage, Storage auditingStorage, DateTime? startDate, TimeSpan timeout, int top, bool verbose, CancellationToken cancellationToken)
        {
            await ProcessFeed(gallery, catalogStorage, auditingStorage, startDate, timeout, top, verbose, cancellationToken);
        }
    }
}
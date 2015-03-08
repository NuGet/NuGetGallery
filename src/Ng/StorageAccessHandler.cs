using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ng
{
    public class StorageAccessHandler : DelegatingHandler
    {
        string _catalogBaseAddress;
        string _storageBaseAddress;

        public StorageAccessHandler(string catalogBaseAddress, string storageBaseAddress)
            : base(new HttpClientHandler())
        {
            _catalogBaseAddress = catalogBaseAddress;
            _storageBaseAddress = storageBaseAddress;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string requestUri = request.RequestUri.AbsoluteUri;

            if (requestUri.StartsWith(_catalogBaseAddress))
            {
                string newRequestUri = _storageBaseAddress + requestUri.Substring(_catalogBaseAddress.Length);
                request.RequestUri = new Uri(newRequestUri);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}

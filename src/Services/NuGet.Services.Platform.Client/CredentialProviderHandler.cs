using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services
{
    public class CredentialProviderHandler : DelegatingHandler
    {
        private IList<ICredentialProvider> _providers;

        public CredentialProviderHandler(params ICredentialProvider[] providers) : base() 
        {
            _providers = providers.ToList();
        }

        public CredentialProviderHandler(HttpClientHandler handler, params ICredentialProvider[] providers)
            : base(handler)
        {
            _providers = providers.ToList();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            // Get locally-cached credentials
            foreach (var provider in _providers)
            {
                if (provider.ApplyLocalCacheCredentials(request))
                {
                    break;
                }
            }

            // Send the request
            var response = await base.SendAsync(request, cancellationToken);
            
            // If we got a 401, ask the provider for credentials
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                bool anySucceeded = false;
                foreach (var provider in _providers)
                {
                    if (await provider.ApplyCredentials(response, request))
                    {
                        anySucceeded = true;
                        break;
                    }
                }
                if (anySucceeded)
                {
                    // Retry the request and return that one
                    return await base.SendAsync(request, cancellationToken);
                }
            }

            // Return the received response
            return response;
        }
    }
}

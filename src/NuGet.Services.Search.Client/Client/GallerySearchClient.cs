// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Search.Client 
{
    public class GallerySearchClient : SearchClient
    {
        private readonly Uri _searchUri;

        /// <summary>
        /// Create a search service client from the specified base uri and credentials.
        /// </summary>
        /// <param name="baseUri">The URL of the search service.</param>
        /// <param name="credentials">The credentials to connect to the service with</param>
        /// <param name="handlers">Handlers to apply to the request in order from first to last</param>
        public GallerySearchClient(Uri baseUri, ICredentials credentials, Action<Exception> onException, params DelegatingHandler[] handlers) 
            : base(baseUri, credentials, onException, handlers)
        {
            _searchUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
        }

        public override async Task<ServiceResponse<JObject>> GetDiagnostics()
        {
            return new ServiceResponse<JObject>(
                await _retryingHttpClientWrapper.GetAsync(new Uri[] { GetDiagnosticsUri() }));
        }

        public Uri GetDiagnosticsUri()
        {
            return AppendPathToUri(_searchUri, "search/diag");
        }

        public override async Task<IEnumerable<Uri>> GetEndpoints()
        {
            await Task.Yield();
            return new Uri[] { _searchUri };
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.Search.Client
{
    public class AutoCompleteSearchClient
    {
        private readonly HttpClient _client;

        /// <summary>
        /// Constructs a wrapper for the http client targeted to serve autocomplete requests.
        /// </summary>
        /// <param name="httpClient">Http client.</param>
        public AutoCompleteSearchClient(HttpClient httpClient)
        {
            _client = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<string> GetStringAsync(string queryString)
        {
            var uri = AppendAutocompleteUriPath(queryString);
            return await _client.GetStringAsync(uri);
        }

        public Uri AppendAutocompleteUriPath(string queryString)
        {
            return _client.BaseAddress.AppendPathToUri("autocomplete", queryString.TrimStart('?'));
        }
    }
}

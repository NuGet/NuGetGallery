// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGetGallery.Infrastructure
{
    public class HttpClientWrapper : IHttpClientWrapper
    {
        HttpClient _httpClient;

        public HttpClientWrapper(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw (new ArgumentNullException(nameof(httpClient)));
        }

        public Uri BaseAddress => _httpClient.BaseAddress;

        public Task<HttpResponseMessage> GetAsync(Uri uri)
        {
            return _httpClient.GetAsync(uri);
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using NuGetGallery.Infrastructure.Search;
using Polly;

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

        public async Task<HttpResponseMessage> GetAsync(Uri uri)
        {
            // Add the request Uri to the  Polly context to be used for logging.
            // https://docs.microsoft.com/en-us/dotnet/api/polly.httprequestmessageextensions.setpolicyexecutioncontext?view=aspnetcore-2.2
            // http://www.thepollyproject.org/2017/05/04/putting-the-context-into-polly/ 
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                var context = new Context();
                context.Add(SearchClientPolicies.ContextKey_RequestUri, uri.AbsoluteUri);
                request.SetPolicyExecutionContext(context);
                return await _httpClient.SendAsync(request);
            }
        }
    }
}
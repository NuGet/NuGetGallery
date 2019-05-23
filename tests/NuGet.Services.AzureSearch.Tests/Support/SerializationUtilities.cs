// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch.Support
{
    public class SerializationUtilities
    {
        public static async Task<string> SerializeToJsonAsync<T>(T obj) where T : class
        {
            using (var testHandler = new TestHttpClientHandler())
            using (var serviceClient = new SearchServiceClient(
                "unit-test-service",
                new SearchCredentials("unit-test-api-key"),
                testHandler))
            {
                var indexClient = serviceClient.Indexes.GetClient("unit-test-index");
                await indexClient.Documents.IndexAsync(IndexBatch.Upload(new[] { obj }));
                return testHandler.LastRequestBody;
            }
        }

        private class TestHttpClientHandler : HttpClientHandler
        {
            public string LastRequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Content != null)
                {
                    LastRequestBody = await request.Content.ReadAsStringAsync();
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(LastRequestBody ?? string.Empty),
                };
            }
        }
    }
}

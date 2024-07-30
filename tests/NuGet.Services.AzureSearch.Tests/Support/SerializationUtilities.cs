// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core.Pipeline;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace NuGet.Services.AzureSearch.Support
{
    public class SerializationUtilities
    {
        public static async Task<string> SerializeToJsonAsync<T>(T obj) where T : class
        {
            using (var testHandler = new TestHttpClientHandler())
            {
                var indexClient = new SearchClient(
                   new Uri("https://unit-test-service.test/"),
                   "unit-test-index",
                   new AzureKeyCredential("unit-test-api-key"),
                   new SearchClientOptions
                   {
                       Transport = new HttpClientTransport(testHandler),
                       Serializer = IndexBuilder.GetJsonSerializer(),
                   });
                await indexClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(new[] { obj }));
                var rawJson = testHandler.LastRequestBody;
                var json = JsonDocument.Parse(rawJson);
                using (var stream = new MemoryStream())
                {
                    var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                    json.WriteTo(writer);
                    writer.Flush();
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
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

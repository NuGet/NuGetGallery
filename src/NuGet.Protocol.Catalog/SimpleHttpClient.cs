// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    public class SimpleHttpClient : ISimpleHttpClient
    {
        private static readonly JsonSerializer JsonSerializer = NuGetJsonSerialization.Serializer;
        private readonly HttpClient _httpClient;
        private readonly ILogger<SimpleHttpClient> _logger;

        public SimpleHttpClient(HttpClient httpClient, ILogger<SimpleHttpClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<byte[]> GetByteArrayAsync(string requestUri)
        {
            return await _httpClient.GetByteArrayAsync(requestUri);
        }

        public T DeserializeBytes<T>(byte[] jsonBytes)
        {
            using (var stream = new MemoryStream(jsonBytes))
            using (var textReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return JsonSerializer.Deserialize<T>(jsonReader);
            }
        }

        public async Task<ResponseAndResult<T>> DeserializeUrlAsync<T>(string documentUrl)
        {
            _logger.LogDebug("Downloading {documentUrl} as a stream.", documentUrl);

            using (var response = await _httpClient.GetAsync(
                documentUrl,
                HttpCompletionOption.ResponseHeadersRead))
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return new ResponseAndResult<T>(
                        HttpMethod.Get,
                        documentUrl,
                        response.StatusCode,
                        response.ReasonPhrase,
                        hasResult: false,
                        result: default(T));
                }

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var textReader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(textReader))
                {
                    return new ResponseAndResult<T>(
                        HttpMethod.Get,
                        documentUrl,
                        response.StatusCode,
                        response.ReasonPhrase,
                        hasResult: true,
                        result: JsonSerializer.Deserialize<T>(jsonReader));
                }
            }
        }
    }
}

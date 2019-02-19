// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGetGallery;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Search.Client
{
    public class ResilientSearchHttpClient : IResilientSearchClient
    {
        private readonly IEnumerable<ISearchHttpClient> _httpClients;
        private readonly ILogger _logger;
        private readonly ITelemetryService _telemetryService;

        public ResilientSearchHttpClient(IEnumerable<ISearchHttpClient> searchClients, ILogger<ResilientSearchHttpClient> logger, ITelemetryService telemetryService)
        {
            _httpClients = searchClients ?? throw new ArgumentNullException(nameof(logger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HttpResponseMessage> GetAsync(string path, string queryString)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Uri searchUri = null; 
            foreach(var client in _httpClients)
            {
                searchUri = client.BaseAddress.AppendPathToUri(path, queryString);
                _logger.LogInformation("Sent GetAsync request for {Url}", searchUri.AbsoluteUri);
                var result = await client.GetAsync(searchUri);
                if (result.IsSuccessStatusCode)
                {
                    sw.Stop();
                    _telemetryService.TrackMetricForSearchExecutionDuration(searchUri.AbsoluteUri, sw.Elapsed, result.StatusCode);
                    return result;
                }
            }
            sw.Stop();
            _telemetryService.TrackMetricForSearchExecutionDuration(searchUri?.AbsoluteUri??string.Empty, sw.Elapsed, HttpStatusCode.ServiceUnavailable);
            return GetSearchServiceNotAvailableHttpResponseMessage(path, queryString);
        }

        public async Task<string> GetStringAsync(string path, string queryString)
        {
            var result = await GetAsync(path, queryString);
            return await result.Content.ReadAsStringAsync();
        }

        private static HttpResponseMessage GetSearchServiceNotAvailableHttpResponseMessage(string path, string queryString)
        {
            var content = new JObject( 
                            new JProperty("data", 
                                new JObject(new JProperty("message", Strings.SearchServiceIsNotAvailable))));

            return new HttpResponseMessage()
            {
                Content = new StringContent(content.ToString(), Encoding.UTF8, CoreConstants.TextContentType),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, $"{path}/{queryString}"),
                StatusCode = HttpStatusCode.ServiceUnavailable
            };
        }
    }
}
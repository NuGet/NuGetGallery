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
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Infrastructure.Search
{
    public class ResilientSearchHttpClient : IResilientSearchClient
    {
        private readonly IEnumerable<IHttpClientWrapper> _httpClients;
        private readonly ITelemetryService _telemetryService;

        public ResilientSearchHttpClient(IEnumerable<IHttpClientWrapper> searchClients, ITelemetryService telemetryService)
        {
            _httpClients = searchClients ?? throw new ArgumentNullException(nameof(searchClients));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        public async Task<HttpResponseMessage> GetAsync(string path, string queryString)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Uri searchUri = null; 
            foreach(var client in _httpClients)
            {
                searchUri = client.BaseAddress.AppendPathToUri(path, queryString);
                var result = await client.GetAsync(searchUri);
                if (result.IsSuccessStatusCode)
                {
                    sw.Stop();
                    _telemetryService.TrackMetricForSearchExecutionDuration(searchUri.AbsoluteUri, sw.Elapsed, success: true);
                    return result;
                }
            }
            sw.Stop();
            _telemetryService.TrackMetricForSearchExecutionDuration(searchUri?.AbsoluteUri??string.Empty, sw.Elapsed, success: false);
            return GetSearchServiceNotAvailableHttpResponseMessage(path, queryString);
        }

        private static HttpResponseMessage GetSearchServiceNotAvailableHttpResponseMessage(string path, string queryString)
        {
            var content = new JObject( 
                            new JProperty("data", 
                                new JObject(new JProperty("message", Strings.SearchServiceIsNotAvailable))));

            return new HttpResponseMessage()
            {
                Content = new StringContent(content.ToString(), Encoding.UTF8, CoreConstants.JsonContentType),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, $"{path}/{queryString}"),
                StatusCode = HttpStatusCode.ServiceUnavailable
            };
        }
    }
}
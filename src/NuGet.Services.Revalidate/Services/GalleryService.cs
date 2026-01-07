// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace NuGet.Services.Revalidate
{
    public class GalleryService : IGalleryService
    {
        private static readonly string GalleryEventsQuery = HttpUtility.UrlPathEncode(
            "customMetrics | " +
            "where name == \"PackagePush\" or name == \"PackageUnlisted\" or name == \"PackageListed\" | " +
            "summarize sum(value)");

        private readonly HttpClient _httpClient;
        private readonly ApplicationInsightsConfiguration _appInsightsConfig;
        private readonly ILogger<GalleryService> _logger;

        public GalleryService(
            HttpClient httpClient,
            ApplicationInsightsConfiguration appInsightsConfig,
            ILogger<GalleryService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _appInsightsConfig = appInsightsConfig ?? throw new ArgumentNullException(nameof(appInsightsConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int> CountEventsInPastHourAsync()
        {
            try
            {
                using (var request = new HttpRequestMessage())
                {
                    request.RequestUri = new Uri($"https://api.applicationinsights.io/v1/apps/{_appInsightsConfig.AppId}/query?timespan=PT1H&query={GalleryEventsQuery}");
                    request.Method = HttpMethod.Get;

                    request.Headers.Add("x-api-key", _appInsightsConfig.ApiKey);

                    using (var response = await _httpClient.SendAsync(request))
                    {
                        response.EnsureSuccessStatusCode();

                        var json = await response.Content.ReadAsStringAsync();
                        var data = JsonConvert.DeserializeObject<QueryResult>(json);

                        if (data?.Tables?.Length != 1 ||
                            data.Tables[0]?.Rows?.Length != 1 ||
                            data.Tables[0].Rows[0]?.Length != 1)
                        {
                            throw new InvalidOperationException("Malformed response content");
                        }

                        // Get the first row's first column's value.
                        return data.Tables[0].Rows[0][0];
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "Exception thrown when getting the Gallery's package event rate.");

                throw new InvalidOperationException("Exception thrown when getting the Gallery's package event rate.", e);
            }
        }

        private class QueryResult
        {
            public QueryTable[] Tables { get; set; }
        }

        private class QueryTable
        {
            public int[][] Rows { get; set; }
        }
    }
}

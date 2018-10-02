// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Autofac;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Monitoring.RebootSearchInstance
{
    public class FeedClient : IFeedClient
    {
        private const string FeedRelativeUrl =
            "api/v2/Packages?" +
            "$filter={0}%20ne%20null&" +
            "$top=1&" +
            "$orderby={0}%20desc&" +
            "$select={0}";

        private static readonly IEnumerable<string> TimeStampProperties = new string[]
        {
            "LastEdited",
        };

        private readonly HttpClient _httpClient;
        private readonly IOptionsSnapshot<MonitorConfiguration> _configuration;
        private readonly ILogger<FeedClient> _logger;

        public FeedClient(HttpClient httpClient, IOptionsSnapshot<MonitorConfiguration> configuration, ILogger<FeedClient> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<DateTimeOffset> GetLatestFeedTimeStampAsync()
        {
            var tasks = TimeStampProperties
                .Select(GetLatestFeedTimeStampAsync)
                .ToList();

            await Task.WhenAll(tasks);

            return tasks.Max(t => t.Result);
        }

        private async Task<DateTimeOffset> GetLatestFeedTimeStampAsync(string propertyName)
        {
            var galleryBaseUrl = _configuration.Value.FeedUrl;
            var feedUrl = new Uri(galleryBaseUrl).GetLeftPart(UriPartial.Authority);
            var url = $"{feedUrl}/{string.Format(FeedRelativeUrl, propertyName)}";
            using (var stream = await _httpClient.GetStreamAsync(url))
            {
                var packagesResultObject = XDocument.Load(stream);
                var propertyValue = DateTimeOffset.Parse(
                    packagesResultObject.Descendants()
                        .Where(a => a.Name.LocalName == propertyName)
                        .FirstOrDefault().Value,
                    formatProvider: null,
                    styles: DateTimeStyles.AssumeUniversal);
                _logger.LogInformation("Feed at {GalleryBaseUrl} has {TimeStampPropertyName} cursor at {TimeStampPropertyValue}", galleryBaseUrl, propertyName, propertyValue);
                return propertyValue;
            }
        }
    }
}

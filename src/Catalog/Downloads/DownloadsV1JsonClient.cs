// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog
{
    public class DownloadsV1JsonClient : IDownloadsV1JsonClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DownloadsV1JsonClient> _logger;

        public DownloadsV1JsonClient(HttpClient httpClient, ILogger<DownloadsV1JsonClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DownloadData> ReadAsync(string url)
        {
            var downloadData = new DownloadData();
            await ReadAsync(url, downloadData.SetDownloadCount);
            return downloadData;
        }

        public async Task ReadAsync(string url, AddDownloadCount addCount)
        {
            var stopwatch = Stopwatch.StartNew();
            var packageCount = 0;
            
            await Retry.IncrementalAsync(
                async () =>
                {
                    _logger.LogInformation("Attempting to download {Url}", url);
                    using (var response = await _httpClient.GetAsync(url))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var textReader = new StreamReader(await response.Content.ReadAsStreamAsync()))
                        using (var jsonReader = new JsonTextReader(textReader))
                        {
                            DownloadsV1Reader.Load(jsonReader, (id, version, count) =>
                            {
                                packageCount++;
                                addCount(id, version, count);
                            });
                        }
                    }
                },
                ex => ex is HttpRequestException,
                maxRetries: 5,
                initialWaitInterval: TimeSpan.Zero,
                waitIncrement: TimeSpan.FromSeconds(20));

            stopwatch.Stop();

            _logger.LogInformation("Got information about {RecordCount} packages from downloads.v1.json in {DurationSeconds} seconds",
                packageCount,
                stopwatch.Elapsed.TotalSeconds);
        }
    }
}

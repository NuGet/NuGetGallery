// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog
{
    public class DownloadsV1JsonClient : IDownloadsV1JsonClient
    {
        private readonly BlobClient _blobClient;
        private readonly ILogger<DownloadsV1JsonClient> _logger;

        public DownloadsV1JsonClient(BlobClient blobClient, ILogger<DownloadsV1JsonClient> logger)
        {
            _blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DownloadData> ReadAsync()
        {
            var downloadData = new DownloadData();
            await ReadAsync(downloadData.SetDownloadCount);
            return downloadData;
        }

        public async Task ReadAsync(AddDownloadCount addCount)
        {
            var stopwatch = Stopwatch.StartNew();
            var packageCount = 0;

            await Retry.IncrementalAsync(
                async () =>
                {
                    _logger.LogInformation("Attempting to download {Url}", _blobClient.Uri.GetLeftPart(UriPartial.Path));
                    using (BlobDownloadStreamingResult result = await _blobClient.DownloadStreamingAsync())
                    using (var textReader = new StreamReader(result.Content))
                    using (var jsonReader = new JsonTextReader(textReader))
                    {
                        DownloadsV1Reader.Load(jsonReader, (id, version, count) =>
                        {
                            packageCount++;
                            addCount(id, version, count);
                        });
                    }
                },
                ex => ex is RequestFailedException,
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

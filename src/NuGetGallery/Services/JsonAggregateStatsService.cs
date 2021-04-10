// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Newtonsoft.Json;
using NuGetGallery.Services;

namespace NuGetGallery
{
    public class JsonAggregateStatsService : IAggregateStatsService
    {
        private readonly Func<ICloudBlobClient> _cloudBlobClientFactory;

        public JsonAggregateStatsService(Func<ICloudBlobClient> cloudBlobClientFactory)
        {
            _cloudBlobClientFactory = cloudBlobClientFactory ?? throw new ArgumentNullException(nameof(cloudBlobClientFactory));
        }

        public async Task<AggregateStats> GetAggregateStats()
        {
            var blobClient = _cloudBlobClientFactory();

            var container = blobClient.GetContainerReference("nuget-cdnstats");
            var blob = container.GetBlobReference("stats-totals.json");

            string totals = await blob.DownloadTextIfExistsAsync();
            if (totals == null)
            {
                throw new StatisticsReportNotFoundException();
            }
            var json = JsonConvert.DeserializeObject<AggregateStats>(totals);

            return json;
        }
    }
}

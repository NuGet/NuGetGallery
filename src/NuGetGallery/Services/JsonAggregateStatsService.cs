// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Newtonsoft.Json;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class JsonAggregateStatsService : IAggregateStatsService
    {
        private IGalleryConfigurationService _configService;

        public JsonAggregateStatsService(IGalleryConfigurationService configService)
        {
            _configService = configService;
        }

        public async Task<AggregateStats> GetAggregateStats()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse((await _configService.GetCurrent()).AzureStorageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            if ((await _configService.GetCurrent()).AzureStorageReadAccessGeoRedundant)
            {
                blobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
            }

            CloudBlobContainer container = blobClient.GetContainerReference("nuget-cdnstats");
            CloudBlockBlob blob = container.GetBlockBlobReference("stats-totals.json");

            //Check if the report blob is present before processing it.
            if (!blob.Exists())
            {
                throw new StatisticsReportNotFoundException();
            }

            string totals = await blob.DownloadTextAsync();
            var json = JsonConvert.DeserializeObject<AggregateStats>(totals);

            return json;
        }
    }
}

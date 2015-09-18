// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public class JsonAggregateStatsService : IAggregateStatsService
    {
        private readonly string _connectionString;
        private readonly bool _readAccessGeoRedundant;

        public JsonAggregateStatsService(string connectionString, bool readAccessGeoRedundant)
        {
            _connectionString = connectionString;
            _readAccessGeoRedundant = readAccessGeoRedundant;
        }

        public async Task<AggregateStats> GetAggregateStats()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            if (_readAccessGeoRedundant)
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

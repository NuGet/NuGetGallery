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
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IBlobStorageConfiguration _primaryStorageConfiguration;
        private readonly IBlobStorageConfiguration _alternateBlobStorageConfiguration;

        public JsonAggregateStatsService(
            IFeatureFlagService featureFlagService,
            IBlobStorageConfiguration primaryBlobStorageConfiguration,
            IBlobStorageConfiguration alternateBlobStorageConfiguration)
        {
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _primaryStorageConfiguration = primaryBlobStorageConfiguration ?? throw new ArgumentNullException(nameof(primaryBlobStorageConfiguration));
            _alternateBlobStorageConfiguration = alternateBlobStorageConfiguration;
        }

        public async Task<AggregateStats> GetAggregateStats()
        {
            var connectionString = _primaryStorageConfiguration.ConnectionString;
            var readAccessGeoRedundant = _primaryStorageConfiguration.ReadAccessGeoRedundant;

            if (_alternateBlobStorageConfiguration != null && _featureFlagService.IsAlternateStatisticsSourceEnabled())
            {
                connectionString = _alternateBlobStorageConfiguration.ConnectionString;
                readAccessGeoRedundant = _alternateBlobStorageConfiguration.ReadAccessGeoRedundant;
            }

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            if (readAccessGeoRedundant)
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

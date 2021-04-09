// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGetGallery.Services;

namespace NuGetGallery
{
    public class CloudReportService : IReportService
    {
        private const string _statsContainerName = "nuget-cdnstats";
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IBlobStorageConfiguration _primaryStorageConfiguration;
        private readonly IBlobStorageConfiguration _alternateBlobStorageConfiguration;

        public CloudReportService(
            IFeatureFlagService featureFlagService,
            IBlobStorageConfiguration primaryBlobStorageConfiguration,
            IBlobStorageConfiguration alternateBlobStorageConfiguration)
        {
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _primaryStorageConfiguration = primaryBlobStorageConfiguration ?? throw new ArgumentNullException(nameof(primaryBlobStorageConfiguration));
            _alternateBlobStorageConfiguration = alternateBlobStorageConfiguration;
        }

        public async Task<StatisticsReport> Load(string reportName)
        {
            // In NuGet we always use lowercase names for all blobs in Azure Storage
            reportName = reportName.ToLowerInvariant();

            var container = GetCloudBlobContainer();
            var blob = container.GetBlockBlobReference(reportName);

            // Check if the report blob is present before processing it.
            if (!blob.Exists())
            {
                throw new StatisticsReportNotFoundException();
            }

            await blob.FetchAttributesAsync();
            string content = await blob.DownloadTextAsync();

            return new StatisticsReport(content, blob.Properties.LastModified?.UtcDateTime);
        }

        private CloudBlobContainer GetCloudBlobContainer()
        {
            var connectionString = _primaryStorageConfiguration.ConnectionString;
            var readAccessGeoRedundant = _primaryStorageConfiguration.ReadAccessGeoRedundant;

            if(_alternateBlobStorageConfiguration != null && _featureFlagService.IsAlternateStatisticsSourceEnabled())
            {
                connectionString = _alternateBlobStorageConfiguration.ConnectionString;
                readAccessGeoRedundant = _alternateBlobStorageConfiguration.ReadAccessGeoRedundant;
            }

            var storageAccount = CloudStorageAccount.Parse(connectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();

            if (readAccessGeoRedundant)
            {
                blobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
            }

            return blobClient.GetContainerReference(_statsContainerName);
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class CloudReportService : IReportService
    {
        private const string _statsContainerName = "nuget-cdnstats";
        private IGalleryConfigurationService _configService;

        public CloudReportService(IGalleryConfigurationService configService)
        {
            _configService = configService;
        }

        public async Task<StatisticsReport> Load(string reportName)
        {
            // In NuGet we always use lowercase names for all blobs in Azure Storage
            reportName = reportName.ToLowerInvariant();

            var storageAccount = CloudStorageAccount.Parse((await _configService.GetCurrent()).AzureStorageConnectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();

            if ((await _configService.GetCurrent()).AzureStorageReadAccessGeoRedundant)
            {
                blobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
            }

            var container = blobClient.GetContainerReference(_statsContainerName);
            var blob = container.GetBlockBlobReference(reportName);

            // Check if the report blob is present before processing it.
            if(!blob.Exists())
            {
                throw new StatisticsReportNotFoundException();
            }

            await blob.FetchAttributesAsync();
            string content = await blob.DownloadTextAsync();

            return new StatisticsReport(content, blob.Properties.LastModified?.UtcDateTime);
        }
    }
}
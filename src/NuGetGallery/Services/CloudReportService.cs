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
        private readonly Func<ICloudBlobClient> _cloudBlobClientFactory;

        public CloudReportService(Func<ICloudBlobClient> cloudBlobClientFactory)
        {
            _cloudBlobClientFactory = cloudBlobClientFactory ?? throw new ArgumentNullException(nameof(cloudBlobClientFactory));
        }

        public async Task<StatisticsReport> Load(string reportName)
        {
            // In NuGet we always use lowercase names for all blobs in Azure Storage
            reportName = reportName.ToLowerInvariant();

            var container = GetCloudBlobContainer();
            var blob = container.GetBlobReference(reportName);

            // Check if the report blob is present before processing it.
            if (!await blob.FetchAttributesIfExistsAsync())
            {
                throw new StatisticsReportNotFoundException();
            }
            string content = await blob.DownloadTextIfExistsAsync();

            return new StatisticsReport(content, blob.Properties.LastModified?.UtcDateTime);
        }

        private ICloudBlobContainer GetCloudBlobContainer()
        {
            var blobClient = _cloudBlobClientFactory();

            return blobClient.GetContainerReference(_statsContainerName);
        }
    }
}
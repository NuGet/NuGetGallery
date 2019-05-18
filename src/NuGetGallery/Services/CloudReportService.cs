// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace NuGetGallery
{
    public class CloudReportService : IReportService
    {
        private const string _statsContainerName = "nuget-cdnstats";
        private readonly string _connectionString;
        private readonly bool _readAccessGeoRedundant;

        public CloudReportService(string connectionString, bool readAccessGeoRedundant)
        {
            _connectionString = connectionString;
            _readAccessGeoRedundant = readAccessGeoRedundant;
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
            var storageAccount = CloudStorageAccount.Parse(_connectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();

            if (_readAccessGeoRedundant)
            {
                blobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
            }

            return blobClient.GetContainerReference(_statsContainerName);
        }
    }
}
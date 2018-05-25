// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace NuGetGallery
{
    public class CloudReportService : IReportService
    {
        private readonly string _connectionString;
        private readonly bool _readAccessGeoRedundant;

        public CloudReportService(string connectionString, bool readAccessGeoRedundant)
        {
            _connectionString = connectionString;
            _readAccessGeoRedundant = readAccessGeoRedundant;
        }

        public IReportContainer GetContainer(string containerName)
        {
            return new Container(GetCloudBlobContainer(containerName));
        }

        private CloudBlobContainer GetCloudBlobContainer(string containerName)
        {
            var storageAccount = CloudStorageAccount.Parse(_connectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();

            if (_readAccessGeoRedundant)
            {
                blobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
            }

            return blobClient.GetContainerReference(containerName);
        }

        private class Container : IReportContainer
        {
            private readonly CloudBlobContainer _container;

            public Container(CloudBlobContainer container)
            {
                _container = container ?? throw new ArgumentNullException(nameof(container));
            }

            public Task<bool> IsAvailableAsync() => _container.ExistsAsync();

            public async Task<ReportBlob> Load(string reportName)
            {
                // In NuGet we always use lowercase names for all blobs in Azure Storage
                reportName = reportName.ToLowerInvariant();
                var blob = _container.GetBlockBlobReference(reportName);

                // Check if the report blob is present before processing it.
                if (!blob.Exists())
                {
                    throw new ReportNotFoundException();
                }

                await blob.FetchAttributesAsync();
                string content = await blob.DownloadTextAsync();

                return new ReportBlob(content, blob.Properties.LastModified?.UtcDateTime);
            }
        }
    }
}
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
        public CloudReportService(string connectionString, bool readAccessGeoRedundant)
        {
            ConnectionString = connectionString;
            ReadAccessGeoRedundant = readAccessGeoRedundant;
        }

        private string ConnectionString { get; }
        private bool ReadAccessGeoRedundant { get; }

        public IReportContainer GetContainer(string containerName)
        {
            return new Container(this, containerName);
        }

        private class Container : IReportContainer
        {
            private readonly CloudReportService _parent;
            private readonly string _name;

            public Container(CloudReportService parent, string name)
            {
                _parent = parent ?? throw new ArgumentNullException(nameof(parent));
                // NOTE: Do not call GetCloudBlobContainer() and store the result in a field here.
                // We want to reinitialize the container on each method call. See
                // https://github.com/NuGet/NuGetGallery/pull/5841#discussion_r193898969
                _name = name;
            }

            public Task<bool> IsAvailableAsync()
            {
                var container = GetCloudBlobContainer();
                return container.ExistsAsync();
            }

            public async Task<ReportBlob> Load(string reportName)
            {
                // In NuGet we always use lowercase names for all blobs in Azure Storage
                reportName = reportName.ToLowerInvariant();

                var container = GetCloudBlobContainer();
                var blob = container.GetBlockBlobReference(reportName);

                // Check if the report blob is present before processing it.
                if (!blob.Exists())
                {
                    throw new ReportNotFoundException();
                }

                await blob.FetchAttributesAsync();
                string content = await blob.DownloadTextAsync();

                return new ReportBlob(content, blob.Properties.LastModified?.UtcDateTime);
            }

            private CloudBlobContainer GetCloudBlobContainer()
            {
                var storageAccount = CloudStorageAccount.Parse(_parent.ConnectionString);
                var blobClient = storageAccount.CreateCloudBlobClient();

                if (_parent.ReadAccessGeoRedundant)
                {
                    blobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
                }

                return blobClient.GetContainerReference(_name);
            }
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace Stats.CreateAzureCdnDownloadCountReports
{
    public abstract class ReportBase
    {
        protected readonly CloudStorageAccount CloudStorageAccount;
        protected readonly string StatisticsContainerName;
        protected readonly SqlConnectionStringBuilder StatisticsDatabase;
        protected SqlConnectionStringBuilder GalleryDatabase;

        public ReportBase(CloudStorageAccount cloudStorageAccount, string statisticsContainerName, SqlConnectionStringBuilder statisticsDatabase, SqlConnectionStringBuilder galleryDatabase)
        {
            CloudStorageAccount = cloudStorageAccount;
            StatisticsContainerName = statisticsContainerName;
            StatisticsDatabase = statisticsDatabase;
            GalleryDatabase = galleryDatabase;
        }

        protected async Task<CloudBlobContainer> GetBlobContainer()
        {
            // construct a cloud blob client for the configured storage account
            var cloudBlobClient = CloudStorageAccount.CreateCloudBlobClient();
            cloudBlobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 5);

            // get the target blob container (to store the generated reports)
            var targetBlobContainer = cloudBlobClient.GetContainerReference(StatisticsContainerName);
            await targetBlobContainer.CreateIfNotExistsAsync();
            var blobContainerPermissions = new BlobContainerPermissions();
            blobContainerPermissions.PublicAccess = BlobContainerPublicAccessType.Blob;
            await targetBlobContainer.SetPermissionsAsync(blobContainerPermissions);
            return targetBlobContainer;
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGet.Services.Sql;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public abstract class ReportBase
    {
        protected ILogger _logger;

        protected readonly IReadOnlyCollection<StorageContainerTarget> Targets;

        protected readonly ISqlConnectionFactory StatisticsDbConnectionFactory;

        protected ISqlConnectionFactory GalleryDbConnectionFactory;

        protected ReportBase(
            ILogger<ReportBase> logger,
            IEnumerable<StorageContainerTarget> targets,
            ISqlConnectionFactory statisticsDbConnectionFactory,
            ISqlConnectionFactory galleryDbConnectionFactory)
        {
            _logger = logger;
            Targets = targets.ToList().AsReadOnly();
            StatisticsDbConnectionFactory = statisticsDbConnectionFactory;
            GalleryDbConnectionFactory = galleryDbConnectionFactory;
        }

        protected async Task<CloudBlobContainer> GetBlobContainer(StorageContainerTarget target)
        {
            // construct a cloud blob client for the configured storage account
            var cloudBlobClient = target.StorageAccount.CreateCloudBlobClient();
            cloudBlobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 5);

            // get the target blob container (to store the generated reports)
            var targetBlobContainer = cloudBlobClient.GetContainerReference(target.ContainerName);
            await targetBlobContainer.CreateIfNotExistsAsync();
            var blobContainerPermissions = new BlobContainerPermissions();
            blobContainerPermissions.PublicAccess = BlobContainerPublicAccessType.Blob;
            await targetBlobContainer.SetPermissionsAsync(blobContainerPermissions);
            return targetBlobContainer;
        }
    }
}
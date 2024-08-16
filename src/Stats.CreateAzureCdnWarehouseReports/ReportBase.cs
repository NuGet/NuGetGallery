// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public abstract class ReportBase
    {
        protected ILogger _logger;

        protected readonly IReadOnlyCollection<StorageContainerTarget> Targets;

        protected readonly Func<Task<SqlConnection>> OpenStatisticsSqlConnectionAsync;

        protected Func<Task<SqlConnection>> OpenGallerySqlConnectionAsync;

        protected readonly TimeSpan CommandTimeoutSeconds;

        protected ReportBase(
            ILogger<ReportBase> logger,
            IEnumerable<StorageContainerTarget> targets,
            Func<Task<SqlConnection>> openStatisticsSqlConnectionAsync,
            Func<Task<SqlConnection>> openGallerySqlConnectionAsync,
            int commandTimeoutSeconds)
        {
            _logger = logger;
            Targets = targets.ToList().AsReadOnly();
            OpenStatisticsSqlConnectionAsync = openStatisticsSqlConnectionAsync;
            OpenGallerySqlConnectionAsync = openGallerySqlConnectionAsync;
            CommandTimeoutSeconds = TimeSpan.FromSeconds(commandTimeoutSeconds);
        }

        protected async Task<CloudBlobContainer> GetBlobContainer(StorageContainerTarget target)
        {
            // construct a cloud blob client for the configured storage account
            var cloudBlobClient = target.StorageAccount.CreateCloudBlobClient();
            cloudBlobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 5);

            // get the target blob container (to store the generated reports)
            var targetBlobContainer = cloudBlobClient.GetContainerReference(target.ContainerName);
            await targetBlobContainer.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Blob, options: null, operationContext: null);
            return targetBlobContainer;
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public abstract class ReportBase
    {
        protected readonly ILogger _logger;

        protected readonly IReadOnlyCollection<StorageContainerTarget> _targets;

        protected readonly Func<Task<SqlConnection>> _openGallerySqlConnectionAsync;

        protected readonly TimeSpan _commandTimeoutSeconds;

        protected ReportBase(
            ILogger<ReportBase> logger,
            IEnumerable<StorageContainerTarget> targets,
            Func<Task<SqlConnection>> openGallerySqlConnectionAsync,
            int commandTimeoutSeconds)
        {
            _logger = logger;
            _targets = targets.ToList().AsReadOnly();
            _openGallerySqlConnectionAsync = openGallerySqlConnectionAsync;
            _commandTimeoutSeconds = TimeSpan.FromSeconds(commandTimeoutSeconds);
        }

        protected async Task<BlobContainerClient> GetBlobContainer(StorageContainerTarget target)
        {
            // get the target blob container (to store the generated reports)
            var targetBlobContainer = target.StorageAccount.GetBlobContainerClient(target.ContainerName);
            await targetBlobContainer.CreateIfNotExistsAsync(PublicAccessType.None);
            return targetBlobContainer;
        }
    }
}

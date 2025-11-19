// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class GalleryTotalsReport
        : ReportBase
    {
        private const string GalleryQuery = @"SELECT
                    (SELECT COUNT(DISTINCT [PackageRegistrationKey]) FROM Packages p WITH (NOLOCK)
                            WHERE p.Listed = 1 AND p.Deleted = 0) AS UniquePackages,
                    (SELECT COUNT([Key]) FROM Packages WITH (NOLOCK) WHERE Listed = 1 AND Deleted = 0) AS TotalPackages";

        public GalleryTotalsReport(
            ILogger<GalleryTotalsReport> logger,
            ICollection<StorageContainerTarget> targets,
            Func<Task<SqlConnection>> openGallerySqlConnectionAsync,
            int commandTimeoutSeconds)
            : base(
                  logger,
                  targets,
                  openGallerySqlConnectionAsync: openGallerySqlConnectionAsync,
                  commandTimeoutSeconds: commandTimeoutSeconds)
        {
        }

        public async Task Run()
        {
            // gather package numbers from gallery database
            GalleryTotalsData totalsData;

            using (var connection = await _openGallerySqlConnectionAsync())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                _logger.LogInformation("Gathering Gallery Totals from {GalleryDataSource}/{GalleryInitialCatalog}...",
                    connection.DataSource, connection.Database);

                totalsData = (await connection.QueryWithRetryAsync<GalleryTotalsData>(
                    GalleryQuery, commandType: CommandType.Text, transaction: transaction)).First();
            }

            _logger.LogInformation("Total packages: {TotalPackagesCount}", totalsData.TotalPackages);
            _logger.LogInformation("Unique packages: {UniquePackagesCount}", totalsData.UniquePackages);

            // write to blob
            totalsData.LastUpdateDateUtc = DateTime.UtcNow;

            var reportText = JsonConvert.SerializeObject(totalsData);

            foreach (var storageContainerTarget in _targets)
            {
                try
                {
                    var targetBlobContainer = await GetBlobContainer(storageContainerTarget);
                    var blob = targetBlobContainer.GetBlobClient(ReportNames.GalleryTotals + ReportNames.Extension);
                    _logger.LogInformation("Writing report to {ReportUri}", blob.Uri.GetLeftPart(UriPartial.Path));
                    var content = new BinaryData(reportText);
                    var options = new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
                    };
                    await blob.UploadAsync(content, options);
                    _logger.LogInformation("Wrote report to {ReportUri}", blob.Uri.GetLeftPart(UriPartial.Path));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing report to storage account {StorageAccount}, container {ReportContainer}. {Exception}",
                        storageContainerTarget.StorageAccount.AccountName,
                        storageContainerTarget.ContainerName,
                        ex);
                }
            }
        }
    }
}

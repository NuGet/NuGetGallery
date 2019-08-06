// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class DownloadCountReport
        : ReportBase
    {
        private const string _storedProcedureName = "[dbo].[SelectTotalDownloadCountsPerPackageVersion]";

        public DownloadCountReport(
            ILogger<DownloadCountReport> logger,
            IEnumerable<StorageContainerTarget> targets,
            Func<Task<SqlConnection>> openStatisticsSqlConnectionAsync,
            Func<Task<SqlConnection>> openGallerySqlConnectionAsync,
            int commandTimeoutSeconds)
            : base(logger, targets, openStatisticsSqlConnectionAsync, openGallerySqlConnectionAsync, commandTimeoutSeconds)
        {
        }

        public async Task Run()
        {
            // Gather download count data from statistics warehouse
            IReadOnlyCollection<DownloadCountData> downloadData;

            using (var connection = await OpenStatisticsSqlConnectionAsync())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                _logger.LogInformation("Gathering Download Counts from {DataSource}/{InitialCatalog}...",
                    connection.DataSource, connection.Database);

                downloadData = (await connection.QueryWithRetryAsync<DownloadCountData>(
                    _storedProcedureName, commandType: CommandType.StoredProcedure, transaction: transaction, commandTimeout: CommandTimeoutSeconds)).ToList();
            }

            _logger.LogInformation("Gathered {DownloadedRowsCount} rows of data.", downloadData.Count);

            if (downloadData.Any())
            {
                // Group based on Package Id
                var grouped = downloadData.GroupBy(p => p.PackageId);
                var registrations = new JArray();
                foreach (var group in grouped)
                {
                    var details = new JArray();
                    details.Add(group.Key);
                    foreach (var gv in group)
                    {
                        // downloads.v1.json should only contain normalized versions - ignore others
                        NuGetVersion semanticVersion;
                        if (!string.IsNullOrEmpty(gv.PackageVersion)
                            && NuGetVersion.TryParse(gv.PackageVersion, out semanticVersion)
                            && gv.PackageVersion == semanticVersion.ToNormalizedString())
                        {
                            var version = new JArray(gv.PackageVersion, gv.TotalDownloadCount);
                            details.Add(version);
                        }
                    }
                    registrations.Add(details);
                }

                var reportText = registrations.ToString(Formatting.None);

                foreach (var storageContainerTarget in Targets)
                {
                    try
                    {
                        var targetBlobContainer = await GetBlobContainer(storageContainerTarget);
                        var blob = targetBlobContainer.GetBlockBlobReference(ReportNames.DownloadCount + ReportNames.Extension);
                        _logger.LogInformation("Writing report to {ReportUri}", blob.Uri.AbsoluteUri);
                        blob.Properties.ContentType = "application/json";
                        await blob.UploadTextAsync(reportText);
                        await blob.CreateSnapshotAsync();
                        _logger.LogInformation("Wrote report to {ReportUri}", blob.Uri.AbsoluteUri);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error writing report to storage account {StorageAccount}, container {ReportContainer}: {Exception}",
                            storageContainerTarget.StorageAccount.Credentials.AccountName,
                            storageContainerTarget.ContainerName,
                            ex);
                    }
                }
            }
        }
    }
}
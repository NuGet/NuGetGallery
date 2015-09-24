// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Stats.CreateAzureCdnDownloadCountReports
{
    public class DownloadCountReport
        : ReportBase
    {
        private const string _storedProcedureName = "[dbo].[SelectTotalDownloadCountsPerPackageVersion]";
        private const int _defaultCommandTimeout = 1800; // 30 minutes max
        internal const string ReportName = "downloads.v1.json";

        public DownloadCountReport(IEnumerable<StorageContainerTarget> targets, SqlConnectionStringBuilder statisticsDatabase, SqlConnectionStringBuilder galleryDatabase)
            : base(targets, statisticsDatabase, galleryDatabase)
        {
        }

        public async Task Run()
        {
            // Gather download count data from statistics warehouse
            IReadOnlyCollection<DownloadCountData> downloadData;
            Trace.TraceInformation("Gathering Download Counts from {0}/{1}...", StatisticsDatabase.DataSource, StatisticsDatabase.InitialCatalog);
            using (var connection = await StatisticsDatabase.ConnectTo())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                downloadData = (await connection.QueryWithRetryAsync<DownloadCountData>(
                    _storedProcedureName, commandType: CommandType.StoredProcedure, transaction: transaction, commandTimeout: _defaultCommandTimeout)).ToList();
            }

            Trace.TraceInformation("Gathered {0} rows of data.", downloadData.Count);

            if (downloadData.Any())
            {
                SemanticVersion semanticVersion = null;

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
                        if (SemanticVersion.TryParse(gv.PackageVersion, out semanticVersion)
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
                        var blob = targetBlobContainer.GetBlockBlobReference(ReportName);
                        Trace.TraceInformation("Writing report to {0}", blob.Uri.AbsoluteUri);
                        blob.Properties.ContentType = "application/json";
                        await blob.UploadTextAsync(reportText);
                        Trace.TraceInformation("Wrote report to {0}", blob.Uri.AbsoluteUri);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("Error writing report to storage account {0}, container {1}. {2} {3}",
                            storageContainerTarget.StorageAccount.Credentials.AccountName,
                            storageContainerTarget.ContainerName, ex.Message, ex.StackTrace);
                    }
                }
            }
        }
    }
}
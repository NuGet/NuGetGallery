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
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stats.CreateAzureCdnDownloadCountReports
{
    public class DownloadCountReport
        : ReportBase
    {
        private const string StoredProcedureName = "[dbo].[SelectTotalDownloadCountsPerPackageVersion]";
        private const string ReportName = "downloads.v1.json";

        public DownloadCountReport(CloudStorageAccount cloudStorageAccount, string statisticsContainerName, SqlConnectionStringBuilder statisticsDatabase, SqlConnectionStringBuilder galleryDatabase)
            : base(cloudStorageAccount, statisticsContainerName, statisticsDatabase, galleryDatabase)
        {
        }

        public async Task Run()
        {
            var targetBlobContainer = await GetBlobContainer();

            Trace.TraceInformation("Generating Download Count Report from {0}/{1} to {2}/{3}.", StatisticsDatabase.DataSource, StatisticsDatabase.InitialCatalog, CloudStorageAccount.Credentials.AccountName, StatisticsContainerName);

            // Gather download count data from statistics warehouse
            IReadOnlyCollection<DownloadCountData> downloadData;
            Trace.TraceInformation("Gathering Download Counts from {0}/{1}...", StatisticsDatabase.DataSource, StatisticsDatabase.InitialCatalog);
            using (var connection = await StatisticsDatabase.ConnectTo())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                downloadData = (await connection.QueryWithRetryAsync<DownloadCountData>(
                    StoredProcedureName, commandType: CommandType.StoredProcedure, transaction: transaction)).ToList();
            }

            Trace.TraceInformation("Gathered {0} rows of data.", downloadData.Count);

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
                        var version = new JArray(gv.PackageVersion, gv.TotalDownloadCount);
                        details.Add(version);
                    }
                    registrations.Add(details);
                }

                var blob = targetBlobContainer.GetBlockBlobReference(ReportName);
                Trace.TraceInformation("Writing report to {0}", blob.Uri.AbsoluteUri);
                blob.Properties.ContentType = "application/json";
                await blob.UploadTextAsync(registrations.ToString(Formatting.None));
                Trace.TraceInformation("Wrote report to {0}", blob.Uri.AbsoluteUri);
            }
        }
    }
}
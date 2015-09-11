// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace Stats.CreateAzureCdnDownloadCountReports
{
    public class GalleryTotalsReport
        : ReportBase
    {
        private const string WarehouseStoredProcedureName = "[dbo].[SelectTotalDownloadCounts]";
        private const string GalleryQuery = @"SELECT
                    (SELECT COUNT([Key]) FROM PackageRegistrations pr WITH (NOLOCK)
                            WHERE EXISTS (SELECT 1 FROM Packages p WITH (NOLOCK) WHERE p.PackageRegistrationKey = pr.[Key] AND p.Listed = 1)) AS UniquePackages,
                    (SELECT COUNT([Key]) FROM Packages WITH (NOLOCK) WHERE Listed = 1) AS TotalPackages";
        private const string ReportName = "stats-totals.json";

        public GalleryTotalsReport(CloudStorageAccount cloudStorageAccount, string statisticsContainerName, SqlConnectionStringBuilder statisticsDatabase, SqlConnectionStringBuilder galleryDatabase)
            : base(cloudStorageAccount, statisticsContainerName, statisticsDatabase, galleryDatabase)
        {
        }

        public async Task Run()
        {
            var targetBlobContainer = await GetBlobContainer();

            Trace.TraceInformation("Generating Gallery Totals Report from {0}/{1} to {2}/{3}.", StatisticsDatabase.DataSource, StatisticsDatabase.InitialCatalog, CloudStorageAccount.Credentials.AccountName, StatisticsContainerName);

            // gather package numbers from gallery database
            GalleryTotalsData totalsData;
            Trace.TraceInformation("Gathering Gallery Totals from {0}/{1}...", GalleryDatabase.DataSource, GalleryDatabase.InitialCatalog);
            using (var connection = await GalleryDatabase.ConnectTo())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                totalsData = (await connection.QueryWithRetryAsync<GalleryTotalsData>(
                    GalleryQuery, commandType: CommandType.Text, transaction: transaction)).First();
            }
            Trace.TraceInformation("Total packages: {0}", totalsData.TotalPackages);
            Trace.TraceInformation("Unique packages: {0}", totalsData.UniquePackages);

            // gather download count data from statistics warehouse
            Trace.TraceInformation("Gathering Gallery Totals from {0}/{1}...", StatisticsDatabase.DataSource, StatisticsDatabase.InitialCatalog);
            using (var connection = await StatisticsDatabase.ConnectTo())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                totalsData.Downloads = (await connection.ExecuteScalarWithRetryAsync<int>(
                    WarehouseStoredProcedureName, commandType: CommandType.StoredProcedure, transaction: transaction));
            }
            Trace.TraceInformation("Total downloads: {0}", totalsData.Downloads);

            // write to blob
            totalsData.LastUpdateDateUtc = DateTime.UtcNow;
            var blob = targetBlobContainer.GetBlockBlobReference(ReportName);
            Trace.TraceInformation("Writing report to {0}", blob.Uri.AbsoluteUri);
            blob.Properties.ContentType = "application/json";
            await blob.UploadTextAsync(JsonConvert.SerializeObject(totalsData));
            Trace.TraceInformation("Wrote report to {0}", blob.Uri.AbsoluteUri);
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Jobs;

namespace Stats.AggregateCdnDownloadsInGallery
{  
    public class Job
        : JobBase
    {
        private const string TempTableName = "#AggregateCdnDownloadsInGallery";

        private const string CreateTempTable = @"
            IF OBJECT_ID('tempdb.dbo.#AggregateCdnDownloadsInGallery', 'U') IS NOT NULL
                DROP TABLE #AggregateCdnDownloadsInGallery

            CREATE TABLE #AggregateCdnDownloadsInGallery
            (
                [PackageRegistrationKey]    INT             NOT NULL,
                [PackageVersion]            NVARCHAR(255)	NOT NULL,
                [DownloadCount]             INT             NOT NULL,
            )";

        private const string UpdateFromTempTable = @"
            -- Update Packages table
            UPDATE P SET P.[DownloadCount] = Stats.[DownloadCount]
            FROM [dbo].[Packages] AS P
            INNER JOIN #AggregateCdnDownloadsInGallery AS Stats ON Stats.[PackageRegistrationKey] = P.[PackageRegistrationKey]
            WHERE P.[Version] = Stats.[PackageVersion]

            -- Update PackageRegistrations table
            UPDATE PR SET PR.[DownloadCount] = AggregateStats.[DownloadCount]
            FROM [dbo].[PackageRegistrations] AS PR
            INNER JOIN (
	            SELECT Stats.[PackageRegistrationKey] AS [PackageRegistrationKey], SUM(Stats.[DownloadCount]) AS [DownloadCount]
	            FROM #AggregateCdnDownloadsInGallery AS Stats
	            GROUP BY Stats.[PackageRegistrationKey]
            ) AS AggregateStats ON AggregateStats.[PackageRegistrationKey] = PR.[Key]

            -- No more need for temp table
            DROP TABLE #AggregateCdnDownloadsInGallery";

        private const string StoredProcedureName = "[dbo].[SelectTotalDownloadCountsPerPackageVersion]";
        private SqlConnectionStringBuilder _statisticsDatabase;
        private SqlConnectionStringBuilder _destinationDatabase;

        public Job()
            : base(JobEventSource.Log)
        {
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var statisticsDatabaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatisticsDatabase);
                _statisticsDatabase = new SqlConnectionStringBuilder(statisticsDatabaseConnectionString);

                var destinationDatabaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.DestinationDatabase);
                _destinationDatabase = new SqlConnectionStringBuilder(destinationDatabaseConnectionString);

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                return false;
            }
        }

        public override async Task<bool> Run()
        {
            try
            {
                Trace.TraceInformation("Updating Download Counts from {0}/{1} to {2}/{3}.", _statisticsDatabase.DataSource, _statisticsDatabase.InitialCatalog, _destinationDatabase.DataSource, _destinationDatabase.InitialCatalog);

                // Gather download counts data from statistics warehouse
                IReadOnlyCollection<DownloadCountData> downloadData;
                Trace.TraceInformation("Gathering Download Counts from {0}/{1}...", _statisticsDatabase.DataSource, _statisticsDatabase.InitialCatalog);
                using (var statisticsDatabase = await _statisticsDatabase.ConnectTo())
                using (var statisticsDatabaseTransaction = statisticsDatabase.BeginTransaction(IsolationLevel.Snapshot)) {
                    downloadData = (
                        await statisticsDatabase.QueryWithRetryAsync<DownloadCountData>(
                            StoredProcedureName, 
                            transaction: statisticsDatabaseTransaction, 
                            commandType: CommandType.StoredProcedure,
                            commandTimeout: (int)TimeSpan.FromMinutes(15).TotalSeconds,
                            maxRetries: 3))
                        .ToList();
                }

                Trace.TraceInformation("Gathered {0} rows of data.", downloadData.Count);

                if (downloadData.Any())
                {
                    // Group based on Package Id
                    var packageRegistrationGroups = downloadData.GroupBy(p => p.PackageId);

                    using (var destinationDatabase = await _destinationDatabase.ConnectTo())
                    {
                        // Fetch package registrations so we can match
                        Trace.TraceInformation("Retrieving package registrations...");
                        var packageRegistrationLookup = (
                            await destinationDatabase.QueryWithRetryAsync<PackageRegistrationData>(
                                "SELECT [Key], LOWER([Id]) AS Id FROM [dbo].[PackageRegistrations]",
                                commandTimeout: (int)TimeSpan.FromMinutes(10).TotalSeconds,
                                maxRetries: 5))
                            .Where(item => !string.IsNullOrEmpty(item.Id))
                            .ToDictionary(item => item.Id, item => item.Key);
                        Trace.TraceInformation("Retrieved package registrations.");

                        // Create a temporary table
                        Trace.TraceInformation("Creating temporary table...");
                        await destinationDatabase.ExecuteAsync(CreateTempTable);
                        
                        // Load temporary table
                        var aggregateCdnDownloadsInGalleryTable = new DataTable();
                        var command = new SqlCommand("SELECT * FROM " + TempTableName, destinationDatabase);
                        command.CommandType = CommandType.Text;
                        command.CommandTimeout = (int)TimeSpan.FromMinutes(10).TotalSeconds;
                        var reader = await command.ExecuteReaderAsync();
                        aggregateCdnDownloadsInGalleryTable.Load(reader);
                        aggregateCdnDownloadsInGalleryTable.Rows.Clear();
                        Trace.TraceInformation("Created temporary table.");

                        // Populate temporary table in memory
                        Trace.TraceInformation("Populating temporary table in memory...");
                        foreach (var packageRegistrationGroup in packageRegistrationGroups)
                        {
                            // don't process empty package id's
                            if (string.IsNullOrEmpty(packageRegistrationGroup.First().PackageId))
                            {
                                continue;
                            }
                            
                            var packageId = packageRegistrationGroup.First().PackageId.ToLowerInvariant();

                            // Get package registration key
                            if (!packageRegistrationLookup.ContainsKey(packageId))
                            {
                                continue;
                            }
                            var packageRegistrationKey = packageRegistrationLookup[packageId];

                            // Set download count on individual packages
                            foreach (var package in packageRegistrationGroup)
                            {
                                var row = aggregateCdnDownloadsInGalleryTable.NewRow();
                                row["PackageRegistrationKey"] = packageRegistrationKey;
                                row["PackageVersion"] = package.PackageVersion;
                                row["DownloadCount"] = package.TotalDownloadCount;
                                aggregateCdnDownloadsInGalleryTable.Rows.Add(row);
                            }
                        }
                        Trace.TraceInformation("Populated temporary table in memory. (" + aggregateCdnDownloadsInGalleryTable.Rows.Count + " rows)");

                        // Transfer to SQL database
                        Trace.TraceInformation("Populating temporary table in database...");
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(destinationDatabase))
                        {
                            bulkcopy.BulkCopyTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
                            bulkcopy.DestinationTableName = TempTableName;
                            bulkcopy.WriteToServer(aggregateCdnDownloadsInGalleryTable);
                            bulkcopy.Close();
                        }
                        Trace.TraceInformation("Populated temporary table in database.");

                        // Update counts in destination database
                        Trace.TraceInformation("Updating destination database Download Counts... (" + packageRegistrationGroups.Count() + " package registrations to process)");
                        await destinationDatabase.ExecuteAsync(UpdateFromTempTable, 
                            timeout: (int)TimeSpan.FromMinutes(30).TotalSeconds);
                        Trace.TraceInformation("Updated destination database Download Counts.");
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                return false;
            }
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using IPackageIdGroup = System.Linq.IGrouping<string, Stats.AggregateCdnDownloadsInGallery.DownloadCountData>;

namespace Stats.AggregateCdnDownloadsInGallery
{
    public class Job
        : JobBase
    {
        private const int _defaultBatchSize = 5000;
        private const int _defaultBatchSleepSeconds = 10;
        private const string _tempTableName = "#AggregateCdnDownloadsInGallery";

        private const string _createTempTable = @"
            IF OBJECT_ID('tempdb.dbo.#AggregateCdnDownloadsInGallery', 'U') IS NOT NULL
                DROP TABLE #AggregateCdnDownloadsInGallery

            CREATE TABLE #AggregateCdnDownloadsInGallery
            (
                [PackageRegistrationKey]    INT             NOT NULL,
                [PackageVersion]            NVARCHAR(255)   NOT NULL,
                [DownloadCount]             INT             NOT NULL,
            )";

        private const string _updateFromTempTable = @"
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

        private const string _storedProcedureName = "[dbo].[SelectTotalDownloadCountsPerPackageVersion]";
        private SqlConnectionStringBuilder _statisticsDatabase;
        private SqlConnectionStringBuilder _destinationDatabase;
        private int _batchSize;
        private int _batchSleepSeconds;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var statisticsDatabaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatisticsDatabase);
            _statisticsDatabase = new SqlConnectionStringBuilder(statisticsDatabaseConnectionString);

            var destinationDatabaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.DestinationDatabase);
            _destinationDatabase = new SqlConnectionStringBuilder(destinationDatabaseConnectionString);

            _batchSize = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.BatchSize) ?? _defaultBatchSize;
            _batchSleepSeconds = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.BatchSleepSeconds) ?? _defaultBatchSleepSeconds;
        }

        public override async Task Run()
        {
            // Gather download counts data from statistics warehouse
            IReadOnlyList<DownloadCountData> downloadData;
            Logger.LogInformation("Using batch size {BatchSize} and batch sleep seconds {BatchSleepSeconds}.", _batchSize, _batchSleepSeconds);
            Logger.LogInformation("Gathering Download Counts from {DataSource}/{InitialCatalog}...", _statisticsDatabase.DataSource, _statisticsDatabase.InitialCatalog);
            var stopwatch = Stopwatch.StartNew();

            using (var statisticsDatabase = await _statisticsDatabase.ConnectTo())
            using (var statisticsDatabaseTransaction = statisticsDatabase.BeginTransaction(IsolationLevel.Snapshot))
            {
                downloadData = (
                    await statisticsDatabase.QueryWithRetryAsync<DownloadCountData>(
                        _storedProcedureName,
                        transaction: statisticsDatabaseTransaction,
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: TimeSpan.FromMinutes(15),
                        maxRetries: 3))
                    .ToList();
            }

            Logger.LogInformation(
                "Gathered {RecordCount} rows of data (took {DurationSeconds} seconds).",
                downloadData.Count,
                stopwatch.Elapsed.TotalSeconds);

            if (!downloadData.Any())
            {
                Logger.LogInformation("No download data to process.");
                return;
            }

            using (var destinationDatabase = await _destinationDatabase.ConnectTo())
            {
                // Fetch package registrations so we can match package ID to package registration key.
                var packageRegistrationLookup = await GetPackageRegistrations(destinationDatabase);

                // Group based on package ID and store in a stack for easy incremental processing.
                var allGroups = downloadData.GroupBy(p => p.PackageId).ToList();
                var filteredGroups = allGroups.Where(g => IsValidGroup(packageRegistrationLookup, g)).ToList();
                var removedCount = allGroups.Count - filteredGroups.Count;
                Logger.LogInformation("{TotalGroupCount} package ID groups were found in the statistics database.", allGroups.Count);
                Logger.LogInformation("{RemovedGroupCount} package ID groups were filtered out because they aren't in the gallery database.", removedCount);
                Logger.LogInformation("{RemainingGroupCount} package ID groups will be processed.", filteredGroups.Count);

                var remainingGroups = new Stack<IPackageIdGroup>(filteredGroups);

                stopwatch.Restart();

                while (remainingGroups.Any())
                {
                    // Create a batch of one or more package registrations to update.
                    var batch = PopGroupBatch(remainingGroups, _batchSize);

                    await ProcessBatch(batch, destinationDatabase, packageRegistrationLookup);

                    Logger.LogInformation(
                        "There are {GroupCount} package registration groups remaining.",
                        remainingGroups.Count);

                    if (remainingGroups.Any())
                    {
                        Logger.LogInformation("Sleeping for {BatchSleepSeconds} seconds before continuing.", _batchSleepSeconds);
                        await Task.Delay(TimeSpan.FromSeconds(_batchSleepSeconds));
                    }
                }

                Logger.LogInformation(
                    "It took {DurationSeconds} seconds to update all download counts.",
                    stopwatch.Elapsed.TotalSeconds);
            }
        }

        private async Task ProcessBatch(List<IPackageIdGroup> batch, SqlConnection destinationDatabase, IDictionary<string, string> packageRegistrationLookup)
        {
            // Create a temporary table
            Logger.LogDebug("Creating temporary table...");
            await destinationDatabase.ExecuteAsync(_createTempTable);

            // Load temporary table
            var aggregateCdnDownloadsInGalleryTable = new DataTable();
            var command = new SqlCommand("SELECT * FROM " + _tempTableName, destinationDatabase);
            command.CommandType = CommandType.Text;
            command.CommandTimeout = (int)TimeSpan.FromMinutes(10).TotalSeconds;
            var reader = await command.ExecuteReaderAsync();
            aggregateCdnDownloadsInGalleryTable.Load(reader);
            aggregateCdnDownloadsInGalleryTable.Rows.Clear();
            aggregateCdnDownloadsInGalleryTable.TableName = $"dbo.{_tempTableName}";
            Logger.LogInformation("Created temporary table.");

            // Populate temporary table in memory
            Logger.LogDebug("Populating temporary table in memory...");
            var stopwatch = Stopwatch.StartNew();

            foreach (var packageRegistrationGroup in batch)
            {
                var packageId = packageRegistrationGroup.Key.ToLowerInvariant();
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

            Logger.LogInformation(
                "Populated temporary table in memory with {RecordCount} rows (took {DurationSeconds} seconds).",
                aggregateCdnDownloadsInGalleryTable.Rows.Count,
                stopwatch.Elapsed.TotalSeconds);

            // Transfer to SQL database
            Logger.LogDebug("Populating temporary table in database...");
            stopwatch.Restart();

            using (SqlBulkCopy bulkcopy = new SqlBulkCopy(destinationDatabase))
            {
                bulkcopy.BulkCopyTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
                bulkcopy.DestinationTableName = _tempTableName;
                bulkcopy.WriteToServer(aggregateCdnDownloadsInGalleryTable);
                bulkcopy.Close();
            }

            Logger.LogInformation(
                "Populated temporary table in database (took {DurationSeconds} seconds).",
                stopwatch.Elapsed.TotalSeconds);

            // Update counts in destination database
            Logger.LogInformation("Updating destination database Download Counts... ({RecordCount} package registrations to process).", batch.Count());
            stopwatch.Restart();

            await destinationDatabase.ExecuteAsync(_updateFromTempTable,
                commandTimeout: TimeSpan.FromMinutes(30));

            Logger.LogInformation(
                "Updated destination database Download Counts (took {DurationSeconds} seconds).",
                stopwatch.Elapsed.TotalSeconds);
        }

        public static List<IPackageIdGroup> PopGroupBatch(Stack<IPackageIdGroup> remainingGroups, int batchSize)
        {
            var batch = new List<IPackageIdGroup>();

            // Always add at least one package ID
            batch.Add(remainingGroups.Pop());

            // Add the next package ID grouping if we don't exceed the desired batch size.
            while (remainingGroups.Any() && CalculatedUpdateCount(batch, remainingGroups.Peek()) <= batchSize)
            {
                batch.Add(remainingGroups.Pop());
            }

            return batch;
        }

        private bool IsValidGroup(IDictionary<string, string> packageRegistrationLookup, IPackageIdGroup group)
        {
            // Don't process missing package IDs.
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                return false;
            }

            var packageId = group.Key.ToLowerInvariant();

            // Make sure the package ID exists in the database.
            return packageRegistrationLookup.ContainsKey(packageId);
        }

        private static int CalculatedUpdateCount(List<IPackageIdGroup> batch, IPackageIdGroup candidate)
        {
            // The number of records to be updated is the number of package versions plus the number of package IDs,
            // since both package IDs and package versions have their own download counts.
            return batch.Count + 1 + batch.Sum(x => x.Count()) + candidate.Count();
        }

        private async Task<IDictionary<string, string>> GetPackageRegistrations(SqlConnection sqlConnection)
        {
            Logger.LogDebug("Retrieving package registrations...");

            var stopwatch = Stopwatch.StartNew();

            var packageRegistrationDictionary = new Dictionary<string, string>();

            // Ensure results are sorted deterministically.
            var packageRegistrationData = (await sqlConnection.QueryWithRetryAsync<PackageRegistrationData>(
                    "SELECT [Key], LOWER([Id]) AS LowercasedId, [Id] AS OriginalId FROM [dbo].[PackageRegistrations] (NOLOCK) ORDER BY [Id] ASC",
                    commandTimeout: TimeSpan.FromMinutes(10),
                    maxRetries: 5)).ToList();

            // We are not using .ToDictionary() and instead explicitly looping through these items to be able to detect
            // and avoid potential duplicate keys caused by LOWER([Id]) conflicts that may occur.
            foreach (var item in packageRegistrationData)
            {
                if (string.IsNullOrEmpty(item.LowercasedId))
                {
                    continue;
                }

                if (!packageRegistrationDictionary.ContainsKey(item.LowercasedId))
                {
                    packageRegistrationDictionary.Add(item.LowercasedId, item.Key);
                }
                else
                {
                    var conflictingPackageRegistration = packageRegistrationDictionary[item.LowercasedId];
                    var conflictingPackageOriginalId = packageRegistrationData.Single(p => p.Key == conflictingPackageRegistration).OriginalId;

                    // Lowercased package ID's should be unique, however, there's the case of the Turkish i...
                    Logger.LogWarning(
                        "Package registration conflict detected: skipping package registration with key {Key} " +
                        "and ID {LowercasedId}. Package {OriginalId} conflicts with package " +
                        "{ConflictingPackageOriginalId}.",
                        item.Key,
                        item.LowercasedId,
                        item.OriginalId,
                        conflictingPackageOriginalId);
                }
            }

            Logger.LogInformation(
                "Retrieved {Count} package registrations (took {DurationSeconds} seconds).",
                packageRegistrationData.Count,
                stopwatch.Elapsed.TotalSeconds);

            return packageRegistrationDictionary;
        }
    }
}
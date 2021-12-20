// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.FeatureFlags;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGetGallery;
using IPackageIdGroup = System.Linq.IGrouping<string, Stats.AggregateCdnDownloadsInGallery.DownloadCountData>;

namespace Stats.AggregateCdnDownloadsInGallery
{
    public class AggregateCdnDownloadsJob : JsonConfigurationJob
    {
        private const string DownloadsV1JsonConfigurationSectionName = "DownloadsV1Json";
        private const string AlternateStatisticsSourceFeatureFlagName = "NuGetGallery.AlternateStatisticsSource";

        private const int _defaultCommandTimeoutSeconds = 1800; // 30 minutes
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

        private AggregateCdnDownloadsConfiguration _configuration;
        private int _commandTimeoutSeconds;
        private IDownloadsV1JsonClient _downloadsV1JsonClient;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            _configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<AggregateCdnDownloadsConfiguration>>().Value;
            _commandTimeoutSeconds = _configuration.CommandTimeoutSeconds ?? _defaultCommandTimeoutSeconds;
            _downloadsV1JsonClient = _serviceProvider.GetRequiredService<IDownloadsV1JsonClient>();
        }

        public override async Task Run()
        {
            var featureFlagRefresher = _serviceProvider.GetRequiredService<IFeatureFlagRefresher>();
            await featureFlagRefresher.StartIfConfiguredAsync();

            IReadOnlyCollection<DownloadCountData> downloadData = null;
            var jsonConfigurationAccessor = _serviceProvider.GetService<IOptionsSnapshot<DownloadsV1JsonConfiguration>>();
            if (jsonConfigurationAccessor == null || jsonConfigurationAccessor.Value == null)
            {
                downloadData = await GetDownloadDataFromStatsDbAsync();
            }
            else
            {
                var ff = _serviceProvider.GetRequiredService<IFeatureFlagClient>();
                string url = jsonConfigurationAccessor.Value.SqlPipelineUrl;
                if (ff.IsEnabled(AlternateStatisticsSourceFeatureFlagName, defaultValue: false))
                {
                    url = jsonConfigurationAccessor.Value.SynapsePipelineUrl;
                }

                var result = new List<DownloadCountData>();
                await _downloadsV1JsonClient.ReadAsync(url, (id, version, downloads) =>
                {
                    result.Add(new DownloadCountData { PackageId = id, PackageVersion = version, TotalDownloadCount = downloads });
                });
                downloadData = result;
            }

            if (!downloadData.Any())
            {
                Logger.LogInformation("No download data to process.");
                return;
            }

            using (var connection = await OpenSqlConnectionAsync<GalleryDbConfiguration>())
            {
                // Fetch package registrations so we can match package ID to package registration key.
                var packageRegistrationLookup = await GetPackageRegistrations(connection);

                // Group based on package ID and store in a stack for easy incremental processing.
                var allGroups = downloadData.GroupBy(p => p.PackageId).ToList();
                var filteredGroups = allGroups.Where(g => IsValidGroup(packageRegistrationLookup, g)).ToList();
                var removedCount = allGroups.Count - filteredGroups.Count;
                Logger.LogInformation("{TotalGroupCount} package ID groups were found in the statistics database.", allGroups.Count);
                Logger.LogInformation("{RemovedGroupCount} package ID groups were filtered out because they aren't in the gallery database.", removedCount);
                Logger.LogInformation("{RemainingGroupCount} package ID groups will be processed.", filteredGroups.Count);

                var remainingGroups = new Stack<IPackageIdGroup>(filteredGroups);

                var stopwatch = Stopwatch.StartNew();

                while (remainingGroups.Any())
                {
                    // Create a batch of one or more package registrations to update.
                    var batch = PopGroupBatch(remainingGroups, _configuration.BatchSize);

                    var rowsProcessed = await ProcessBatchAsync(batch, connection, packageRegistrationLookup);

                    Logger.LogInformation(
                        "There are {GroupCount} package registration groups remaining.",
                        remainingGroups.Count);

                    // will only sleep if DB write happened.
                    if (remainingGroups.Any() && rowsProcessed > 0)
                    {
                        Logger.LogInformation("Sleeping for {BatchSleepSeconds} seconds before continuing.", _configuration.BatchSleepSeconds);
                        await Task.Delay(TimeSpan.FromSeconds(_configuration.BatchSleepSeconds));
                    }
                }

                stopwatch.Stop();
                Logger.LogInformation(
                    "It took {DurationSeconds} seconds to update all download counts.",
                    stopwatch.Elapsed.TotalSeconds);
            }

            await featureFlagRefresher.StopAndWaitAsync();
        }

        private async Task<IReadOnlyList<DownloadCountData>> GetDownloadDataFromStatsDbAsync()
        {
            // Gather download counts data from statistics warehouse
            IReadOnlyList<DownloadCountData> downloadData;
            Logger.LogInformation("Using batch size {BatchSize} and batch sleep seconds {BatchSleepSeconds}.",
                _configuration.BatchSize,
                _configuration.BatchSleepSeconds);

            var stopwatch = Stopwatch.StartNew();

            using (var connection = await OpenSqlConnectionAsync<StatisticsDbConfiguration>())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                Logger.LogInformation("Gathering Download Counts from {DataSource}/{InitialCatalog}...", connection.DataSource, connection.Database);

                downloadData = (
                    await connection.QueryWithRetryAsync<DownloadCountData>(
                        _storedProcedureName,
                        transaction: transaction,
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: TimeSpan.FromSeconds(_commandTimeoutSeconds),
                        maxRetries: 3))
                    .ToList();
            }

            stopwatch.Stop();
            Logger.LogInformation(
                "Gathered {RecordCount} rows of data (took {DurationSeconds} seconds).",
                downloadData.Count,
                stopwatch.Elapsed.TotalSeconds);

            return downloadData;
        }

        private async Task<int> ProcessBatchAsync(List<IPackageIdGroup> batch, SqlConnection destinationDatabase, IDictionary<string, PackageRegistrationData> packageRegistrationLookup)
        {
            // Create a temporary table
            Logger.LogDebug("Creating temporary table...");

            using (var cmd = destinationDatabase.CreateCommand())
            {
                cmd.CommandText = _createTempTable;
                cmd.CommandType = CommandType.Text;

                await cmd.ExecuteNonQueryAsync();
            }

            // Load temporary table
            var aggregateCdnDownloadsInGalleryTable = new DataTable();
            var command = new SqlCommand("SELECT * FROM " + _tempTableName, destinationDatabase);
            command.CommandType = CommandType.Text;
            command.CommandTimeout = _commandTimeoutSeconds;
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
                // The packageRegistrationGroup is created with data from Statistics db and grouped based on packageId.
                // Each group has a key that is the packageId and has a list of packages that are the versions for the packageId that is the key.
                // Using the packageId the packageRegistration key is found - see  packageRegistrationData = packageRegistrationLookup[packageId];
                // Using this package registation key the data in the temp table is populated - see: row["PackageRegistrationKey"] = packageRegistrationData.Key;
                // If not all the elements in a group have the same PackageId that will lead to have data from packageId2 version:1.0.0 to be ingested as data for packageId1: version:1.0.0
                // In this case data will not be ingested.
                if (packageRegistrationGroup.Select(g => g.PackageId).Distinct().Count() == 1)
                {
                    var packageId = packageRegistrationGroup.Key.ToLowerInvariant();
                    var packageRegistrationData = packageRegistrationLookup[packageId];

                    // Calculate the total sum of the new downloads 
                    // If it is not greater than the current download count there is not any need to update.

                    // This data is from Statistics db.
                    long newDownloadCount = packageRegistrationGroup.Select(g => g.TotalDownloadCount).Sum();

                    // This data is from Gallery db, PackageRegistration table.
                    long currentDownloadCount = long.Parse(packageRegistrationData.DownloadCount);

                    if (newDownloadCount > currentDownloadCount)
                    {
                        Logger.LogInformation("PackageId:{PackageId} CurrentDownloadCount:{CurrentDownloadCount} NewDownloadCount:{NewDownloadCount}",
                            packageId,
                            currentDownloadCount,
                            newDownloadCount);

                        // Set download count on individual packages
                        foreach (var package in packageRegistrationGroup)
                        {
                            var row = aggregateCdnDownloadsInGalleryTable.NewRow();
                            row["PackageRegistrationKey"] = packageRegistrationData.Key;
                            row["PackageVersion"] = package.PackageVersion;
                            row["DownloadCount"] = package.TotalDownloadCount;
                            aggregateCdnDownloadsInGalleryTable.Rows.Add(row);
                        }
                    }
                    if (newDownloadCount < currentDownloadCount)
                    {
                        Logger.LogCritical(LogEvents.DownloadCountDecreaseDetected, "{PackageId} {CurrentDownloadCount} {NewDownloadCount}", packageRegistrationGroup.Key, currentDownloadCount, newDownloadCount);
                    }
                }
                else
                {
                    // This is not expected to happen as it should be one id per group. 
                    Logger.LogCritical(LogEvents.IncorrectIdsInGroupBatch, "{GroupKey} {Ids}", packageRegistrationGroup.Key, string.Join(",", packageRegistrationGroup.Select(g => g.PackageId).Distinct()));
                }
            }

            stopwatch.Stop();
            Logger.LogInformation(
                "Populated temporary table in memory with {RecordCount} rows (took {DurationSeconds} seconds).",
                aggregateCdnDownloadsInGalleryTable.Rows.Count,
                stopwatch.Elapsed.TotalSeconds);

            if (aggregateCdnDownloadsInGalleryTable.Rows.Count == 0)
            {
                Logger.LogInformation("No data to transfer in this batch, returning");
                return 0;
            }

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

            stopwatch.Stop();
            Logger.LogInformation(
                "Populated temporary table in database (took {DurationSeconds} seconds).",
                stopwatch.Elapsed.TotalSeconds);

            // Update counts in destination database
            Logger.LogInformation("Updating destination database Download Counts... ({RecordCount} package registrations to process).", batch.Count());
            stopwatch.Restart();

            using (var cmd = destinationDatabase.CreateCommand())
            {
                cmd.CommandText = _updateFromTempTable;
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = _commandTimeoutSeconds;

                await cmd.ExecuteNonQueryAsync();
            }

            stopwatch.Stop();
            Logger.LogInformation(
                "Updated destination database Download Counts (took {DurationSeconds} seconds).",
                stopwatch.Elapsed.TotalSeconds);

            return aggregateCdnDownloadsInGalleryTable.Rows.Count;
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

        private bool IsValidGroup(IDictionary<string, PackageRegistrationData> packageRegistrationLookup, IPackageIdGroup group)
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

        private async Task<IDictionary<string, PackageRegistrationData>> GetPackageRegistrations(SqlConnection sqlConnection)
        {
            Logger.LogDebug("Retrieving package registrations...");

            var stopwatch = Stopwatch.StartNew();

            var packageRegistrationDictionary = new Dictionary<string, PackageRegistrationData>();

            // Ensure results are sorted deterministically.
            var packageRegistrationData = (await sqlConnection.QueryWithRetryAsync<PackageRegistrationData>(
                    "SELECT [Key], LOWER([Id]) AS LowercasedId, [Id] AS OriginalId, [DownloadCount] AS DownloadCount FROM [dbo].[PackageRegistrations] (NOLOCK) ORDER BY [Id] ASC",
                    commandTimeout: TimeSpan.FromSeconds(_commandTimeoutSeconds),
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
                    packageRegistrationDictionary.Add(item.LowercasedId, item);
                }
                else
                {
                    var conflictingPackageRegistration = packageRegistrationDictionary[item.LowercasedId];
                    var conflictingPackageOriginalId = packageRegistrationData.Single(p => p.Key == conflictingPackageRegistration.Key).OriginalId;

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

            stopwatch.Stop();
            Logger.LogInformation(
                "Retrieved {Count} package registrations (took {DurationSeconds} seconds).",
                packageRegistrationData.Count,
                stopwatch.Elapsed.TotalSeconds);

            return packageRegistrationDictionary;
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            ConfigureFeatureFlagAutofacServices(containerBuilder);
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<AggregateCdnDownloadsConfiguration>(services, configurationRoot);
            services.Configure<DownloadsV1JsonConfiguration>(configurationRoot.GetSection(DownloadsV1JsonConfigurationSectionName));
            ConfigureFeatureFlagServices(services, configurationRoot);
            services.AddSingleton(_ => new HttpClient());
            services.AddTransient<IDownloadsV1JsonClient, DownloadsV1JsonClient>();
        }
    }
}
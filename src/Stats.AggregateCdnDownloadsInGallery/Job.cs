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
        private const string _storedProcedureName = "[dbo].[SelectTotalDownloadCountsPerPackageVersion]";
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
                var destinationDatabaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.DestinationDatabase);

                _statisticsDatabase = new SqlConnectionStringBuilder(statisticsDatabaseConnectionString);
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
                    downloadData = (await statisticsDatabase.QueryWithRetryAsync<DownloadCountData>(
                        _storedProcedureName, transaction: statisticsDatabaseTransaction, commandType: CommandType.StoredProcedure)).ToList();
                }

                Trace.TraceInformation("Gathered {0} rows of data.", downloadData.Count);

                if (downloadData.Any())
                {
                    // Group based on Package Id
                    var packageRegistrationGroups = downloadData.GroupBy(p => p.PackageId);

                    Trace.TraceInformation("Updating destination database Download Counts... (" + packageRegistrationGroups.Count() + " package registrations to process)");
                    using (var destinationDatabase = await _destinationDatabase.ConnectTo())
                    {
                        var packageRegistrationLookup = (
                            await destinationDatabase.QueryWithRetryAsync<PackageRegistrationData>(
                                "SELECT [Key], LOWER([Id]) AS Id FROM [dbo].[PackageRegistrations]"))
                            .Where(item => !string.IsNullOrEmpty(item.Id))
                            .ToDictionary(item => item.Id, item => item.Key);

                        foreach (var packageRegistrationGroup in packageRegistrationGroups)
                        {
                            // don't process empty package id's
                            if (string.IsNullOrEmpty(packageRegistrationGroup.First().PackageId))
                            {
                                continue;
                            }

                            using (var packageRegistrationGroupTransaction = destinationDatabase.BeginTransaction())
                            {
                                var packageId = packageRegistrationGroup.First().PackageId.ToLowerInvariant();
                                var totalForGroup = packageRegistrationGroup.Sum(p => p.TotalDownloadCount);

                                // Get package registration key
                                if (!packageRegistrationLookup.ContainsKey(packageId))
                                {
                                    continue;
                                }
                                var packageRegistrationKey = packageRegistrationLookup[packageId];

                                // Set download count on individual packages
                                foreach (var package in packageRegistrationGroup)
                                {
                                    // Set download count on package registration
                                    await destinationDatabase.ExecuteAsync(
                                        string.Format("UPDATE [dbo].[Packages] SET [DownloadCount] = {0} WHERE LOWER([Version]) = '{1}' AND [PackageRegistrationKey] = {2} AND [DownloadCount] <> {0}", package.TotalDownloadCount, package.PackageVersion.ToLowerInvariant(), packageRegistrationKey),
                                        packageRegistrationGroupTransaction);
                                }
                                
                                // Set download count on package registration
                                await destinationDatabase.ExecuteAsync(
                                    string.Format("UPDATE [dbo].[PackageRegistrations] SET [DownloadCount] = {0} WHERE [Key] = {1} AND [DownloadCount] <> {0}", totalForGroup, packageRegistrationKey),
                                    packageRegistrationGroupTransaction);

                                packageRegistrationGroupTransaction.Commit();

                                Trace.TraceInformation("Updated download counts for package " + packageId + ".");
                            }
                        }
                    }
                    Trace.TraceInformation("Updated destination database Download Counts.");
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
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    internal class DownloadFacts
    {
        internal static async Task<DataTable> CreateAsync(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var stopwatch = Stopwatch.StartNew();

            // insert any new dimension data first
            Trace.WriteLine("Querying dimension: operation");
            var operations = await Warehouse.RetrieveOperationDimensions(sourceData, connection);
            stopwatch.Stop();
            Trace.Write("  DONE (" + operations.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            Trace.WriteLine("Querying dimension: project type");
            stopwatch.Restart();
            var projectTypes = await Warehouse.RetrieveProjectTypeDimensions(sourceData, connection);
            stopwatch.Stop();
            Trace.Write("  DONE (" + projectTypes.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            Trace.WriteLine("Querying dimension: client");
            stopwatch.Restart();
            var clients = await Warehouse.RetrieveClientDimensions(sourceData, connection);
            stopwatch.Stop();
            Trace.Write("  DONE (" + clients.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            Trace.WriteLine("Querying dimension: platform");
            stopwatch.Restart();
            var platforms = await Warehouse.RetrievePlatformDimensions(sourceData, connection);
            stopwatch.Stop();
            Trace.Write("  DONE (" + platforms.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            Trace.WriteLine("Querying dimension: time");
            stopwatch.Restart();
            var times = await Warehouse.RetrieveTimeDimensions(connection);
            stopwatch.Stop();
            Trace.Write("  DONE (" + times.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            Trace.WriteLine("Querying dimension: date");
            stopwatch.Restart();
            var dates = await Warehouse.RetrieveDateDimensions(connection, sourceData.Min(e => e.EdgeServerTimeDelivered), sourceData.Max(e => e.EdgeServerTimeDelivered));
            stopwatch.Stop();
            Trace.Write("  DONE (" + dates.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            Trace.WriteLine("Querying dimension: package");
            stopwatch.Restart();
            var packages = await Warehouse.RetrievePackageDimensions(sourceData, connection);
            stopwatch.Stop();
            Trace.Write("  DONE (" + packages.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            // create facts data rows by linking source data with dimensions
            // insert into temp table for increased scalability and allow for aggregation later

            var dataTable = DataImporter.GetDataTable("Fact_Download", connection);

            // ensure all dimension IDs are set to the Unknown equivalent if no dimension data is available
            int? operationId = !operations.Any() ? DimensionId.Unknown : (int?)null;
            int? projectTypeId = !projectTypes.Any() ? DimensionId.Unknown : (int?)null;
            int? clientId = !clients.Any() ? DimensionId.Unknown : (int?)null;
            int? platformId = !platforms.Any() ? DimensionId.Unknown : (int?)null;

            Trace.WriteLine("Creating facts...");
            stopwatch.Restart();
            foreach (var groupedByPackageId in sourceData.GroupBy(e => e.PackageId))
            {
                var packagesForId = packages.Where(e => e.PackageId == groupedByPackageId.Key).ToList();

                foreach (var groupedByPackageIdAndVersion in groupedByPackageId.GroupBy(e => e.PackageVersion))
                {
                    var packageId = packagesForId.First(e => e.PackageVersion == groupedByPackageIdAndVersion.Key).Id;

                    foreach (var element in groupedByPackageIdAndVersion)
                    {
                        // required dimensions
                        var dateId = dates.First(e => e.Date.Equals(element.EdgeServerTimeDelivered.Date)).Id;
                        var timeId = times.First(e => e.HourOfDay == element.EdgeServerTimeDelivered.Hour).Id;

                        // dimensions that could be "(unknown)"
                        if (!operationId.HasValue)
                        {
                            operationId = operations[element.Operation];
                        }
                        if (!platformId.HasValue)
                        {
                            platformId = platforms[element.UserAgent];
                        }
                        if (!clientId.HasValue)
                        {
                            clientId = clients[element.UserAgent];
                        }

                        if (!projectTypeId.HasValue)
                        {
                            // foreach project type
                            foreach (var projectGuid in element.ProjectGuids.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                projectTypeId = projectTypes[projectGuid];

                                var dataRow = dataTable.NewRow();
                                FillDataRow(dataRow, dateId, timeId, packageId, operationId.Value, platformId.Value, projectTypeId.Value, clientId.Value);
                                dataTable.Rows.Add(dataRow);
                            }
                        }
                        else
                        {
                            var dataRow = dataTable.NewRow();
                            FillDataRow(dataRow, dateId, timeId, packageId, operationId.Value, platformId.Value, projectTypeId.Value, clientId.Value);
                            dataTable.Rows.Add(dataRow);
                        }
                    }
                }
            }
            stopwatch.Stop();
            Trace.Write("  DONE (" + dataTable.Rows.Count + " records, " + stopwatch.ElapsedMilliseconds + "ms)");

            return dataTable;
        }

        private static void FillDataRow(DataRow dataRow, int dateId, int timeId, int packageId, int operationId, int platformId, int projectTypeId, int clientId)
        {
            dataRow["Id"] = Guid.NewGuid();
            dataRow["Dimension_Package_Id"] = packageId;
            dataRow["Dimension_Date_Id"] = dateId;
            dataRow["Dimension_Time_Id"] = timeId;
            dataRow["Dimension_Operation_Id"] = operationId;
            dataRow["Dimension_ProjectType_Id"] = projectTypeId;
            dataRow["Dimension_Client_Id"] = clientId;
            dataRow["Dimension_Platform_Id"] = platformId;
            dataRow["DownloadCount"] = 1;
        }

    }
}
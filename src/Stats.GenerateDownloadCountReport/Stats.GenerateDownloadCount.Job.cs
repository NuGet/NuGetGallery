using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Data;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using NuGet.Jobs.Common;
using Dapper;
using NuGet.Versioning;

namespace Stats.GenerateDownloadCount
{
    internal class Job : JobBase
    {
        private const string GetDownloadsScript = @"-- Work Service / GenerateDownloadCountReport / GetDownloadsScript
            SELECT p.[Key] AS PackageKey, pr.Id, p.NormalizedVersion, p.DownloadCount, pr.DownloadCount AS 'AllVersionsDownloadCount'
            FROM Packages p WITH (NOLOCK)
            INNER JOIN PackageRegistrations pr ON p.PackageRegistrationKey = pr.[Key]";
        private const string GetRecentDataScript = @"-- Work Service / GenerateDownloadCountReport / GetRecentDataScript
            DECLARE @Install int
            DECLARE @Update int

            -- Get the IDs of the Operations we're interested in
            SELECT @Install = [Id] FROM Dimension_Operation WHERE Operation = 'Install';
            SELECT @Update = [Id] FROM Dimension_Operation WHERE Operation = 'Update';

            -- Group data by Dimension_Package_Id and stuff it in a table variable
            DECLARE @temp TABLE(
	            Dimension_Package_Id int,
	            InstallCount int,
	            UpdateCount int);

            WITH cte AS(
	            SELECT
		            Dimension_Package_Id, 
		            (CASE WHEN Dimension_Operation_Id = @Install THEN 1 ELSE 0 END) AS [Install],
		            (CASE WHEN Dimension_Operation_Id = @Update THEN 1 ELSE 0 END) AS [Update]
	            FROM Fact_Download WITH(NOLOCK)
	            WHERE Dimension_Operation_Id = @Install OR Dimension_Operation_Id = @Update 
            )
            INSERT INTO @temp(Dimension_Package_Id, InstallCount, UpdateCount)
            SELECT
	            [Dimension_Package_Id],
	            SUM([Install]) AS InstallCount, 
	            SUM([Update]) AS UpdateCount
            FROM cte
            INNER JOIN Dimension_Package ON Dimension_Package.Id = cte.Dimension_Package_Id
            GROUP BY Dimension_Package_Id

            -- Get the actual PackageId and PackageVersion using the Dimension_Package_Id in the table variable
            SELECT Dimension_Package.PackageId, Dimension_Package.PackageVersion, t.InstallCount, t.UpdateCount
            FROM @temp t
            INNER JOIN Dimension_Package ON Dimension_Package.Id = t.Dimension_Package_Id";

        public static readonly string DefaultContainerName = "ng-search-data";
        public static readonly string ReportName = "downloads.v1.json";

        public SqlConnectionStringBuilder WarehouseConnection { get; set; }
        public SqlConnectionStringBuilder Source { get; set; }
        public CloudStorageAccount Destination { get; set; }
        public CloudBlobContainer DestinationContainer { get; set; }
        public string DestinationContainerName { get; set; }
        public string OutputDirectory { get; set; }

        public override async Task<bool> Run()
        {
            string destination = String.IsNullOrEmpty(OutputDirectory) ?
                (Destination.Credentials.AccountName + "/" + DestinationContainerName) :
                OutputDirectory;
            if (String.IsNullOrEmpty(destination))
            {
                throw new Exception(Strings.WarehouseJob_NoDestinationAvailable);
            }
            
            Trace.TraceInformation(String.Format("Generating Download Count Report from {0}/{1} and {2}/{3} to {4}.", Source.DataSource, Source.InitialCatalog, WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog, destination));

            // Gather download count data from packages database
            IList<DownloadCountData> downloadData;
            Trace.TraceInformation(String.Format("Gathering Download Counts from {0}/{1}...", Source.DataSource, Source.InitialCatalog));
            using (var connection = await Source.ConnectTo())
            {
                downloadData = (await connection.QueryWithRetryAsync<DownloadCountData>(GetDownloadsScript)).ToList();
            }
            Trace.TraceInformation(String.Format("Gathered {0} rows of data.", downloadData.Count));

            // Gather recent activity data from warehouse
            IList<RecentActivityData> recentActivityData;
            Trace.TraceInformation(String.Format("Gathering Recent Activity Counts from {0}/{1}...",WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog));
            using (var connection = await WarehouseConnection.ConnectTo())
            {
                recentActivityData = (await connection.QueryWithRetryAsync<RecentActivityData>(GetRecentDataScript, commandTimeout: 180)).ToList();
            }
            Trace.TraceInformation(String.Format("Gathered {0} rows of data.", recentActivityData.Count));

            // Join!
            Trace.TraceInformation("Joining data in memory...");
            IDictionary<int, FullDownloadData> data =
                downloadData.GroupJoin(
                    recentActivityData,
                    dcd => GetKey(dcd.Id, dcd.NormalizedVersion),
                    rad => GetKey(rad.PackageId, rad.PackageVersion),

                    (dcd, rads) => new {
                        Key = dcd.PackageKey, 
                        Value = new FullDownloadData()
                        {
                            Id = dcd.Id,
                            Version = dcd.NormalizedVersion,
                            Downloads = dcd.DownloadCount,
                            RegistrationDownloads = dcd.AllVersionsDownloadCount,
                            Installs = rads.Any() ? rads.Sum(r => r.InstallCount) : 0, 
                            Updates = rads.Any() ? rads.Sum(r => r.UpdateCount) : 0
                        }
                    },
                    StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            Trace.TraceInformation("Joined data.");

            // Write the report
            await WriteReport(JObject.FromObject(data), ReportName, Formatting.None);
            return true;
        }

        protected async Task WriteReport(JObject report, string name, Formatting formatting)
        {
            if (!String.IsNullOrEmpty(OutputDirectory))
            {
                await WriteToFile(report, name, formatting);
            }
            else
            {
                await DestinationContainer.CreateIfNotExistsAsync();
                await WriteToBlob(report, name, formatting);
            }
        }

        private async Task WriteToFile(JObject report, string name, Formatting formatting)
        {
            string fullPath = Path.Combine(OutputDirectory, name);
            string parentDir = Path.GetDirectoryName(fullPath);
            Trace.TraceInformation(String.Format("Writing report to {0}", fullPath));
            
                if (!Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
                using (var writer = new StreamWriter(File.OpenWrite(fullPath)))
                {
                    await writer.WriteAsync(report.ToString(formatting));
                }

                Trace.TraceInformation(String.Format("Wrote report to {0}", fullPath));
        }

        private async Task WriteToBlob(JObject report, string name, Formatting formatting)
        {
            var blob = DestinationContainer.GetBlockBlobReference(name);
            Trace.TraceInformation(String.Format("Writing report to {0}", blob.Uri.AbsoluteUri));

            blob.Properties.ContentType = "json";
                await blob.UploadTextAsync(report.ToString(formatting));

                Trace.TraceInformation(String.Format("Wrote report to {0}", blob.Uri.AbsoluteUri));
        }

        private string GetKey(string id, string version)
        {
            NuGetVersion semanticVersion = NuGetVersion.Parse(version);
            return (id + "/" + semanticVersion.ToNormalizedString()).ToLower();
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {

            Source =
                new SqlConnectionStringBuilder(
                    JobConfigManager.GetArgument(jobArgsDictionary,
                        JobArgumentNames.SourceDatabase,
                        EnvironmentVariableKeys.SqlGallery));

            WarehouseConnection =
                    new SqlConnectionStringBuilder(
                        JobConfigManager.GetArgument(jobArgsDictionary,
                            JobArgumentNames.DestinationDatabase,
                            EnvironmentVariableKeys.SqlWarehouse));
             Destination = CloudStorageAccount.Parse(
                                        JobConfigManager.GetArgument(jobArgsDictionary,
                                            JobArgumentNames.PrimaryDestination, EnvironmentVariableKeys.StoragePrimary));

             DestinationContainerName = JobConfigManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName) ?? DefaultContainerName;


             DestinationContainer = Destination.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);

             return true;

        }

        public class DownloadCountData
        {
            public int PackageKey { get; set; }
            public string Id { get; set; }
            public string NormalizedVersion { get; set; }
            public int DownloadCount { get; set; }
            public int AllVersionsDownloadCount { get; set; }
        }

        public class RecentActivityData
        {
            public string PackageId { get; set; }
            public string PackageVersion { get; set; }
            public int InstallCount { get; set; }
            public int UpdateCount { get; set; }
        }

        public class FullDownloadData
        {
            public string Id { get; set; }
            public string Version { get; set; }
            public int Downloads { get; set; }
            public int RegistrationDownloads { get; set; }
            public int Installs { get; set; }
            public int Updates { get; set; }
        }
    }

    }



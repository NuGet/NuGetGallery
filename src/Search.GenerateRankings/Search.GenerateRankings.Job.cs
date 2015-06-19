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
using Search.GenerateRankings.Helpers;
using Dapper;
using NuGet.Jobs;

namespace Search.GenerateRankings
{
    public class SearchRankingEntry
    {
        public string PackageId { get; set; }
        public int Downloads { get; set; }
    }

    internal class Job : JobBase
    {
        public static readonly int DefaultRankingCount = 250;
        public static readonly string DefaultContainerName = "ng-search-data";
        public static readonly string ReportName = "rankings.v1.json";

        public SqlConnectionStringBuilder WarehouseConnection { get; set; }
        public int? RankingCount { get; set; }
        public CloudStorageAccount Destination { get; set; }
        public CloudBlobContainer DestinationContainer { get; set; }
        public string DestinationContainerName { get; set; }
        public string OutputDirectory { get; set; }

        public override async Task<bool> Run()
        {

            RankingCount = RankingCount ?? DefaultRankingCount;

            string destination = string.IsNullOrEmpty(OutputDirectory) ?
                (Destination.Credentials.AccountName + "/" + DestinationContainer.Name) :
                OutputDirectory;
            if (string.IsNullOrEmpty(destination))
            {
                throw new Exception(Strings.WarehouseJob_NoDestinationAvailable);
            }

            Trace.TraceInformation(string.Format("Generating Search Ranking Report from {0}/{1} to {2}.", WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog, destination));

            // Gather overall rankings
            JObject report = new JObject();
            Trace.TraceInformation(string.Format("Gathering Overall Rankings from {0}/{1}...", WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog));
            var overallData = await GatherOverallRankings();
            report.Add("Rank", overallData);
            Trace.TraceInformation(string.Format("Gathered {0} rows of data.", overallData.Count));

            // Get project types
            Trace.TraceInformation(string.Format("Getting Project Types from {0}/{1}...", WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog));
            var projectTypes = await GetProjectTypes();
            Trace.TraceInformation(string.Format("Got {0} project types", projectTypes.Count));

            // Gather data by project type
            int count = 0;
            Trace.TraceInformation(string.Format("Gathering Project Type Rankings from {0}/{1}...", WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog));
            foreach (var projectType in projectTypes)
            {
                Trace.TraceInformation(string.Format("Gathering Project Type Rankings for '{2}' from {0}/{1}...", WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog, projectType));
                var data = await GatherProjectTypeRanking(projectType);
                report.Add(projectType, data);
                Trace.TraceInformation(string.Format("Gathered {0} rows of data for project type '{1}'.", data.Count, projectType));
                count += data.Count;
            }
            Trace.TraceInformation(string.Format("Gathered {0} rows of data for all project types.", count));

            // Write the JSON blob
            await WriteReport(report, ReportName, Formatting.Indented);
            return true;
        }

        private async Task<JArray> GatherOverallRankings()
        {
            using (var connection = await WarehouseConnection.ConnectTo())
            {
                // Get the script
                var script = await ResourceHelpers.ReadResourceFile("Scripts.SearchRanking_Overall.sql");

                // Execute it and return the results
                return new JArray(
                    (await connection.QueryWithRetryAsync<SearchRankingEntry>(script, new { RankingCount }, commandTimeout: 120))
                        .Select(e => e.PackageId));
            }
        }

        private async Task<JArray> GatherProjectTypeRanking(string projectType)
        {
            using (var connection = await WarehouseConnection.ConnectTo())
            {
                // Get the script
                var script = await ResourceHelpers.ReadResourceFile("Scripts.SearchRanking_ByProjectType.sql");

                // Execute it and return the results
                return new JArray(
                    (await connection.QueryWithRetryAsync<SearchRankingEntry>(script, new { RankingCount, ProjectGuid = projectType }, commandTimeout: 120))
                        .Select(e => e.PackageId));
            }
        }

        private async Task<IList<string>> GetProjectTypes()
        {
            using (var connection = await WarehouseConnection.ConnectTo())
            {
                // Execute the query and return the results
                return (await connection.QueryAsync<string>("SELECT ProjectTypes FROM Dimension_Project")).ToList();
            }
        }

        protected async Task WriteReport(JObject report, string name, Formatting formatting)
        {
            if (!string.IsNullOrEmpty(OutputDirectory))
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
            Trace.TraceInformation(string.Format("Writing report to {0}", fullPath));

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

            Trace.TraceInformation(string.Format("Wrote report to {0}", fullPath));
        }

        private async Task WriteToBlob(JObject report, string name, Formatting formatting)
        {
            var blob = DestinationContainer.GetBlockBlobReference(name);
            Trace.TraceInformation(string.Format("Writing report to {0}", blob.Uri.AbsoluteUri));

            blob.Properties.ContentType = "json";
            await blob.UploadTextAsync(report.ToString(formatting));

            Trace.TraceInformation(string.Format("Wrote report to {0}", blob.Uri.AbsoluteUri));
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {

            WarehouseConnection =
                    new SqlConnectionStringBuilder(
                        JobConfigurationManager.GetArgument(jobArgsDictionary,
                            JobArgumentNames.DestinationDatabase,
                            EnvironmentVariableKeys.SqlWarehouse));
            Destination = CloudStorageAccount.Parse(
                                       JobConfigurationManager.GetArgument(jobArgsDictionary,
                                           JobArgumentNames.PrimaryDestination, EnvironmentVariableKeys.StoragePrimary));

            DestinationContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName) ?? DefaultContainerName;


            DestinationContainer = Destination.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);

            string rankingCountString = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.RankingCount);

            if (string.IsNullOrEmpty(rankingCountString))
            {
                RankingCount = DefaultRankingCount;
            }
            else
            {
                RankingCount = Convert.ToInt32(rankingCountString);
            }
            
            return true;

        }

    }

}



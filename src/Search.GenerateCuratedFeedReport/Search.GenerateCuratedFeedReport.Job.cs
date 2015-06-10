using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Jobs.Common;
using NuGet.Versioning;

namespace Search.GenerateCuratedFeedReport
{
    internal class Job : JobBase
    {
        private const string GetCuratedPackagesScript = @"-- Work Service / Search.GenerateCuratedFeedReport 
         SELECT pr.[Id], cf.[Name] FROM [dbo].[PackageRegistrations] pr Inner join CuratedPackages cp on cp.PackageRegistrationKey = pr.[Key] join CuratedFeeds cf on cp.[CuratedFeedKey] = cf.[Key] group by  cf.[Name], pr.[Id]";

        public static readonly string DefaultContainerName = "ng-search-data";
        public static readonly string ReportName = "curatedfeeds.json";


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

            Trace.TraceInformation(String.Format("Generating Curated feed report from {0}/{1} to {2}.", Source.DataSource, Source.InitialCatalog, destination));


            IList<CuratedFeedData> curatedPackages;
            using (var connection = await Source.ConnectTo())
            {
                curatedPackages = (await connection.QueryWithRetryAsync<CuratedFeedData>(GetCuratedPackagesScript)).ToList();
            }
            Trace.TraceInformation(String.Format("Gathered {0} rows of data.", curatedPackages.Count));

            //group based on Package Id.
            var grouped = curatedPackages.GroupBy(item => item.Id);

            JArray curatedFeeds = new JArray();
            foreach (var group in grouped)
            {
                JArray details = new JArray();
                details.Add(group.Key);
                foreach (var gv in group)
                {
                    JArray feedName = new JArray(gv.Name);
                    details.Add(feedName);
                }
                curatedFeeds.Add(details);
            }
            await WriteReport(curatedFeeds.ToString(Formatting.None), ReportName, Formatting.None);
            return true;
        }

        protected async Task WriteReport(string report, string name, Formatting formatting)
        {
            if (!String.IsNullOrEmpty(OutputDirectory))
            {
                await WriteToFile(report, name);
            }
            else
            {
                await DestinationContainer.CreateIfNotExistsAsync();
                await WriteToBlob(report, name);
            }
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {

            Source =
                new SqlConnectionStringBuilder(
                    JobConfigManager.GetArgument(jobArgsDictionary,
                        JobArgumentNames.SourceDatabase,
                        EnvironmentVariableKeys.SqlGallery));

            OutputDirectory = JobConfigManager.GetArgument(jobArgsDictionary,
                       JobArgumentNames.OutputDirectory);

            if (string.IsNullOrEmpty(OutputDirectory))
            {
                Destination = CloudStorageAccount.Parse(
                                           JobConfigManager.GetArgument(jobArgsDictionary,
                                               JobArgumentNames.PrimaryDestination, EnvironmentVariableKeys.StoragePrimary));

                DestinationContainerName = JobConfigManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName) ?? DefaultContainerName;


                DestinationContainer = Destination.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);
            }

            return true;

        }

        #region PrivateMembers

        private async Task WriteToFile(string report, string name)
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
                await writer.WriteAsync(report);
            }

            Trace.TraceInformation(String.Format("Wrote report to {0}", fullPath));
        }

        private async Task WriteToBlob(string report, string name)
        {
            var blob = DestinationContainer.GetBlockBlobReference(name);
            Trace.TraceInformation(String.Format("Writing report to {0}", blob.Uri.AbsoluteUri));

            blob.Properties.ContentType = "json";
            await blob.UploadTextAsync(report);

            Trace.TraceInformation(String.Format("Wrote report to {0}", blob.Uri.AbsoluteUri));
        }

        #endregion PrivateMembers

        public class CuratedFeedData
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }
    }

}



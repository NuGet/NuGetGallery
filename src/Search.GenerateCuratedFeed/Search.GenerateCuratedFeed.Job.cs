using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Jobs.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Search.GenerateCuratedFeed
{
    internal class Job : JobBase
    {
        public const string DefaultContainerName = "ng-search-data";
        public const string ReportName = "curatedfeeds.json";

        public SqlConnectionStringBuilder Source { get; set; }
        public CloudStorageAccount Destination { get; set; }
        public CloudBlobContainer DestinationContainer { get; set; }
        public string DestinationContainerName { get; set; }
        public string OutputDirectory { get; set; }

        public override async Task<bool> Run()
        {
            Trace.TraceInformation(String.Format("Generating Curated feed report from {0}/{1}.", Source.DataSource, Source.InitialCatalog));

            using (SqlConnection connection = new SqlConnection(Source.ConnectionString))
            {
                connection.Open();

                string sql = LoadResource("Scripts.CuratedFeed.sql");

                SqlCommand command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;

                SqlDataReader reader = command.ExecuteReader();

                JArray result = JsonHelper.SqlDataReader2Json(reader, "FeedName", "Id");

                Trace.TraceInformation(String.Format("Gathered {0} rows of data.", result.Count));

                await WriteReport(result.ToString(Formatting.None), ReportName, Formatting.None);
            }

            return true;
        }

        static string LoadResource(string resourceName)
        {
            string name = Assembly.GetExecutingAssembly().GetName().Name;
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name + "." + resourceName);
            return new StreamReader(stream).ReadToEnd();
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

            blob.Properties.ContentType = "application/json";
            await blob.UploadTextAsync(report);

            Trace.TraceInformation(String.Format("Wrote report to {0}", blob.Uri.AbsoluteUri));
        }

        #endregion PrivateMembers
    }

}


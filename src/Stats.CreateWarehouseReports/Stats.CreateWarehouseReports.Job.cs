using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Data;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using NuGet.Jobs.Common;
using Stats.CreateWarehouseReports.Helpers;
using Stats.CreateWarehouseReports.Resources;

namespace Stats.CreateWarehouseReports
{
    internal class Job : JobBase
    {
        private const string PackageReportBaseName = "recentpopularity_";
        private const string NuGetClientVersion = "nugetclientversion";
        private const string Last6Months = "last6months";
        private const string RecentPopularity = "recentpopularity";
        private const string RecentPopularityDetail = "recentpopularitydetail";
        private const string PackageReportDetailBaseName = "recentpopularitydetail_";
        private const string DefaultPackageStatsContainerName = "stats";

        private Dictionary<string, Func<Task>> _globalReportBuilders;
        public Job() : base() { }

        /// <summary>
        /// Gets or sets a connection string to the database containing package data.
        /// </summary>
        public SqlConnectionStringBuilder Source { get; set; }

        public bool RebuildAll { get; set; }
        public SqlConnectionStringBuilder WarehouseConnection { get; set; }
        public CloudStorageAccount Destination { get; set; }
        public string DestinationContainerName { get; set; }
        public string OutputDirectory { get; set; }
        public string ReportName { get; set; }

        protected CloudBlobContainer DestinationContainer { get; set; }


        public override async Task<bool> Run()
        {
            try
            {
                if (!String.IsNullOrEmpty(OutputDirectory))
                {
                    Trace.TraceInformation(String.Format("Generating reports from {0}/{1} and saving to {2}/{3}", Source.DataSource, Source.InitialCatalog, "local file system", OutputDirectory));
                }
                else if (Destination != null)
                {
                    Trace.TraceInformation(String.Format("Generating reports from {0}/{1} and saving to {2}/{3}", Source.DataSource, Source.InitialCatalog, Destination.Credentials.AccountName, DestinationContainer.Name));
                }
                else
                {
                    throw new InvalidOperationException(Strings.WarehouseJob_NoDestinationAvailable);
                }

                if (String.IsNullOrEmpty(ReportName))
                {
                    // Generate all reports
                    foreach (var reportBuilder in _globalReportBuilders.Values)
                    {
                        await reportBuilder();
                    }

                    if (RebuildAll)
                    {
                        await RebuildPackageReports(all: true);
                    }
                    else
                    {
                        await RebuildPackageReports(all: false);
                        await CleanInactivePackageReports();
                    }
                }
                else
                {
                    // Just generate that report
                    Func<Task> generator;
                    if (_globalReportBuilders.TryGetValue(ReportName, out generator))
                    {
                        await generator();
                    }
                    else
                    {
                        throw new InvalidOperationException(String.Format(Strings.CreateWarehouseReportsJob_UnknownReport, ReportName));
                    }
                }

                if (!String.IsNullOrEmpty(OutputDirectory))
                {
                    Trace.TraceInformation(String.Format("Generated reports from {0}/{1} and saving to {2}/{3}", Source.DataSource, Source.InitialCatalog, "local file system", OutputDirectory));
                }
                else
                {
                    Trace.TraceInformation(String.Format("Generated reports from {0}/{1} and saving to {2}/{3}", Source.DataSource, Source.InitialCatalog, Destination.Credentials.AccountName, DestinationContainer.Name));
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return false;
            }

            return true;
        }

        private async Task RebuildPackageReports(bool all)
        {
            IList<WarehousePackageReference> packages;
            using (var connection = await WarehouseConnection.ConnectTo())
            {
                if (all)
                {
                    Trace.TraceInformation("Getting list of all packages.");
                    packages = (await connection.QueryAsync<WarehousePackageReference>("SELECT DISTINCT packageId AS PackageId, NULL as DirtyCount FROM Dimension_Package")).ToList();
                }
                else
                {
                    Trace.TraceInformation("Getting list of packages in need of update.");
                    packages = (await connection.QueryAsync<WarehousePackageReference>("GetPackagesForExport", commandType: CommandType.StoredProcedure)).ToList();
                }
                Trace.TraceInformation(String.Format("Found {0} packages to update.", packages.Count));
            }

            Parallel.ForEach(packages, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, package =>
            {
                //CreateReport(
                //    PackageReportDetailBaseName + package.PackageId.ToLowerInvariant(),
                //    "Scripts.DownloadReport_RecentPopularityDetailByPackage.sql",
                //    t =>
                //    {
                //        var jobj = MakeReportJson(t);
                //        TotalDownloads(jobj);
                //        SortItems(jobj);
                //        return jobj.ToString(JsonFormat.SerializerSettings.Formatting);
                //    },
                //    Tuple.Create("@PackageId", 128, package.PackageId)).Wait();
                if (!all)
                {
                    ConfirmPackageExport(package).Wait();
                }
            });
        }

        private async Task ConfirmPackageExport(WarehousePackageReference package)
        {
            Trace.TraceInformation(String.Format("{0}: Marked Package Exported", package.PackageId));

            using (var connection = await WarehouseConnection.ConnectTo())
            {
                await connection.QueryAsync<int>(
                    "ConfirmPackageExported",
                    param: new { PackageId = package.PackageId, DirtyCount = package.DirtyCount },
                    commandType: CommandType.StoredProcedure);

            }
            Trace.TraceInformation(String.Format("{0}: Marking Package Exported", package.PackageId));
        }

        private async Task CleanInactivePackageReports()
        {
            Trace.TraceInformation("Getting list of inactive packages.");
            IList<string> packageIds;
            using (var connection = await WarehouseConnection.ConnectTo())
            {
                string sql = await ResourceHelpers.ReadResourceFile("Scripts.DownloadReport_ListInactive.sql");
                packageIds = (await connection.QueryAsync<string>(sql)).ToList();
            }
            Trace.TraceInformation(String.Format("Found {0} inactive packages.", packageIds.Count));

            // Collect the list of reports
            Trace.TraceInformation("Collecting list of package detail reports");
            IEnumerable<string> reports;
            if (!String.IsNullOrEmpty(OutputDirectory))
            {
                reports = Directory.EnumerateFiles(OutputDirectory, PackageReportDetailBaseName + "*.json").Select(Path.GetFileNameWithoutExtension);
            }
            else
            {
                reports = DestinationContainer.ListBlobs("popularity/" + PackageReportDetailBaseName)
                    .OfType<CloudBlockBlob>()
                    .Select(b => b.Name);
            }
            var reportSet = new HashSet<string>(reports);
            Trace.TraceInformation(String.Format("Collected {0} package detail reports", reportSet.Count));

            Parallel.ForEach(packageIds, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, id =>
            {
                string reportName = PackageReportDetailBaseName + id;
                if (!String.IsNullOrEmpty(OutputDirectory))
                {
                    if (reportSet.Contains(reportName))
                    {
                        string fullPath = Path.Combine(OutputDirectory, reportName + ".json");
                        Trace.TraceInformation(String.Format("{0}: Delet empty report from {1}", reportName, fullPath));
                        if (File.Exists(fullPath))
                        {
                            File.Delete(fullPath);
                        }
                        Trace.TraceInformation(String.Format("{0}: Deleting empty report from {1}", reportName, fullPath));
                    }
                }
                else
                {
                    string blobName = "popularity/" + reportName + ".json";
                    if (reportSet.Contains(blobName))
                    {
                        var blob = DestinationContainer.GetBlockBlobReference(blobName);
                        Trace.TraceInformation(String.Format("{0}: Deleting empty report from {1}", reportName, blob.Uri.AbsoluteUri));

                        blob.DeleteIfExists();

                        Trace.TraceInformation(String.Format("{0}: Deleted empty report from {1}", reportName, blob.Uri.AbsoluteUri));
                    }
                }
            });
        }

        private Task CreateReport(string reportName, string scriptName, params Tuple<string, int, string>[] parameters)
        {
            return CreateReport(reportName, scriptName, table => JsonConvert.SerializeObject(table, Formatting.Indented), parameters);
        }

        private async Task CreateReport(string reportName, string scriptName, Func<DataTable, string> jsonSerializer, params Tuple<string, int, string>[] parameters)
        {
            Trace.TraceInformation(String.Format("{0}: Collected {1} rows", reportName, scriptName));

            DataTable table = await CollectReportData(reportName, scriptName, parameters);

            // Transform the data table to JSON and process it with any provided transforms
            Trace.TraceInformation(String.Format("{0}: Processing report", reportName));
            string json = jsonSerializer(table);
            Trace.TraceInformation(String.Format("{0}: Proceesed report", reportName));

            await WriteReport(reportName, json);
            Trace.TraceInformation(String.Format("{0}: Generated report from SQL Script {1}", reportName, scriptName));
        }

        private async Task<DataTable> CollectReportData(string reportName, string scriptName, params Tuple<string, int, string>[] parameters)
        {
            Trace.TraceInformation(String.Format("{0}: Collecting data", reportName));
            DataTable table = null;
            using (var connection = await WarehouseConnection.ConnectTo())
            {
                // Get the data
                await WithRetry(async () =>
                {
                    table = await ExecuteSql(scriptName, parameters);
                });
            }
            Debug.Assert(table != null);
            Trace.TraceInformation(String.Format("{0}: Collected {1} rows", reportName, table.Rows.Count));
            return table;
        }

        private async Task WriteReport(string reportName, string json)
        {
            if (!String.IsNullOrEmpty(OutputDirectory))
            {
                await WriteToFile(reportName, json);
            }
            else
            {
                await WriteToBlob(reportName, json);
            }
        }

        private async Task WriteToBlob(string reportName, string json)
        {
            var blob = DestinationContainer.GetBlockBlobReference("popularity/" + reportName + ".json");
            Trace.TraceInformation(String.Format("{0}: Writing report to {1}", reportName, blob.Uri.AbsoluteUri));
            //blob.Properties.ContentType = MimeTypes.Json;
            await blob.UploadTextAsync(json);

            Trace.TraceInformation(String.Format("{0}: Wrote report to {1}", reportName, blob.Uri.AbsoluteUri));
        }

        private async Task WriteToFile(string reportName, string json)
        {
            string fullPath = Path.Combine(OutputDirectory, reportName + ".json");
            Trace.TraceInformation(String.Format("{0}: Writing report to {1}", reportName, fullPath));

            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            using (var writer = new StreamWriter(File.OpenWrite(fullPath)))
            {
                await writer.WriteAsync(json);
            }

            Trace.TraceInformation(String.Format("{0}: Wrote report to {1}", reportName, fullPath));
        }

        private async Task WithRetry(Func<Task> action)
        {
            int attempts = 10;

            while (attempts-- > 0)
            {
                Exception caught = null;
                try
                {
                    await action();
                    break;
                }
                catch (Exception ex)
                {
                    if (attempts == 1)
                    {
                        throw;
                    }
                    else
                    {
                        caught = ex;
                    }
                }
                if (caught != null)
                {
                    SqlConnection.ClearAllPools();
                    Trace.TraceInformation(String.Format("SQL Invocation failed, retrying. {0} attempts remaining. Exception: {1}", attempts, caught.ToString()));
                    await Task.Delay(20 * 1000);
                }
            }
        }

        // We don't use Dapper because we need a general purpose method to load any resultset
        // This method loads a tuple where the first item is a 
        private async Task<DataTable> ExecuteSql(string scriptName, params Tuple<string, int, string>[] parameters)
        {
            string sql = await ResourceHelpers.ReadResourceFile(scriptName);

            using (SqlConnection connection = await WarehouseConnection.ConnectTo())
            {
                SqlCommand command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60 * 5;

                foreach (Tuple<string, int, string> parameter in parameters)
                {
                    command.Parameters.Add(parameter.Item1, SqlDbType.NVarChar, parameter.Item2).Value = parameter.Item3;
                }

                var table = new DataTable();
                var reader = await command.ExecuteReaderAsync();
                table.Load(reader);
                return table;
            }
        }

        private static int TotalDownloads(JObject report)
        {
            JToken token;
            if (report.TryGetValue("Items", out token))
            {
                if (token is JArray)
                {
                    int total = 0;
                    for (int i = 0; i < ((JArray)token).Count; i++)
                    {
                        total += TotalDownloads((JObject)((JArray)token)[i]);
                    }
                    report["Downloads"] = total;
                    return total;
                }
                else
                {
                    int total = 0;
                    foreach (KeyValuePair<string, JToken> child in ((JObject)token))
                    {
                        total += TotalDownloads((JObject)child.Value);
                    }
                    report["Downloads"] = total;
                    return total;
                }
            }
            return (int)report["Downloads"];
        }

        private static void SortItems(JObject report)
        {
            List<Tuple<int, JObject>> scratch = new List<Tuple<int, JObject>>();

            foreach (KeyValuePair<string, JToken> child in ((JObject)report["Items"]))
            {
                scratch.Add(new Tuple<int, JObject>((int)child.Value["Downloads"], new JObject((JObject)child.Value)));
            }

            scratch.Sort((x, y) => { return x.Item1 == y.Item1 ? 0 : x.Item1 < y.Item1 ? 1 : -1; });

            JArray items = new JArray();

            foreach (Tuple<int, JObject> item in scratch)
            {
                items.Add(item.Item2);
            }

            report["Items"] = items;
        }

        public static JObject MakeReportJson(DataTable data)
        {
            JObject report = new JObject();

            report.Add("Downloads", 0);

            JObject items = new JObject();

            foreach (DataRow row in data.Rows)
            {
                string packageVersion = (string)row[0];

                JObject childReport;
                JToken token;
                if (items.TryGetValue(packageVersion, out token))
                {
                    childReport = (JObject)token;
                }
                else
                {
                    childReport = new JObject();
                    childReport.Add("Downloads", 0);
                    childReport.Add("Items", new JArray());
                    childReport.Add("Version", packageVersion);

                    items.Add(packageVersion, childReport);
                }

                JObject obj = new JObject();

                if (row[1].ToString() == "NuGet" || row[1].ToString() == "WebMatrix")
                {
                    obj.Add("Client", string.Format("{0} {1}.{2}", row[2], row[3], row[4]));
                    obj.Add("ClientName", row[2].ToString());
                    obj.Add("ClientVersion", string.Format("{0}.{1}", row[3], row[4]));
                }
                else
                {
                    obj.Add("Client", row[2].ToString());
                    obj.Add("ClientName", row[2].ToString());
                    obj.Add("ClientVersion", "");
                }

                if (row[5].ToString() != "(unknown)")
                {
                    obj.Add("Operation", row[5].ToString());
                }

                obj.Add("Downloads", (int)row[6]);

                ((JArray)childReport["Items"]).Add(obj);
            }

            report.Add("Items", items);

            return report;
        }

        public class WarehousePackageReference
        {
            public string PackageId { get; set; }
            public int? DirtyCount { get; set; }
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {

            Source =
                new SqlConnectionStringBuilder(
                    JobConfigManager.GetArgument(jobArgsDictionary,
                        JobArgumentNames.SourceDatabase,
                        EnvironmentVariableKeys.SqlWarehouse));

            Destination = CloudStorageAccount.Parse(
                                        JobConfigManager.GetArgument(jobArgsDictionary,
                                            JobArgumentNames.WarehouseStorageAccount, EnvironmentVariableKeys.WarehouseStorage));

            DestinationContainerName = JobConfigManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.WarehouseContainerName) ?? DefaultPackageStatsContainerName;

            DestinationContainer = Destination.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);
            _globalReportBuilders = new Dictionary<string, Func<Task>>(StringComparer.OrdinalIgnoreCase) {
                { NuGetClientVersion, () => CreateReport(NuGetClientVersion, "Scripts.DownloadReport_NuGetClientVersion.sql") },
                { Last6Months, () => CreateReport(Last6Months, "Scripts.DownloadReport_Last6Months.sql") },
                { RecentPopularity, () => CreateReport(RecentPopularity, "Scripts.DownloadReport_RecentPopularity.sql") },
                { RecentPopularityDetail, () => CreateReport(RecentPopularityDetail, "Scripts.DownloadReport_RecentPopularityDetail.sql") },
            };

            return true;

        }

    }
}
    

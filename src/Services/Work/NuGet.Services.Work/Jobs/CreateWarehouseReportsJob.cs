using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Configuration;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Storage;
using NuGet.Services.Work.Helpers;
using System.Data;
using NuGet.Services.Client;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Work.Jobs
{
    public class CreateWarehouseReportsJob : JobHandler<CreateWarehouseReportsEventSource>
    {
        private const string PackageReportBaseName = "recentpopularity_";
        private const string NuGetClientVersion = "nugetclientversion";
        private const string Last6Months = "last6months";
        private const string RecentPopularity = "recentpopularity";
        private const string RecentPopularityDetail = "recentpopularitydetail";
        private const string PackageReportDetailBaseName = "recentpopularitydetail_";

        private List<Func<Task>> _globalReportBuilders;

        public bool RebuildAll { get; set; }
        public SqlConnectionStringBuilder WarehouseConnection { get; set; }
        public CloudStorageAccount Destination { get; set; }
        public string DestinationContainerName { get; set; }
        public string OutputDirectory { get; set; }

        protected ConfigurationHub Config { get; set; }
        protected CloudBlobContainer DestinationContainer { get; set; }


        public CreateWarehouseReportsJob(ConfigurationHub config)
        {
            Config = config;

            _globalReportBuilders = new List<Func<Task>>() {
                () => CreateReport(NuGetClientVersion, "NuGet.Services.Work.Jobs.Scripts.DownloadReport_NuGetClientVersion.sql"),
                () => CreateReport(Last6Months, "NuGet.Services.Work.Jobs.Scripts.DownloadReport_Last6Months.sql"),
                () => CreateReport(RecentPopularity, "NuGet.Services.Work.Jobs.Scripts.DownloadReport_RecentPopularityDetail.sql"),
                () => CreateReport(RecentPopularityDetail, "NuGet.Services.Work.Jobs.Scripts.DownloadReport_RecentPopularity.sql")
            };
        }

        protected internal override async Task Execute()
        {
            LoadDefaults();

            if (!String.IsNullOrEmpty(OutputDirectory))
            {
                Log.GeneratingReports(WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog, "local file system", OutputDirectory);
            }
            else if (Destination != null)
            {
                Log.GeneratingReports(WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog, Destination.Credentials.AccountName, DestinationContainer.Name);
            }
            else
            {
                throw new InvalidOperationException(Strings.CreateWarehouseReportsJob_NoDestinationAvailable);
            }

            foreach (var reportBuilder in _globalReportBuilders)
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

            if (!String.IsNullOrEmpty(OutputDirectory))
            {
                Log.GeneratedReports(WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog, "local file system", OutputDirectory);
            }
            else
            {
                Log.GeneratedReports(WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog, Destination.Credentials.AccountName, DestinationContainer.Name);
            }
        }

        private async Task RebuildPackageReports(bool all)
        {
            IList<WarehousePackageReference> packages;
            using (var connection = await WarehouseConnection.ConnectTo())
            {
                if (all)
                {
                    Log.GettingAllPackages();
                    packages = (await connection.QueryAsync<WarehousePackageReference>("SELECT DISTINCT packageId AS PackageId, NULL as DirtyCount FROM Dimension_Package")).ToList();
                }
                else
                {
                    Log.GettingPackagesInNeedOfUpdate();
                    packages = (await connection.QueryAsync<WarehousePackageReference>("GetPackagesForExport", commandType: CommandType.StoredProcedure)).ToList();
                }
                Log.GotPackages(packages.Count);
            }

            Parallel.ForEach(packages, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, package =>
            {
                CreateReport(
                    PackageReportDetailBaseName + package.PackageId.ToLowerInvariant(),
                    "NuGet.Services.Work.Jobs.Scripts.DownloadReport_RecentPopularityDetailByPackage.sql",
                    t =>
                    {
                        var jobj = MakeReportJson(t);
                        TotalDownloads(jobj);
                        SortItems(jobj);
                        return Task.FromResult(jobj.ToString(JsonFormat.SerializerSettings.Formatting));
                    },
                    Tuple.Create("@PackageId", 128, package.PackageId)).Wait();
                if (!all)
                {
                    ConfirmPackageExport(package).Wait();
                }
            });
        }

        private async Task ConfirmPackageExport(WarehousePackageReference package)
        {
            Log.MarkingPackageExported(package.PackageId);
            if (!WhatIf)
            {
                using (var connection = await WarehouseConnection.ConnectTo())
                {
                    await connection.QueryAsync<int>(
                        "ConfirmPackageExported",
                        param: new { PackageId = package.PackageId, DirtyCount = package.DirtyCount },
                        commandType: CommandType.StoredProcedure);
                }
            }
            Log.MarkedPackageExported(package.PackageId);
        }

        private async Task CleanInactivePackageReports()
        {
            Log.GettingInactivePackages();
            IList<string> packageIds;
            using (var connection = await WarehouseConnection.ConnectTo())
            {
                string sql = await ResourceHelpers.ReadResourceFile("NuGet.Services.Work.Jobs.Scripts.DownloadReport_ListInactive.sql");
                packageIds = (await connection.QueryAsync<string>(sql)).ToList();
            }
            Log.GotInactivePackages(packageIds.Count);

            // Collect the list of reports
            Log.CollectingReportList();
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
            Log.CollectedReportList(reportSet.Count);

            Parallel.ForEach(packageIds, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, id =>
            {
                string reportName = PackageReportDetailBaseName + id;
                if (!String.IsNullOrEmpty(OutputDirectory))
                {
                    if(reportSet.Contains(reportName)) {
                        string fullPath = Path.Combine(OutputDirectory, reportName + ".json");
                        Log.DeletingReport(reportName, fullPath);
                        if (!WhatIf && File.Exists(fullPath))
                        {
                            File.Delete(fullPath);
                        }
                        Log.DeletedReport(reportName, fullPath);
                    }
                }
                else
                {
                    string blobName = "popularity/" + reportName + ".json";
                    if(reportSet.Contains(blobName)) {
                    var blob = DestinationContainer.GetBlockBlobReference(blobName);
                    Log.DeletingReport(reportName, blob.Uri.AbsoluteUri);
                    if (!WhatIf)
                    {
                        blob.DeleteIfExists();
                    }
                    Log.DeletedReport(reportName, blob.Uri.AbsoluteUri);
                    }
                }
            });
        }

        private Task CreateReport(string reportName, string scriptName, params Tuple<string, int, string>[] parameters)
        {
            return CreateReport(reportName, scriptName, table => JsonFormat.SerializeAsync(table, camelCase: false), parameters);
        }

        private async Task CreateReport(string reportName, string scriptName, Func<DataTable, Task<string>> jsonSerializer, params Tuple<string, int, string>[] parameters)
        {
            Log.GeneratingSingleReport(reportName, scriptName);

            DataTable table = await CollectReportData(reportName, scriptName, parameters);

            // Transform the data table to JSON and process it with any provided transforms
            Log.ProcessingReport(reportName);
            string json = await jsonSerializer(table);
            Log.ProcessedReport(reportName);

            await WriteReport(reportName, json);
            Log.GeneratedSingleReport(reportName, scriptName);
        }

        private async Task<DataTable> CollectReportData(string reportName, string scriptName, params Tuple<string, int, string>[] parameters)
        {
            Log.CollectingReportData(reportName);
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
            Log.CollectedReportData(reportName, table.Rows.Count);
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
            Log.WritingReport(reportName, blob.Uri.AbsoluteUri);
            if (!WhatIf)
            {
                blob.Properties.ContentType = MimeTypes.Json;
                await blob.UploadTextAsync(json);
            }
            Log.WroteReport(reportName, blob.Uri.AbsoluteUri);
        }

        private async Task WriteToFile(string reportName, string json)
        {
            string fullPath = Path.Combine(OutputDirectory, reportName + ".json");
            Log.WritingReport(reportName, fullPath);
            if (!WhatIf)
            {
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
            }
            Log.WroteReport(reportName, fullPath);
        }

        private void LoadDefaults()
        {
            WarehouseConnection = WarehouseConnection ?? Config.Sql.Warehouse;
            Destination = Destination ?? Config.Storage.Legacy;
            if (Destination != null)
            {
                DestinationContainer = Destination.CreateCloudBlobClient().GetContainerReference(
                    String.IsNullOrEmpty(DestinationContainerName) ? BlobContainerNames.LegacyStats : DestinationContainerName);
            }
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
                    Log.RetryingSqlInvocation(attempts, caught.ToString());
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
    }

    [EventSource(Name = "Outercurve-NuGet-Jobs-CreateWarehouseReports")]
    public class CreateWarehouseReportsEventSource : EventSource
    {
        public static readonly CreateWarehouseReportsEventSource Log = new CreateWarehouseReportsEventSource();
        private CreateWarehouseReportsEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Task = Tasks.GeneratingReports,
            Opcode = EventOpcode.Start,
            Message = "Generating reports from {0}/{1} and saving to {2}/{3}")]
        public void GeneratingReports(string dbServer, string db, string storageAccount, string container) { WriteEvent(1, dbServer, db, storageAccount, container); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Task = Tasks.GeneratingReports,
            Opcode = EventOpcode.Stop,
            Message = "Generated reports from {0}/{1} and saved to {2}/{3}")]
        public void GeneratedReports(string dbServer, string db, string storageAccount, string container) { WriteEvent(2, dbServer, db, storageAccount, container); }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Task = Tasks.GeneratingSingleReport,
            Opcode = EventOpcode.Start,
            Message = "{0}: Generating report from SQL Script {1}")]
        public void GeneratingSingleReport(string reportName, string scriptName) { WriteEvent(3, reportName, scriptName); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Task = Tasks.GeneratingSingleReport,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Generated report from SQL Script {1}")]
        public void GeneratedSingleReport(string reportName, string scriptName) { WriteEvent(4, reportName, scriptName); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Task = Tasks.GeneratingSingleReport,
            Opcode = EventOpcode.Start,
            Message = "{0}: Collecting data")]
        public void CollectingReportData(string reportName) { WriteEvent(5, reportName); }

        [Event(
            eventId: 6,
            Level = EventLevel.Informational,
            Task = Tasks.GeneratingSingleReport,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Collected {1} rows")]
        public void CollectedReportData(string reportName, int rowCount) { WriteEvent(6, reportName, rowCount); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Task = Tasks.WritingReport,
            Opcode = EventOpcode.Start,
            Message = "{0}: Writing report to {1}")]
        public void WritingReport(string reportName, string blobUri) { WriteEvent(7, reportName, blobUri); }

        [Event(
            eventId: 8,
            Level = EventLevel.Informational,
            Task = Tasks.WritingReport,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Wrote report to {1}")]
        public void WroteReport(string reportName, string blobUri) { WriteEvent(8, reportName, blobUri); }

        [Event(
            eventId: 9,
            Level = EventLevel.Informational,
            Message = "Writing reports to '{0}' instead of blobs.")]
        public void WritingToOutputDirectory(string directory) { WriteEvent(9, directory); }

        // EventID 10 was accidentally skipped. Just leave it :)

        [Event(
            eventId: 11,
            Level = EventLevel.Informational,
            Task = Tasks.CollectingPackageList,
            Opcode = EventOpcode.Start,
            Message = "Getting list of packages in need of update.")]
        public void GettingPackagesInNeedOfUpdate() { WriteEvent(11); }

        [Event(
            eventId: 12,
            Level = EventLevel.Informational,
            Task = Tasks.CollectingPackageList,
            Opcode = EventOpcode.Start,
            Message = "Getting list of all packages.")]
        public void GettingAllPackages() { WriteEvent(12); }

        [Event(
            eventId: 13,
            Level = EventLevel.Informational,
            Task = Tasks.CollectingPackageList,
            Opcode = EventOpcode.Stop,
            Message = "Found {0} packages to update.")]
        public void GotPackages(int count) { WriteEvent(13, count); }

        [Event(
            eventId: 14,
            Level = EventLevel.Informational,
            Task = Tasks.ProcessingReport,
            Opcode = EventOpcode.Start,
            Message = "{0}: Processing report")]
        public void ProcessingReport(string reportName) { WriteEvent(14, reportName); }

        [Event(
            eventId: 15,
            Level = EventLevel.Informational,
            Task = Tasks.ProcessingReport,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Processed report")]
        public void ProcessedReport(string reportName) { WriteEvent(15, reportName); }

        [Event(
            eventId: 16,
            Level = EventLevel.Informational,
            Task = Tasks.MarkingPackageExported,
            Opcode = EventOpcode.Start,
            Message = "{0}: Marking Package Exported")]
        public void MarkingPackageExported(string packageId) { WriteEvent(16, packageId); }

        [Event(
            eventId: 17,
            Level = EventLevel.Informational,
            Task = Tasks.MarkingPackageExported,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Marked Package Exported")]
        public void MarkedPackageExported(string packageId) { WriteEvent(17, packageId); }

        [Event(
            eventId: 18,
            Level = EventLevel.Informational,
            Task = Tasks.CollectingInactivePackageIds,
            Opcode = EventOpcode.Start,
            Message = "Getting list of inactive packages.")]
        public void GettingInactivePackages() { WriteEvent(18); }

        [Event(
            eventId: 19,
            Level = EventLevel.Informational,
            Task = Tasks.CollectingInactivePackageIds,
            Opcode = EventOpcode.Stop,
            Message = "Found {0} inactive packages.")]
        public void GotInactivePackages(int count) { WriteEvent(19, count); }

        [Event(
            eventId: 20,
            Level = EventLevel.Informational,
            Message = "SQL Invocation failed, retrying. {0} attempts remaining. Exception: {1}")]
        public void RetryingSqlInvocation(int attemptsRemaining, string exception) { WriteEvent(20, attemptsRemaining, exception); }

        [Event(
            eventId: 21,
            Level = EventLevel.Informational,
            Task = Tasks.DeletingReport,
            Opcode = EventOpcode.Start,
            Message = "{0}: Deleting empty report from {1}")]
        public void DeletingReport(string reportName, string blobUri) { WriteEvent(21, reportName, blobUri); }

        [Event(
            eventId: 22,
            Level = EventLevel.Informational,
            Task = Tasks.DeletingReport,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Deleted empty report from {1}")]
        public void DeletedReport(string reportName, string blobUri) { WriteEvent(22, reportName, blobUri); }

        [Event(
            eventId: 23,
            Level = EventLevel.Informational,
            Task = Tasks.CollectingReportList,
            Opcode = EventOpcode.Start,
            Message = "Collecting list of package detail reports")]
        public void CollectingReportList() { WriteEvent(23); }

        [Event(
            eventId: 24,
            Level = EventLevel.Informational,
            Task = Tasks.CollectingReportList,
            Opcode = EventOpcode.Stop,
            Message = "Collected {0} package detail reports")]
        public void CollectedReportList(int reportCount) { WriteEvent(24, reportCount); }

        public static class Tasks
        {
            public const EventTask GeneratingReports = (EventTask)0x1;
            public const EventTask GeneratingSingleReport = (EventTask)0x2;
            public const EventTask CollectingReportData = (EventTask)0x3;
            public const EventTask WritingReport = (EventTask)0x4;
            public const EventTask CollectingPackageList = (EventTask)0x5;
            public const EventTask ProcessingReport = (EventTask)0x6;
            public const EventTask MarkingPackageExported = (EventTask)0x7;
            public const EventTask CollectingInactivePackageIds = (EventTask)0x8;
            public const EventTask DeletingReport = (EventTask)0x9;
            public const EventTask CollectingReportList = (EventTask)0xA;
        }
    }
}

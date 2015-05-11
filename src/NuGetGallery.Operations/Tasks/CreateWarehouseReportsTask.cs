// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("createwarehousereports", "Create warehouse reports", AltName = "cwrep")]
    public class CreateWarehouseReportsTask : DatabaseAndStorageTask
    {
        private const string JsonContentType = "application/json";
        private const string PackageReportBaseName = "recentpopularity_";
        private const string NuGetClientVersion = "nugetclientversion";
        private const string Last6Months = "last6months";
        private const string RecentPopularity = "recentpopularity";
        private const string RecentPopularityDetail = "recentpopularitydetail";
        private const string PackageReportDetailBaseName = "recentpopularitydetail_";

        [Option("Re-create all reports", AltName = "all")]
        public bool All { get; set; }

        public override void ExecuteCommand()
        {
            Log.Info("Generate reports begin");

            CreateContainerIfNotExists();

            CreateReport_NuGetClientVersion();
            CreateReport_Last6Months();
            CreateReport_RecentPopularityDetail();
            CreateReport_RecentPopularity();

            if (All)
            {
                CreateAllPerPackageReports();
            }
            else
            {
                CreateDirtyPerPackageReports();
                ClearInactivePackageReports();
            }

            Log.Info("Generate reports end");
        }

        private void CreateReport_NuGetClientVersion()
        {
            Log.Info("CreateReport_NuGetClientVersion");

            Tuple<string[], List<object[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_NuGetClientVersion.sql");

            CreateBlob(NuGetClientVersion + ".json", JsonContentType, ReportHelpers.ToJson(report));
        }

        private void CreateReport_Last6Months()
        {
            Log.Info("CreateReport_Last6Months");

            Tuple<string[], List<object[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_Last6Months.sql");

            CreateBlob(Last6Months + ".json", JsonContentType, ReportHelpers.ToJson(report));
        }

        private void CreateReport_RecentPopularityDetail()
        {
            Log.Info("CreateReport_RecentPopularityDetail");

            Tuple<string[], List<object[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_RecentPopularityDetail.sql");

            CreateBlob(RecentPopularityDetail + ".json", JsonContentType, ReportHelpers.ToJson(report));
        }

        private void CreateReport_RecentPopularity()
        {
            Log.Info("CreateReport_RecentPopularity");

            Tuple<string[], List<object[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_RecentPopularity.sql");

            CreateBlob(RecentPopularity + ".json", JsonContentType, ReportHelpers.ToJson(report));

            CreatePerPackageReports(report);
        }

        private void CreatePerPackageReports(Tuple<string[], List<object[]>> report)
        {
            Log.Info(string.Format("CreatePerPackageReports (count = {0})", report.Item2.Count));

            int indexOfPackageId = 0;
            foreach (string column in report.Item1)
            {
                if (column == "PackageId")
                {
                    break;
                }
                indexOfPackageId++;
            }

            if (indexOfPackageId == report.Item1.Length)
            {
                throw new InvalidOperationException("expected PackageId in result");
            }

            foreach (object[] row in report.Item2)
            {
                string packageId = row[indexOfPackageId].ToString();
                WithRetry(() =>
                {
                    CreatePackageReport(packageId);
                });
            }
        }

        private void CreateAllPerPackageReports()
        {
            Log.Info("CreateAllPerPackageReports");

            DateTime before = DateTime.Now;

            IList<string> packageIds = GetAllPackageIds();

            string[] bag = new string[packageIds.Count];

            int index = 0;
            foreach (string packageId in packageIds)
            {
                bag[index++] = packageId;
            }

            ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism = 4 };

            Parallel.ForEach(bag, options, packageId =>
            {
                WithRetry(() =>
                {
                    CreatePackageReport(packageId);
                });
            });

            string msg = string.Format("CreateAllPerPackageReports complete {0} seconds", (DateTime.Now - before).TotalSeconds);

            Log.Info(msg);
        }

        private IList<string> GetAllPackageIds()
        {
            IList<string> packageIds = new List<string>();

            using (SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand("SELECT DISTINCT packageId FROM Dimension_Package", connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60 * 5;

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string packageId = reader.GetValue(0).ToString();
                    packageIds.Add(packageId);
                }
            }

            return packageIds;
        }

        private void CreateDirtyPerPackageReports()
        {
            Log.Info("CreateDirtyPerPackageReports");

            DateTime before = DateTime.Now;

            IList<Tuple<string, int>> packageIds = GetPackageIds();

            Log.Info(string.Format("Creating {0} Reports", packageIds.Count));

            Tuple<string, int>[] bag = new Tuple<string, int>[packageIds.Count];

            int index =0;
            foreach (Tuple<string, int> packageId in packageIds)
            {
                bag[index++] = packageId;
            }

            // limit the potential concurrency becasue this is against SQL

            ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism = 4 };

            Parallel.ForEach(bag, options, packageId =>
            {
                WithRetry(() =>
                {
                    CreatePackageReport(packageId.Item1);
                    
                    ConfirmExport(packageId);
                });
            });

            string msg = string.Format("CreateDirtyPerPackageReports complete {0} seconds", (DateTime.Now - before).TotalSeconds);

            Log.Info(msg);
        }

        private IList<Tuple<string, int>> GetPackageIds()
        {
            IList<Tuple<string, int>> packageIds = new List<Tuple<string, int>>();

            using (SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand("GetPackagesForExport", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 60 * 5;

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string packageId = reader.GetValue(0).ToString();
                    int dirtyCount = (int)reader.GetValue(1);

                    packageIds.Add(new Tuple<string, int>(packageId, dirtyCount));
                }
            }

            return packageIds;
        }

        //  for the initial release we will run New and Old reports in parallel
        //  (the difference is that new reports contain more details)
        //  then when we are happy with our new deployment we will drop the old

        private void CreatePackageReport(string packageId)
        {
            Log.Info(string.Format("CreatePackageReport for {0}", packageId));

            // All blob names use lower case identifiers in the NuGet Gallery Azure Blob Storage 

            string name = PackageReportDetailBaseName + packageId.ToLowerInvariant();

            JObject report = CreateJsonContent(packageId);

            CreateBlob(name + ".json", JsonContentType, ReportHelpers.ToStream(report));
        }

        private JObject CreateJsonContent(string packageId)
        {
            Tuple<string[], List<object[]>> data = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_RecentPopularityDetailByPackage.sql", new Tuple<string, int, string>("@packageId", 128, packageId));
            JObject content = MakeReportJson(data);
            TotalDownloads(content);
            SortItems(content);
            return content;
        }

        static JObject MakeReportJson(Tuple<string[], List<object[]>> data)
        {
            JObject report = new JObject();

            report.Add("Downloads", 0);

            JObject items = new JObject();

            foreach (object[] row in data.Item2)
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

        private void CreateEmptyPackageReport(string packageId)
        {
            Log.Info(string.Format("CreateEmptyPackageReport for {0}", packageId));

            // All blob names use lower case identifiers in the NuGet Gallery Azure Blob Storage 

            string name = PackageReportDetailBaseName + packageId.ToLowerInvariant();

            CreateBlob(name + ".json", JsonContentType, ReportHelpers.ToStream(new JObject()));
        }

        private void ClearInactivePackageReports()
        {
            Log.Info("ClearInactivePackageReports");

            IList<string> packageIds = GetInactivePackageIds();

            Log.Info(string.Format("Creating {0} empty Reports", packageIds.Count));

            string[] bag = new string[packageIds.Count];

            int index = 0;
            foreach (string packageId in packageIds)
            {
                bag[index++] = packageId;
            }

            Parallel.ForEach(bag, packageId =>
            {
                CreateEmptyPackageReport(packageId);
            });
        }

        private IList<string> GetInactivePackageIds()
        {
            string sql = ResourceHelper.GetBatchFromSqlFile("NuGetGallery.Operations.Scripts.DownloadReport_ListInactive.sql");

            IList<string> packageIds = new List<string>();

            using (SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60 * 5;

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string packageId = reader.GetValue(0).ToString();
                    packageIds.Add(packageId);
                }
            }

            return packageIds;
        }

        private void ConfirmExport(Tuple<string, int> packageId)
        {
            Log.Info(string.Format("ConfirmPackageExported for {0}", packageId.Item1));

            using (SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand("ConfirmPackageExported", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 60 * 5;
                command.Parameters.AddWithValue("PackageId", packageId.Item1);
                command.Parameters.AddWithValue("DirtyCount", packageId.Item2);

                command.ExecuteNonQuery();
            }
        }

        private Tuple<string[], List<object[]>> ExecuteSql(string filename, params Tuple<string, int, string>[] parameters)
        {
            string sql = ResourceHelper.GetBatchFromSqlFile(filename);

            List<object[]> rows = new List<object[]>();
            string[] columns;

            using (SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60 * 5;

                foreach (Tuple<string, int, string> parameter in parameters)
                {
                    command.Parameters.Add(parameter.Item1, SqlDbType.NVarChar, parameter.Item2).Value = parameter.Item3;
                }

                SqlDataReader reader = command.ExecuteReader();

                columns = new string[reader.FieldCount];

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns[i] = reader.GetName(i);
                }

                while (reader.Read())
                {
                    object[] row = new object[reader.FieldCount];

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }

                    rows.Add(row);
                }
            }

            return new Tuple<string[], List<object[]>>(columns, rows);
        }

        private void CreateContainerIfNotExists()
        {
            CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("stats");
            
            container.CreateIfNotExists();  // this can throw if the container was just deleted a few seconds ago
            
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
        }

        private Uri CreateBlob(string name, string contentType, Stream content)
        {
            CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("stats");
            CloudBlockBlob blockBlob = container.GetBlockBlobReference("popularity/" + name);

            blockBlob.Properties.ContentType = contentType;
            blockBlob.UploadFromStream(content);

            return blockBlob.Uri;
        }

        private void WithRetry(Action action)
        {
            int attempts = 10;

            while (attempts-- > 0)
            {
                try
                {
                    action();
                    break;
                }
                catch (Exception)
                {
                    if (attempts == 1)
                    {
                        throw;
                    }
                    else
                    {
                        SqlConnection.ClearAllPools();
                        Log.Info(string.Format("Retry attempts remaining {0}", attempts));
                        Thread.Sleep(20 * 1000);
                    }
                }
            }
        }
    }
}

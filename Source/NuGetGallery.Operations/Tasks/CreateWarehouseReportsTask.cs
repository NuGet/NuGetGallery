using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace NuGetGallery.Operations
{
    [Command("createwarehousereports", "Create warehouse reports", AltName = "cwrep")]
    public class CreateWarehouseReportsTask : OpsTask
    {
        private const string JsonContentType = "application/json";
        private const string PackageReportBaseName = "RecentPopularity_";

        [Option("Connection string to the warehouse database", AltName = "wdb")]
        public string WarehouseConnectionString { get; set; }

        [Option("Connection string to the warehouse reports container", AltName = "wracc")]
        public string ReportsConnectionString { get; set; }

        public CreateWarehouseReportsTask()
        {
            WarehouseConnectionString = Environment.GetEnvironmentVariable("NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING");
            ReportsConnectionString = Environment.GetEnvironmentVariable("NUGET_WAREHOUSE_REPORTS_STORAGE");
        }

        public override void ExecuteCommand()
        {
            Log.Info("Generate reports begin");

            CreateReport_PerMonth();
            CreateReport_RecentPopularity();
            CreateReport_RecentPopularityDetail();

            Log.Info("Generate reports end");
        }

        private void CreateReport_PerMonth()
        {
            Log.Info("CreateReport_PerMonth");

            Tuple<string[], List<string[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_PerMonth.sql");

            const string Name = "PerMonth";
            CreateBlob(Name + ".json", JsonContentType, ReportHelpers.ToJson(report));
        }

        private void CreateReport_RecentPopularity()
        {
            Log.Info("CreateReport_RecentPopularity");

            Tuple<string[], List<string[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_RecentPopularity.sql");

            const string Name = "RecentPopularity";
            CreateBlob(Name + ".json", JsonContentType, ReportHelpers.ToJson(report));

            //  and now generate the per package drill-down reports corresponding to this top level

            //  (1) get the current list because there maybe some to delete

            HashSet<string> currentPackageReports = FetchListOfCurrentPackageReports();

            //  (2) create the new reports, this will overwrite old with new reports

            CreatePerPackageReports(report, currentPackageReports);

            //  (3) clean up any left over old package reports

            DeleteOldPackageReports(currentPackageReports);
        }

        private HashSet<string> FetchListOfCurrentPackageReports()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ReportsConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("popularity");

            HashSet<string> packageReports = new HashSet<string>();

            foreach (CloudBlockBlob blockBlob in container.ListBlobs())
            {
                if (blockBlob.Name.StartsWith(PackageReportBaseName))
                {
                    string packageId = blockBlob.Name.Substring(PackageReportBaseName.Length);
                    packageId = packageId.Substring(0, packageId.LastIndexOf('.'));

                    packageReports.Add(packageId);
                }
            }

            return packageReports;
        }

        private void DeleteOldPackageReports(HashSet<string> packageReportsToDelete)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ReportsConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("popularity");

            foreach (string packageId in packageReportsToDelete)
            {
                string reportName = PackageReportBaseName + packageId;

                CloudBlockBlob jsonBlockBlob = container.GetBlockBlobReference(reportName + ".json");
                jsonBlockBlob.DeleteIfExists();

                Log.Info(string.Format("deleted reports for {0}", packageId));
            }
        }

        private void CreatePerPackageReports(Tuple<string[], List<string[]>> report, HashSet<string> packageReportsToDelete)
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

            foreach (string[] row in report.Item2)
            {
                string packageId = row[indexOfPackageId];

                CreatePackageReport(packageId);

                packageReportsToDelete.Remove(packageId);
            }
        }

        private void CreatePackageReport(string packageId)
        {
            Log.Info(string.Format("CreatePackageReport for {0}", packageId));

            string name = PackageReportBaseName + packageId;

            Tuple<string[], List<string[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_RecentPopularityByPackage.sql", new Tuple<string, string>("@packageId", packageId));

            CreateBlob(name + ".json", JsonContentType, ReportHelpers.ToJson(report));
        }

        private void CreateReport_RecentPopularityDetail()
        {
            Log.Info("CreateReport_RecentPopularityDetail");

            Tuple<string[], List<string[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_RecentPopularityDetail.sql");

            const string Name = "RecentPopularityDetail";
            CreateBlob(Name + ".json", JsonContentType, ReportHelpers.ToJson(report));
        }

        private Tuple<string[], List<string[]>> ExecuteSql(string filename, params Tuple<string, string>[] parameters)
        {
            string sql = ResourceHelper.GetBatchFromSqlFile(filename);

            List<string[]> rows = new List<string[]>();
            string[] columns;

            using (SqlConnection connection = new SqlConnection(WarehouseConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 180;

                foreach (Tuple<string, string> parameter in parameters)
                {
                    command.Parameters.AddWithValue(parameter.Item1, parameter.Item2);
                }

                SqlDataReader reader = command.ExecuteReader();

                columns = new string[reader.FieldCount];

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns[i] = reader.GetName(i);
                }

                while (reader.Read())
                {
                    string[] row = new string[reader.FieldCount];

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[i] = reader.GetValue(i).ToString();
                    }

                    rows.Add(row);
                }
            }

            return new Tuple<string[], List<string[]>>(columns, rows);
        }

        private Uri CreateBlob(string name, string contentType, Stream content)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ReportsConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("popularity");
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(name);

            blockBlob.Properties.ContentType = contentType;
            blockBlob.UploadFromStream(content);

            return blockBlob.Uri;
        }
    }
}


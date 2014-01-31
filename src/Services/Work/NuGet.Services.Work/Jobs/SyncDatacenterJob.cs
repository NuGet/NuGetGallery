using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Configuration;
using NuGet.Services.Work.DACWebService;
using System.Net;
using System.IO;
using NuGet.Services.Client;
using System.Runtime.Serialization;

namespace NuGet.Services.Work.Jobs
{
    [Description("Sync the secondary datacenter with the primary datacenter")]
    public class SyncDatacenterJob : DatabaseJobHandlerBase<SyncDatacenterEventSource>
    {
        private static readonly string NorthCentralUSUri = @"https://ch1prod-dacsvc.azure.com/DACWebService.svc";
        //private static readonly string EastUSUri = @"https://bl2prod-dacsvc.azure.com/DACWebService.svc";

        //private static readonly string ExportMethod = "Export";

        public string DestinationStorageAccountName { get; set; }
        
        public string DestinationStorageAccountKey { get; set; }

        public string RequestGUID { get; set; }

        public SyncDatacenterJob(ConfigurationHub configHub) : base(configHub) { }

        protected internal override Task<JobContinuation> Execute()
        {
            // Load Defaults
            var uri = NorthCentralUSUri;
            var cstr = TargetDatabaseConnection;

            if (cstr == null || cstr.InitialCatalog == null || cstr.Password == null || cstr.DataSource == null || cstr.UserID == null)
            {
                throw new ArgumentNullException("One of the connection string parameters or the string itself is null");
            }

            if (DestinationStorageAccountName == null)
            {
                throw new ArgumentNullException("Destination Storage Account Name is null");
            }

            if (DestinationStorageAccountKey == null)
            {
                throw new ArgumentNullException("Destination Storage Account Key is null");
            }

            WASDImportExport.ImportExportHelper helper = new WASDImportExport.ImportExportHelper(SyncDatacenterEventSource.Log)
            {
                EndPointUri = NorthCentralUSUri,
                DatabaseName = cstr.InitialCatalog,
                ServerName = cstr.DataSource,
                UserName = cstr.UserID,
                Password = cstr.Password,
                StorageKey = DestinationStorageAccountKey,
            };

            var blobAbsoluteUri = String.Format(@"https://{0}.blob.core.windows.net/bacpac-files/{1}-{2}.bacpac", DestinationStorageAccountName, helper.DatabaseName, DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss"));

            var requestGUID = helper.DoExport(blobAbsoluteUri, whatIf: false, async: true);

            Log.Information(String.Format("\n\n Request GUID is : {0}", requestGUID));

            var parameters = new Dictionary<string, string>();
            parameters["RequestGUID"] = requestGUID;
            parameters["TargetDatabaseConnection"] = cstr.ConnectionString;

            return Task.FromResult(Suspend(TimeSpan.FromSeconds(60), parameters));
        }

        protected internal override Task<JobContinuation> Resume()
        {
            if (RequestGUID == null || TargetDatabaseConnection == null)
            {
                throw new ArgumentNullException("Job could not resume properly due to incorrect parameters");
            }

            WASDImportExport.ImportExportHelper helper = new WASDImportExport.ImportExportHelper(SyncDatacenterEventSource.Log)
            {
                ServerName = TargetDatabaseConnection.DataSource,
                UserName = TargetDatabaseConnection.UserID,
                Password = TargetDatabaseConnection.Password,
            };

            var statusInfoList = helper.CheckRequestStatus(RequestGUID);
            var statusInfo = statusInfoList.FirstOrDefault();

            var exportComplete = false;

            if (statusInfo.Status == "Failed")
            {
                Log.Information(String.Format("Database export failed: {0}", statusInfo.ErrorMessage));
                exportComplete = true;
            }

            if (statusInfo.Status == "Completed")
            {
                var exportedBlobPath = statusInfo.BlobUri;
                Log.Information(String.Format("Export Complete - Database exported to: {0}", exportedBlobPath));
                exportComplete = true;
            }

            if (exportComplete)
            {
                return Task.FromResult(Complete());
            }

            var parameters = new Dictionary<string, string>();
            parameters["RequestGUID"] = RequestGUID;
            parameters["TargetDatabaseConnection"] = TargetDatabaseConnection.ConnectionString;
            return Task.FromResult(Suspend(TimeSpan.FromSeconds(60), parameters));
        }

        //private Task ExportAsync(string serviceUri, BlobStorageAccessKeyCredentials blobStorageCredentials, ConnectionInfo connectionInfo)
        //{
        //    try
        //    {
        //        var exportInput = new ExportInput()
        //        {
        //            BlobCredentials = blobStorageCredentials,
        //            ConnectionInfo = connectionInfo,
        //        };

        //        HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(String.Format("{0}{1}", serviceUri, ExportMethod));
        //        httpWebRequest.Method = WebRequestMethods.Http.Post;
        //        httpWebRequest.ContentType = @"application/xml";

        //        Log.Information(String.Format("\n httpWebRequest.RequestUri : {0}", httpWebRequest.RequestUri));

        //        Log.Information(String.Format("\n blobUri : {0}", blobStorageCredentials.Uri));
        //        Log.Information(String.Format("\n blobStorageAccessKey : {0}", blobStorageCredentials.StorageAccessKey));

        //        Log.Information(String.Format("\n connectionInfo.DatabaseName: {0}", connectionInfo.DatabaseName));
        //        Log.Information(String.Format("\n connectionInfo.Password: {0}", connectionInfo.Password));
        //        Log.Information(String.Format("\n connectionInfo.ServerName: {0}", connectionInfo.ServerName));
        //        Log.Information(String.Format("\n connectionInfo.UserName: {0}", connectionInfo.UserName));

        //        using (var stream = httpWebRequest.GetRequestStream())
        //        {
        //            var dcs = new DataContractSerializer(exportInput.GetType());
        //            dcs.WriteObject(stream, exportInput);
        //        }

        //        HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
        //        if (httpResponse.StatusCode != HttpStatusCode.OK)
        //        {
        //            Log.Information(String.Format("httpResponse Status Code : ", httpResponse.StatusCode));
        //        }
        //        else
        //        {
        //            Guid guid = Guid.Empty;
        //            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
        //            {
        //                var result = streamReader.ReadToEnd();
        //                guid = JsonFormat.Deserialize<System.Guid>(result);
        //                streamReader.Close();
        //            }

        //            Log.Information(String.Format("Started Import. GUID is {0}", guid));
        //        }
        //    }
        //    catch (WebException ex)
        //    {
        //        Log.Error("WebException Caught");
        //        Log.Error(ex.ToString());
        //    }

        //    return Task.FromResult(0);
        //}

        public static string GetDatabaseServerName(SqlConnectionStringBuilder connectionStringBuilder)
        {
            var dataSource = connectionStringBuilder.DataSource;
            if (dataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
                dataSource = dataSource.Substring(4);
            var indexOfFirstPeriod = dataSource.IndexOf(".", StringComparison.Ordinal);

            if (indexOfFirstPeriod > -1)
                return dataSource.Substring(0, indexOfFirstPeriod);

            return dataSource;
        }
    }

    public class SyncDatacenterEventSource : EventSource
    {
        public static readonly SyncDatacenterEventSource Log = new SyncDatacenterEventSource();

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Message = "Preparing to export source database {0} on server {1} from primary datacenter {2}")]
        public void PreparingToExport(string datacenter, string server, string database) { WriteEvent(1, database, server, datacenter); }

        [Event(
            eventId: 2,
            Level = EventLevel.Warning,
            Message = "Source database {0} not found!")]
        public void SourceDatabaseNotFound(string source) { WriteEvent(2, source); }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Message = "{0}")]
        public void Information(string message) { WriteEvent(3, message); }

        [Event(
            eventId: 4,
            Level = EventLevel.Error,
            Message = "{0}")]
        public void Error(string message) { WriteEvent(4, message); }

        //public void LogMsg(string format, params object[] args) { LogMsg(String.Format(format, args)); }

        //private SyncDatacenterEventSource() { }
    }
}

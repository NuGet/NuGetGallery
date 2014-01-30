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
        private static readonly string NorthCentralUSUri = @"https://ch1prod-dacsvc.azure.com/DACWebService.svc/";
        //private static readonly string EastUSUri = @"https://bl2prod-dacsvc.azure.com/DACWebService.svc/";

        private static readonly string ExportMethod = "Export";

        public string DestinationStorageAccountName { get; set; }
        
        public string DestinationStorageAccountKey { get; set; }

        public SyncDatacenterJob(ConfigurationHub configHub) : base(configHub) { }

        protected internal override async Task<JobContinuation> Execute()
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

            var blobStorageCredentials = new BlobStorageAccessKeyCredentials();
            blobStorageCredentials.Uri = String.Format(@"https://{0}.blob.core.windows.net/bacpac-files/{1}.bacpac", DestinationStorageAccountName, DateTime.UtcNow.ToString("yyyy-mm-dd"));
            blobStorageCredentials.StorageAccessKey = DestinationStorageAccountKey;

            var connectionInfo = new ConnectionInfo();
            connectionInfo.DatabaseName = cstr.InitialCatalog;
            connectionInfo.Password = cstr.Password;
            connectionInfo.ServerName = cstr.DataSource;
            connectionInfo.UserName = cstr.UserID;

            await ExportAsync(NorthCentralUSUri, blobStorageCredentials, connectionInfo);

            var parameters = new Dictionary<string, string>();
            return Suspend(TimeSpan.FromSeconds(3), parameters);
        }

        private Task ExportAsync(string serviceUri, BlobStorageAccessKeyCredentials blobStorageCredentials, ConnectionInfo connectionInfo)
        {
            try
            {
                var exportInput = new ExportInput()
                {
                    BlobCredentials = blobStorageCredentials,
                    ConnectionInfo = connectionInfo,
                };

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(String.Format("{0}{1}", serviceUri, ExportMethod));
                httpWebRequest.Method = WebRequestMethods.Http.Post;
                httpWebRequest.ContentType = @"application/xml";

                Log.LogMsg(String.Format("\n httpWebRequest.RequestUri : {0}", httpWebRequest.RequestUri));

                Log.LogMsg(String.Format("\n blobUri : {0}", blobStorageCredentials.Uri));
                Log.LogMsg(String.Format("\n blobStorageAccessKey : {0}", blobStorageCredentials.StorageAccessKey));

                Log.LogMsg(String.Format("\n connectionInfo.DatabaseName: {0}", connectionInfo.DatabaseName));
                Log.LogMsg(String.Format("\n connectionInfo.Password: {0}", connectionInfo.Password));
                Log.LogMsg(String.Format("\n connectionInfo.ServerName: {0}", connectionInfo.ServerName));
                Log.LogMsg(String.Format("\n connectionInfo.UserName: {0}", connectionInfo.UserName));

                using (var stream = httpWebRequest.GetRequestStream())
                {
                    var dcs = new DataContractSerializer(exportInput.GetType());
                    dcs.WriteObject(stream, exportInput);
                }

                HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                if (httpResponse.StatusCode != HttpStatusCode.OK)
                {
                    Log.LogMsg(String.Format("httpResponse Status Code : ", httpResponse.StatusCode));
                }
                else
                {
                    Guid guid = Guid.Empty;
                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        var result = streamReader.ReadToEnd();
                        guid = JsonFormat.Deserialize<System.Guid>(result);
                        streamReader.Close();
                    }

                    Log.LogMsg(String.Format("Started Import. GUID is {0}", guid));
                }
            }
            catch (WebException ex)
            {
                Log.LogMsg("WebException Caught");
                Log.LogMsg(ex.ToString());
            }

            return Task.FromResult(0);
        }

        private void GetStatus(string guid)
        {

        }

        protected internal override Task<JobContinuation> Resume()
        {
            return base.Resume();
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
        public void LogMsg(string message) { WriteEvent(3, message); }

        private SyncDatacenterEventSource() { }
    }
}

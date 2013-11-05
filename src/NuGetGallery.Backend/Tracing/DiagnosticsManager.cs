using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Backend.Tracing
{
    public class DiagnosticsManager
    {
        private bool _initialized = false;
        private CloudTable _resultTable;

        public string StorageConnectionString { get; private set; }
        public string LogDirectory { get; private set; }

        public DiagnosticsManager(string logDirectory, string storageConnectionString)
        {
            LogDirectory = logDirectory;
            StorageConnectionString = storageConnectionString;
        }

        public void Initialize()
        {
            // Initialization should not kill the service, but should be able to report a failure
            try
            {
                InitializeCore();
                _initialized = true;
            }
            catch (Exception ex)
            {
                WorkerEventSource.Log.StartupError(ex.ToString(), ex.StackTrace);
                _initialized = false;
            }

            if (_initialized)
            {
                WorkerEventSource.Log.DiagnosticsInitialized();
            }
        }

        public void RegisterJob(Job job)
        {
            try
            {
                // Set up log table
                var logListener = WindowsAzureTableLog.CreateListener(
                    RoleEnvironment.CurrentRoleInstance.Id,
                    connectionString: StorageConnectionString,
                    tableAddress: "NuGetWorkerJob" + job.Name);
                logListener.EnableEvents(job.BaseLog, EventLevel.Informational);
            }
            catch (Exception ex)
            {
                WorkerEventSource.Log.StartupError(ex.ToString(), ex.StackTrace);
            }
        }

        public async Task ReportJobResponse(JobResponse response)
        {
            try
            {
                await _resultTable.CreateIfNotExistsAsync();

                await _resultTable.ExecuteAsync(
                    TableOperation.InsertOrReplace(
                        new JobResposeTableEntity(RoleEnvironment.CurrentRoleInstance.Id, response)));
            }
            catch (Exception ex)
            {
                WorkerEventSource.Log.ReportingFailure(response.Invocation.Id, ex);
            }
        }

        private void InitializeCore()
        {
            ConfigureBaseTracing();

            var tables = CloudStorageAccount.Parse(StorageConnectionString).CreateCloudTableClient();
            _resultTable = tables.GetTableReference("NuGetJobStatus");
        }

        private void ConfigureBaseTracing()
        {
            // Set up flat file
            var masterFileLog = RollingFlatFileLog.CreateListener(
                Path.Combine(LogDirectory, "master.log"),
                rollSizeKB: 1024,
                timestampPattern: "yyyy-MM-ddTHH-mm-ss",
                rollFileExistsBehavior: RollFileExistsBehavior.Increment,
                rollInterval: RollInterval.Hour,
                formatter: new JsonEventTextFormatter(),
                isAsync: true);
            AttachCoreLoggers(masterFileLog);

            // Set up a table
            var masterTableLog = WindowsAzureTableLog.CreateListener(
                RoleEnvironment.CurrentRoleInstance.Id,
                StorageConnectionString,
                tableAddress: "NuGetWorkerMaster");
            AttachCoreLoggers(masterTableLog);
        }

        private void AttachCoreLoggers(EventListener listener)
        {
            listener.EnableEvents(
                WorkerEventSource.Log,
                EventLevel.LogAlways);
            listener.EnableEvents(
                DispatcherEventSource.Log,
                EventLevel.LogAlways);
        }
    }
}

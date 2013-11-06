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
        private CloudTableClient _tables;

        public string StorageConnectionString { get; private set; }
        public string LogDirectory { get; private set; }

        public DiagnosticsManager(string logDirectory, string storageConnectionString)
        {
            LogDirectory = logDirectory;
            StorageConnectionString = storageConnectionString;

            _tables = CloudStorageAccount.Parse(StorageConnectionString).CreateCloudTableClient();
        }

        public void Initialize()
        {
            // Initialization should not kill the service, but should be able to report a failure
            WorkerEventSource.Log.DiagnosticsInitializing();

            try
            {
                InitializeCore();
                _initialized = true;
            }
            catch (Exception ex)
            {
                WorkerEventSource.Log.DiagnosticsInitializationError(ex);
                _initialized = false;
            }

            if (_initialized)
            {
                WorkerEventSource.Log.DiagnosticsInitialized();
            }
        }

        public void RegisterJob(Job job)
        {
            if (_initialized)
            {
                WorkerEventSource.Log.DiagnosticsRegisterJob(job.Name);
                try
                {
                    // Set up log table
                    var tableName = "NuGetWorkerJob" + job.Name;
                    var logListener = WindowsAzureTableLog.CreateListener(
                        RoleEnvironment.CurrentRoleInstance.Id,
                        connectionString: StorageConnectionString,
                        tableAddress: tableName);
                    logListener.EnableEvents(job.BaseLog, EventLevel.Informational);
                    WorkerEventSource.Log.DiagnosticsJobRegistered(job.Name, tableName);
                }
                catch (Exception ex)
                {
                    WorkerEventSource.Log.DiagnosticsJobRegisterError(job.Name, ex);
                }
            }
        }

        public async Task ReportJobResponse(JobResponse response)
        {
            if (_initialized)
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
                    WorkerEventSource.Log.ReportingFailure(response, ex);
                }
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
                // All but Verbose
                EventLevel.Informational);
            listener.EnableEvents(
                SemanticLoggingEventSource.Log,
                EventLevel.LogAlways);
        }
    }
}

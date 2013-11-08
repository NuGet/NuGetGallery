using System;
using System.Reactive.Linq;
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
using NuGetGallery.Jobs;
using NuGetGallery.Monitoring.Tables;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Backend.Monitoring
{
    public class BackendMonitoringHub : MonitoringHub
    {
        private const string BackendMonitoringContainerName = "backend-monitoring";
        private const string BackendServiceName = "Backend";

        private Dictionary<Job, ObservableEventListener> _eventStreams = new Dictionary<Job, ObservableEventListener>();
        private MonitoringTable<BackendInstanceStatusEntry> _instanceStatusTable;
        private MonitoringTable<BackendJobInvocationEntry> _invocationsTable;

        public string LogsDirectory { get; private set; }
        public string TempDirectory { get; private set; }
        public string InstanceName { get; private set; }

        public BackendMonitoringHub(
            string storageConnectionString, 
            string logsDirectory, 
            string tempDirectory,
            string instanceName)
            : base(storageConnectionString)
        {
            LogsDirectory = logsDirectory;
            TempDirectory = tempDirectory;
            InstanceName = instanceName;

            _instanceStatusTable = Table<BackendInstanceStatusEntry>();
            _invocationsTable = Table<BackendJobInvocationEntry>();
        }

        /// <summary>
        /// Registers a job with the monitoring hub
        /// </summary>
        /// <param name="job">The job to register</param>
        public virtual void RegisterJob(Job job)
        {
            // Set up an event listener for the job
            var eventStream = new ObservableEventListener();
            eventStream.EnableEvents(job.GetEventSource(), EventLevel.LogAlways);
            _eventStreams[job] = eventStream;

            // Set up the table listener for this job
            var tableName = GetTableFullName("Job" + job.Name);
            eventStream.LogToWindowsAzureTable(
                InstanceName,
                StorageConnectionString,
                tableAddress: tableName);
        }

        public override async Task Start()
        {
            // Log Status
            await _instanceStatusTable.Upsert(new BackendInstanceStatusEntry(BackendServiceName, InstanceName)
            {
                Status = BackendInstanceStatus.Started,
                LastInvocation = Guid.Empty,
                LastJob = String.Empty,
                LastUpdatedAt = DateTimeOffset.UtcNow
            });
        }

        /// <summary>
        /// Handles monitoring tasks performed when a job request is dispatched. Call Complete(JobResult)
        /// on the IAsyncDeferred returned by this method when the job finishes execution.
        /// </summary>
        public async Task<IAsyncDeferred<JobResult>> InvokingJob(JobInvocation invocation, Job job)
        {
            // Log Invocation Status
            await _invocationsTable.Upsert(new BackendJobInvocationEntry(job.Name, invocation.Id)
            {
                Status = JobStatus.Executing,
                RecievedAt = invocation.RecievedAt,
                Source = invocation.Request.Source,
                Payload = invocation.Request.Message != null ? invocation.Request.Message.AsString : String.Empty,
                LogUrl = String.Empty
            });

            // Log Instance Status
            await _instanceStatusTable.Upsert(new BackendInstanceStatusEntry(BackendServiceName, InstanceName)
            {
                Status = BackendInstanceStatus.Executing,
                LastInvocation = invocation.Id,
                LastJob = job.Name,
                LastUpdatedAt = DateTimeOffset.UtcNow
            });

            // Get the event stream for this job
            ObservableEventListener eventStream;
            if (!_eventStreams.TryGetValue(job, out eventStream))
            {
                return;
            }

            // Capture the events into a flat file
            var fileName = invocation.Id.ToString("N") + ".json";
            var path = Path.Combine(TempDirectory, "Invocations", fileName);
            var token = eventStream.LogToFlatFile(
                path,
                new JsonEventTextFormatter(EventTextFormatting), 
                isAsync: true);
            return new AsyncDeferred<JobResult>(async result =>
            {
                // Disconnect the listener
                token.Dispose();

                // Upload the file to blob storage
                var blob = await UploadBlob(path, BackendMonitoringContainerName, "invocations/" + fileName);

                // Delete the temp file
                File.Delete(path);

                // Log Invocation status
                await _invocationsTable.Upsert(new BackendJobInvocationEntry(job.Name, invocation.Id)
                {
                    Status = result.Status,
                    RecievedAt = invocation.RecievedAt,
                    Source = invocation.Request.Source,
                    Payload = invocation.Request.Message != null ? invocation.Request.Message.AsString : String.Empty,
                    LogUrl = blob.Uri.AbsoluteUri
                });

                // Log Instance Status
                await _instanceStatusTable.Upsert(new BackendInstanceStatusEntry(BackendServiceName, InstanceName)
                {
                    PartitionKey = BackendServiceName,
                    RowKey = InstanceName,
                    Status = BackendInstanceStatus.Idle,
                    LastInvocation = invocation.Id,
                    LastJob = job.Name,
                    LastUpdatedAt = DateTimeOffset.UtcNow
                });
            });
        }
    }
}

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
        private const string BackendTraceTableName = "BackendTrace";

        private Dictionary<Job, ObservableEventListener> _eventStreams = new Dictionary<Job, ObservableEventListener>();
        
        private MonitoringTable<WorkerInstanceStatusEntry> _instanceStatusTable;
        private MonitoringTable<WorkerInstanceHistoryEntry> _instanceHistoryTable;
        private MonitoringTable<JobStatusEntry> _jobStatusTable;
        private MonitoringTable<JobHistoryEntry> _jobHistoryTable;
        private MonitoringTable<InvocationsEntry> _invocationsTable;

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

            _instanceStatusTable = Table<WorkerInstanceStatusEntry>();
            _instanceHistoryTable = Table<WorkerInstanceHistoryEntry>();
            _jobStatusTable = Table<JobStatusEntry>();
            _jobHistoryTable = Table<JobHistoryEntry>();
            _invocationsTable = Table<InvocationsEntry>();
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

            // Log an entry for the job in the status table
            _jobStatusTable.InsertOrIgnoreDuplicate(new JobStatusEntry(job.Name, DateTimeOffset.UtcNow));
        }

        public override async Task Start()
        {
            // Set up worker logging
            var listener = WindowsAzureTableLog.CreateListener(
                InstanceName,
                StorageConnectionString,
                tableAddress: GetTableFullName(BackendTraceTableName));
            listener.EnableEvents(WorkerEventSource.Log, EventLevel.Informational);
            listener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Informational);

            // Log Instance Status
            await _instanceStatusTable.Upsert(new WorkerInstanceStatusEntry(InstanceName, DateTimeOffset.UtcNow, BackendInstanceStatus.Started));
        }

        /// <summary>
        /// Handles monitoring tasks performed when a job request is dispatched. Call Complete(JobResult)
        /// on the IAsyncDeferred returned by this method when the job finishes execution.
        /// </summary>
        public async Task<IAsyncDeferred<JobResult>> InvokingJob(JobInvocation invocation, Job job)
        {
            // Record start of job
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            await ReportStartJob(invocation, job, startTime);

            // Get the event stream for this job
            ObservableEventListener eventStream;
            if (!_eventStreams.TryGetValue(job, out eventStream))
            {
                return null;
            }

            // Capture the events into a flat file
            var fileName = invocation.Id.ToString("N") + ".json";
            var path = Path.Combine(TempDirectory, "Invocations", fileName);
            var token = eventStream.LogToFlatFile(
                path,
                new JsonEventTextFormatter(EventTextFormatting.None));
            return new AsyncDeferred<JobResult>(async result =>
            {
                // Disconnect the listener
                token.Dispose();

                // Upload the file to blob storage
                var blob = await UploadBlob(path, BackendMonitoringContainerName, "invocations/" + fileName);

                // Delete the temp file
                File.Delete(path);

                // Record end of job
                await ReportEndJob(invocation, result, job, blob.Uri.AbsoluteUri, startTime, DateTimeOffset.UtcNow);
            });
        }

        private Task ReportStartJob(JobInvocation invocation, Job job, DateTimeOffset startTime)
        {
            return Task.WhenAll(
                // Add History Rows
                _jobHistoryTable.Upsert(new JobHistoryEntry(job.Name, startTime, invocation.Id, InstanceName)),
                _instanceHistoryTable.Upsert(new WorkerInstanceHistoryEntry(InstanceName, startTime, invocation.Id, job.Name)),

                // Upsert Status Rows
                _jobStatusTable.Upsert(new JobStatusEntry(job.Name, startTime, invocation.Id, InstanceName)),
                _instanceStatusTable.Upsert(new WorkerInstanceStatusEntry(InstanceName, startTime, BackendInstanceStatus.Executing, invocation.Id, job.Name)),
                
                // Add invocation row
                _invocationsTable.Upsert(new InvocationsEntry(invocation)));
        }

        private Task ReportEndJob(JobInvocation invocation, JobResult result, Job job, string logUrl, DateTimeOffset startTime, DateTimeOffset completionTime)
        {
            return Task.WhenAll(
                // Add History Rows
                _jobHistoryTable.Upsert(new JobHistoryEntry(job.Name, completionTime, invocation.Id, InstanceName, result, completionTime)),
                _instanceHistoryTable.Upsert(new WorkerInstanceHistoryEntry(InstanceName, completionTime, invocation.Id, job.Name, result, completionTime)),

                // Add Status rows
                _jobStatusTable.Upsert(new JobStatusEntry(job.Name, startTime, invocation.Id, result, InstanceName, completionTime)),
                _instanceStatusTable.Upsert(new WorkerInstanceStatusEntry(InstanceName, startTime, BackendInstanceStatus.Idle, invocation.Id, job.Name, result, completionTime)),

                // Update invocation row
                _invocationsTable.Upsert(new InvocationsEntry(invocation, result, logUrl)));
        }
    }
}

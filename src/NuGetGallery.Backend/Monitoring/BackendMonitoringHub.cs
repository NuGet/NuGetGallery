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
        internal const string BackendMonitoringContainerName = "backend-monitoring";
        internal const string BackendTraceTableName = "BackendTrace";

        private Dictionary<JobBase, ObservableEventListener> _eventStreams = new Dictionary<JobBase, ObservableEventListener>();
        
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
        public virtual void RegisterJob(JobBase job)
        {
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
            listener.EnableEvents(InvocationEventSource.Log, EventLevel.Informational);
            listener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Informational);

            // Log Instance Status
            await _instanceStatusTable.Upsert(new WorkerInstanceStatusEntry(InstanceName, DateTimeOffset.UtcNow, BackendInstanceStatus.Started));
        }

        /// <summary>
        /// Handles monitoring tasks performed when a job request is dispatched. Call Complete(JobResult)
        /// on the IComplete returned by this method when the job finishes execution.
        /// </summary>
        public async Task<InvocationMonitoringContext> BeginInvocation(JobInvocation invocation)
        {
            // Create a monitoring context
            var context = new InvocationMonitoringContext(invocation, this);
            await context.Begin();
            return context;
        }

        internal Task ReportStartJob(JobInvocation invocation, JobBase job, DateTimeOffset startTime)
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

        internal Task ReportEndJob(JobInvocation invocation, JobResult result, JobBase job, string logUrl, DateTimeOffset startTime, DateTimeOffset completionTime)
        {
            return Task.WhenAll(
                // Add History Rows
                _jobHistoryTable.Upsert(new JobHistoryEntry(job.Name, completionTime, invocation.Id, InstanceName, result, completionTime)),
                _instanceHistoryTable.Upsert(new WorkerInstanceHistoryEntry(InstanceName, completionTime, invocation.Id, job.Name, result, completionTime)),

                // Add Status rows
                _jobStatusTable.Upsert(new JobStatusEntry(job.Name, startTime, invocation.Id, result, InstanceName, completionTime)),
                _instanceStatusTable.Upsert(new WorkerInstanceStatusEntry(InstanceName, startTime, BackendInstanceStatus.Idle, invocation.Id, job.Name, result, completionTime)),

                // Update invocation row
                _invocationsTable.Upsert(new InvocationsEntry(invocation, result, logUrl, completionTime)));
        }
    }
}

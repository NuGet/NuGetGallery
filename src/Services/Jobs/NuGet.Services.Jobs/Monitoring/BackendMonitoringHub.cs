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
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Monitoring;
using NuGetGallery.Storage;

namespace NuGet.Services.Jobs.Monitoring
{
    public class BackendMonitoringHub : MonitoringHub, IDisposable
    {
        internal const string BackendMonitoringContainerName = "ng-jobs-invocationlogs";
        internal const string BackendTraceTableName = "JobsServiceTrace";

        private Dictionary<JobBase, ObservableEventListener> _eventStreams = new Dictionary<JobBase, ObservableEventListener>();
        
        private PivotedTable<InvocationsEntry> _invocationsTable;
        private AzureTable<JobsEntry> _jobsTable;
        private AzureTable<InstancesEntry> _instancesTable;
        
        private SinkSubscription<WindowsAzureTableSink> _globalSinkSubscription;
        
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

            _invocationsTable = PivotedTable<InvocationsEntry>();
            _jobsTable = Table<JobsEntry>();
            _instancesTable = Table<InstancesEntry>();
        }

        /// <summary>
        /// Registers a job with the monitoring hub
        /// </summary>
        /// <param name="job">The job to register</param>
        public virtual void RegisterJob(JobDescription job)
        {
            // Log an entry for the job in the status table
            _jobsTable.Merge(JobsEntry.ForJob(job));
        }

        public override async Task Start()
        {
            // Set up worker logging
            var listener = new ObservableEventListener();
            var capturedId = RunnerId.Get();
            var stream = listener.Where(_ => RunnerId.Get() == capturedId);
            listener.EnableEvents(JobsServiceEventSource.Log, EventLevel.Informational);
            listener.EnableEvents(InvocationEventSource.Log, EventLevel.Informational);
            listener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Informational);
            _globalSinkSubscription = stream.LogToWindowsAzureTable(
                InstanceName,
                StorageConnectionString,
                tableAddress: GetTableFullName(BackendTraceTableName));

            // Log Instance Status
            await _instancesTable.InsertOrReplace(InstancesEntry.ForCurrentMachine(InstanceName));
        }

        /// <summary>
        /// Handles monitoring tasks performed when a job request is dispatched. Call Complete(JobResult)
        /// on the IComplete returned by this method when the job finishes execution.
        /// </summary>
        public async Task<InvocationLogCapture> BeginInvocation(Invocation invocation)
        {
            // Create a monitoring context
            var context = new InvocationLogCapture(invocation, this);
            await context.Start();
            return context;
        }

        public void Dispose()
        {
            if (_globalSinkSubscription != null)
            {
                _globalSinkSubscription.Dispose();
            }
        }
    }
}

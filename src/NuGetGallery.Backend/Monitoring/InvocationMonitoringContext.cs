using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using NuGetGallery.Jobs;

namespace NuGetGallery.Backend.Monitoring
{
    public class InvocationMonitoringContext
    {
        private InvocationEventSource _workerLog;
        private ObservableEventListener _eventStream;

        private string _jsonLog;
        private string _textLog;
        private IDisposable _jsonSubscription;
        private IDisposable _textSubscription;
        private DateTimeOffset _startTime;
        
        public JobInvocation Invocation { get; private set; }
        public BackendMonitoringHub Monitoring { get; private set; }
        public Job Job { get; private set; }

        public InvocationMonitoringContext(JobInvocation invocation, InvocationEventSource log, BackendMonitoringHub monitoring)
        {
            Invocation = invocation;
            Monitoring = monitoring;
            _workerLog = log;
        }

        public void Begin()
        {
            // Mark start time
            _startTime = DateTimeOffset.UtcNow;
            
            // Set up an event stream
            _eventStream = new ObservableEventListener();
            _eventStream.EnableEvents(_workerLog, EventLevel.Informational);

            // Capture the events into a flat file
            var root = Path.Combine(Monitoring.TempDirectory, "Invocations");
            _jsonLog = Path.Combine(root, Invocation.Id.ToString("N") + ".json");
            _textLog = Path.Combine(root, Invocation.Id.ToString("N") + ".txt");

            // Json Log
            _jsonSubscription = _eventStream.LogToFlatFile(_jsonLog, new JsonEventTextFormatter(EventTextFormatting.Indented, dateTimeFormat: "O"));

            // Plain text log
            _textSubscription = _eventStream.LogToFlatFile(_textLog, new EventTextFormatter(dateTimeFormat: "O"));
        }

        public async Task End(JobResult result)
        {
            // Disconnect the listener
            _jsonSubscription.Dispose();
            _textSubscription.Dispose();

            // Upload the file to blob storage
            var jsonBlob = await Monitoring.UploadBlob(_jsonLog, BackendMonitoringHub.BackendMonitoringContainerName, "invocations/" + Path.GetFileName(_jsonLog));
            await Monitoring.UploadBlob(_textLog, BackendMonitoringHub.BackendMonitoringContainerName, "invocations/" + Path.GetFileName(_textLog));

            // Delete the temp files
            File.Delete(_jsonLog);
            File.Delete(_textLog);

            // Record end of job
            await Monitoring.ReportEndJob(Invocation, result, Job, jsonBlob.Uri.AbsoluteUri, _startTime, DateTimeOffset.UtcNow);
        }

        public async Task SetJob(Job job)
        {
            Job = job;

            // Record start of job
            await Monitoring.ReportStartJob(Invocation, Job, _startTime);

            _eventStream.EnableEvents(Job.GetEventSource(), EventLevel.Informational);
        }
    }
}

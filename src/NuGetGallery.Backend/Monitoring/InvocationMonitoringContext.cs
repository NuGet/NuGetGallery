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
using NuGetGallery.Jobs;

namespace NuGetGallery.Backend.Monitoring
{
    public class InvocationMonitoringContext
    {
        private ObservableEventListener _listener;
        private IObservable<EventEntry> _eventStream;

        private string _jsonLog;
        private string _textLog;
        private IDisposable _jsonSubscription;
        private IDisposable _textSubscription;
        private DateTimeOffset _startTime;
        
        public JobInvocation Invocation { get; private set; }
        public BackendMonitoringHub Monitoring { get; private set; }
        public JobBase Job { get; private set; }
        public JobDescription JobDescription { get; private set; }

        public InvocationMonitoringContext(JobInvocation invocation, BackendMonitoringHub monitoring)
        {
            Invocation = invocation;
            Monitoring = monitoring;
        }

        public async Task Begin()
        {
            // Mark start time
            _startTime = DateTimeOffset.UtcNow;
            
            // Set up an event stream
            _listener = new ObservableEventListener();
            _eventStream = from events in _listener
                           where JobInvocationContext.GetCurrentInvocationId() == Invocation.Id
                           select events;
            _listener.EnableEvents(InvocationEventSource.Log, EventLevel.Informational);

            // Calculate paths
            var root = Path.Combine(Monitoring.TempDirectory, "Invocations");
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
            }

            _jsonLog = Path.Combine(root, Invocation.Id.ToString("N") + ".json");
            _textLog = Path.Combine(root, Invocation.Id.ToString("N") + ".txt");
            
            // Fetch the current logs if this is a continuation, we'll append to them during the invocation
            if (Invocation.IsContinuation)
            {
                await Task.WhenAll(
                    Monitoring.DownloadBlob(BackendMonitoringHub.BackendMonitoringContainerName, "invocations/" + Path.GetFileName(_jsonLog), _jsonLog),
                    Monitoring.DownloadBlob(BackendMonitoringHub.BackendMonitoringContainerName, "invocations/" + Path.GetFileName(_textLog), _textLog));
            }

            // Capture the events into a JSON file and a plain text file
            _jsonSubscription = _eventStream.LogToFlatFile(_jsonLog, new JsonEventTextFormatter(EventTextFormatting.Indented, dateTimeFormat: "O"));
            _textSubscription = _eventStream.LogToFlatFile(_textLog, new EventTextFormatter(dateTimeFormat: "O"));
        }

        public async Task End(JobResult result)
        {
            // Disconnect the listener
            _jsonSubscription.Dispose();
            _textSubscription.Dispose();

            // Upload the file to blob storage
            var jsonBlob = (await Task.WhenAll(
                Monitoring.UploadBlob(_jsonLog, BackendMonitoringHub.BackendMonitoringContainerName, "invocations/" + Path.GetFileName(_jsonLog)),
                Monitoring.UploadBlob(_textLog, BackendMonitoringHub.BackendMonitoringContainerName, "invocations/" + Path.GetFileName(_textLog))))
                .First();

            // Delete the temp files
            File.Delete(_jsonLog);
            File.Delete(_textLog);

            // Record end of job
            await Monitoring.ReportEndJob(Invocation, result, JobDescription, jsonBlob.Uri.AbsoluteUri, _startTime, DateTimeOffset.UtcNow);
        }

        public async Task SetJob(JobDescription jobDesc, JobBase job)
        {
            Job = job;
            JobDescription = jobDesc;

            // Record start of job
            await Monitoring.ReportStartJob(Invocation, JobDescription, _startTime);

            var eventSource = Job.GetEventSource();
            if (eventSource == null)
            {
                InvocationEventSource.Log.NoEventSource(jobDesc.Name);
            }
            else
            {
                _listener.EnableEvents(eventSource, EventLevel.Informational);
            }
        }
    }
}

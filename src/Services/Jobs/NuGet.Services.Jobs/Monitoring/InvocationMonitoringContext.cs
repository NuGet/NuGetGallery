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

namespace NuGet.Services.Jobs.Monitoring
{
    public class InvocationMonitoringContext
    {
        private ObservableEventListener _listener;
        private IObservable<EventEntry> _eventStream;

        private string _jsonLog;
        private IDisposable _jsonSubscription;
        
        public JobInvocation Invocation { get; private set; }
        public BackendMonitoringHub Hub { get; private set; }
        public JobBase Job { get; private set; }
        public JobDescription JobDescription { get; private set; }

        public InvocationMonitoringContext(JobInvocation invocation, BackendMonitoringHub hub)
        {
            Invocation = invocation;
            Hub = hub;
        }

        public async Task Begin()
        {
            // Set up an event stream
            _listener = new ObservableEventListener();
            _eventStream = from events in _listener
                           where JobInvocationContext.GetCurrentInvocationId() == Invocation.Id
                           select events;
            _listener.EnableEvents(InvocationEventSource.Log, EventLevel.Informational);

            // Calculate paths
            var root = Path.Combine(Hub.TempDirectory, "Invocations");
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
            }

            _jsonLog = Path.Combine(root, Invocation.Id.ToString("N") + ".json");
            if (File.Exists(_jsonLog))
            {
                File.Delete(_jsonLog);
            }
            
            // Fetch the current logs if this is a continuation, we'll append to them during the invocation
            if (Invocation.IsContinuation)
            {
                await Hub.DownloadBlob(BackendMonitoringHub.BackendMonitoringContainerName, "invocations/" + Path.GetFileName(_jsonLog), _jsonLog);
            }

            // Capture the events into a JSON file and a plain text file
            _jsonSubscription = _eventStream.LogToFlatFile(_jsonLog, new JsonEventTextFormatter(EventTextFormatting.Indented, dateTimeFormat: "O"));
        }

        public async Task End(JobInvocation invocation, JobResponse response)
        {
            // Disconnect the listener
            _jsonSubscription.Dispose();

            // Upload the file to blob storage
            var jsonBlob = await Hub.UploadBlob(_jsonLog, BackendMonitoringHub.BackendMonitoringContainerName, "invocations/" + Path.GetFileName(_jsonLog));

            // Delete the temp files
            File.Delete(_jsonLog);

            // Record end of job
            await Hub.ReportEndJob(invocation, response, jsonBlob.Uri.AbsoluteUri);
        }

        public async Task SetJob(JobDescription jobDesc, JobBase job)
        {
            Job = job;
            JobDescription = jobDesc;

            // Record start of job
            await Hub.ReportStartJob(Invocation, JobDescription);

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

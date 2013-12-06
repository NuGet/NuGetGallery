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
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.Jobs.Monitoring
{
    public class InvocationLogCapture
    {
        private ObservableEventListener _listener;
        private IObservable<EventEntry> _eventStream;

        private string _tempFile;
        private IDisposable _eventSubscription;

        public Invocation Invocation { get; private set; }
        public JobsService Service { get; private set; }

        public InvocationLogCapture(Invocation invocation, JobsService service)
        {
            Invocation = invocation;
            Service = service;
        }

        public async Task Start()
        {
            // Set up an event stream
            _listener = new ObservableEventListener();
            _eventStream = from events in _listener
                           where InvocationContext.GetCurrentInvocationId() == Invocation.Id
                           select events;
            _listener.EnableEvents(InvocationEventSource.Log, EventLevel.Informational);

            // Calculate paths
            var root = Path.Combine(Service.TempDirectory, "Invocations");
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
            }

            _tempFile = Path.Combine(root, Invocation.Id.ToString("N") + ".json");
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
            
            // Fetch the current logs if this is a continuation, we'll append to them during the invocation
            if (Invocation.Continuation)
            {
                await Service.Storage.Primary.Blobs.DownloadBlob(JobsService.InvocationLogsContainerBaseName, "invocations/" + Path.GetFileName(_tempFile), _tempFile);
            }

            // Capture the events into a JSON file and a plain text file
            _eventSubscription = _eventStream.LogToFlatFile(_tempFile, new JsonEventTextFormatter(EventTextFormatting.Indented, dateTimeFormat: "O"));
        }

        public async Task<CloudBlockBlob> End()
        {
            // Disconnect the listener
            _eventSubscription.Dispose();

            // Upload the file to blob storage
            var logBlob = await Service.Storage.Primary.Blobs.UploadBlob(_tempFile, JobsService.InvocationLogsContainerBaseName, "invocations/" + Path.GetFileName(_tempFile));

            // Delete the temp files
            File.Delete(_tempFile);

            return logBlob;
        }

        public void SetJob(JobDescription jobDesc, JobBase job)
        {
            var eventSource = job.GetEventSource();
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

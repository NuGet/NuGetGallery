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
using NuGet.Services.Storage;
using System.Reactive.Subjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;

namespace NuGet.Services.Work.Monitoring
{
    public class InvocationLogCapture : IObservable<EventEntry>
    {
        private ObservableEventListener _listener;
        private IObservable<EventEntry> _eventStream;

        public InvocationState Invocation { get; private set; }
        
        public InvocationLogCapture(InvocationState invocation)
        {
            Invocation = invocation;

            // Set up an event stream
            _listener = new ObservableEventListener();
            _eventStream = from events in _listener
                           where InvocationContext.GetCurrentInvocationId() == Invocation.Id
                           select events;
        }

        public virtual Task Start() {
            _listener.EnableEvents(InvocationEventSource.Log, EventLevel.Informational);
            return Task.FromResult<object>(null);
        }

        public virtual Task<Uri> End() 
        {
            return Task.FromResult<Uri>(null);
        }

        public virtual void SetJob(JobDescription jobdef, JobHandlerBase job)
        {
            var eventSource = job.GetEventSource();
            if (eventSource == null)
            {
                InvocationEventSource.Log.NoEventSource(jobdef.Name);
            }
            else
            {
                _listener.EnableEvents(eventSource, EventLevel.Informational);
            }
        }
    
        public IDisposable Subscribe(IObserver<EventEntry> observer)
        {
 	        return _eventStream.Subscribe(observer);
        }
    }

    public class BlobInvocationLogCapture : InvocationLogCapture
    {
        private SinkSubscription<FlatFileSink> _eventSubscription;

        private readonly string _tempDirectory;
        private string _tempFile;
        
        public StorageHub Storage { get; private set; }

        public BlobInvocationLogCapture(InvocationState invocation, StorageHub storage)
            : base(invocation)
        {
            Storage = storage;

            _tempDirectory = Path.Combine(Path.GetTempPath(), "InvocationLogs");
        }

        public override async Task Start()
        {
            await base.Start();

            // Calculate paths
            if (!Directory.Exists(_tempDirectory))
            {
                Directory.CreateDirectory(_tempDirectory);
            }

            _tempFile = Path.Combine(_tempDirectory, Invocation.Id.ToString("N") + ".json");
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
            
            // Fetch the current logs if this is a continuation, we'll append to them during the invocation
            if (Invocation.IsContinuation)
            {
                await Storage.Primary.Blobs.DownloadBlob(WorkService.InvocationLogsContainerBaseName, "invocations/" + Path.GetFileName(_tempFile), _tempFile);
            }
            
            // Capture the events into a JSON file and a plain text file
            _eventSubscription = this.LogToFlatFile(_tempFile, new JsonEventTextFormatter(EventTextFormatting.Indented, dateTimeFormat: "O"));
        }

        public override async Task<Uri> End()
        {
            // Disconnect the listener
            _eventSubscription.Dispose();

            // Upload the file to blob storage
            var logBlob = await Storage.Primary.Blobs.UploadBlob("application/json", _tempFile, WorkService.InvocationLogsContainerBaseName, "invocations/" + Path.GetFileName(_tempFile));

            // Delete the temp files
            File.Delete(_tempFile);

            return logBlob.Uri;
        }
    }
}

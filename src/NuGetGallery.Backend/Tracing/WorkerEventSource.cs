using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend.Tracing
{
    [EventSource(Name = "NuGet-Worker")]
    public class WorkerEventSource : EventSource
    {
        public static readonly WorkerEventSource Log = new WorkerEventSource();

        private WorkerEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Message = "Worker Starting")]
        public void Starting() { WriteEvent(1); }

        [Event(
            eventId: 2,
            Level = EventLevel.Error,
            Message = "Worker encoutered a startup error: {0}.\r\nStack Trace: {1}.")]
        public void StartupError(string exception, string stackTrace) { WriteEvent(2, exception, stackTrace); }

        [Event(
            eventId: 3,
            Level = EventLevel.Critical,
            Message = "Worker encountered a fatal startup error: {0}.\r\nStack Trace: {1}.")]
        public void StartupFatal(string exception, string stackTrace) { WriteEvent(3, exception, stackTrace); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Message = "Worker Diagnostics Initialized")]
        public void DiagnosticsInitialized() { WriteEvent(4); }

        [Event(
            eventId: 5,
            Level = EventLevel.Error,
            Message = "Invalid Queue Message Received: {0}.\r\nException: {1}\r\nStack Trace: {2}")]
        public void InvalidQueueMessage(string message, string exception, string stackTrace) { WriteEvent(5, message, exception, stackTrace); }

        [Event(
            eventId: 6,
            Level = EventLevel.Error,
            Message = "Error reporting result of invocation {0}.\r\nException: {1}\r\nStack Trace: {2}")]
        public void ReportingFailure(string invocationId, string exception, string stackTrace) { WriteEvent(6, invocationId, exception, stackTrace); }

        [NonEvent]
        public void ReportingFailure(Guid invocationId, Exception ex) { ReportingFailure(invocationId.ToString("N"), ex.ToString(), ex.StackTrace); }
    }
}

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

#pragma warning disable 0618
        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Message = "Worker Starting")]
        public void Starting() { WriteEvent(1); }

        [Event(
            eventId: 2,
            Level = EventLevel.Error,
            Message = "Worker encoutered a startup error: {0}.\r\nStack Trace: {1}.")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void StartupError(string exception, string stackTrace) { WriteEvent(2, exception, stackTrace); }

        [NonEvent]
        public void StartupError(Exception ex) { StartupError(ex.ToString(), ex.StackTrace); }

        [Event(
            eventId: 3,
            Level = EventLevel.Critical,
            Message = "Worker encountered a fatal startup error: {0}.\r\nStack Trace: {1}.")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void StartupFatal(string exception, string stackTrace) { WriteEvent(3, exception, stackTrace); }

        [NonEvent]
        public void StartupFatal(Exception ex) { StartupFatal(ex.ToString(), ex.StackTrace); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Message = "Worker Diagnostics Initialized")]
        public void DiagnosticsInitialized() { WriteEvent(4); }

        [Event(
            eventId: 5,
            Level = EventLevel.Error,
            Message = "Invalid Queue Message Received: {0}.\r\nException: {1}\r\nStack Trace: {2}")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void InvalidQueueMessage(string message, string exception, string stackTrace) { WriteEvent(5, message, exception, stackTrace); }

        [NonEvent]
        public void InvalidQueueMessage(string message, Exception ex) { InvalidQueueMessage(message, ex.ToString(), ex.StackTrace); }

        [NonEvent]
        public void InvalidQueueMessage(string message, string error) { InvalidQueueMessage(message, error, ""); }

        [Event(
            eventId: 6,
            Level = EventLevel.Error,
            Message = "Error reporting result of invoking {1} (ID: {0}).\r\nException: {1}\r\nStack Trace: {2}")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void ReportingFailure(string invocationId, string jobName, string exception, string stackTrace) { WriteEvent(6, invocationId, jobName, exception, stackTrace); }

        [NonEvent]
        public void ReportingFailure(JobResponse response, Exception ex) { ReportingFailure(response.Invocation.Id.ToString("N"), response.Invocation.Request.Name, ex.ToString(), ex.StackTrace); }

        [Event(
            eventId: 7,
            Message = "{0} job discovered. Runtime: {1}",
            Level = EventLevel.Informational)]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void JobDiscovered(string jobName, string runtime) { WriteEvent(7, jobName, runtime); }

        [NonEvent]
        public void JobDiscovered(Job instance) { JobDiscovered(instance.Name, instance.GetType().AssemblyQualifiedName); }

        [Event(
            eventId: 8,
            Message = "Dispatching Invocation of {0}. Id: {1}",
            Level = EventLevel.Informational)]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void DispatchingRequest(string jobName, string invocationId) { WriteEvent(8, jobName, invocationId); }

        [NonEvent]
        public void DispatchingRequest(JobInvocation invocation) { DispatchingRequest(invocation.Request.Name, invocation.Id.ToString("N")); }

        [Event(
            eventId: 9,
            Message = "Worker is stopping",
            Level = EventLevel.Informational)]
        public void Stopping() { WriteEvent(9); }

        [Event(
            eventId: 10,
            Message = "Worker has started",
            Level = EventLevel.Informational)]
        public void StartupComplete() { WriteEvent(10); }

        [Event(
            eventId: 11,
            Level = EventLevel.Informational,
            Message = "Worker Diagnostics Initializing")]
        public void DiagnosticsInitializing() { WriteEvent(11); }

        [Event(
            eventId: 12,
            Level = EventLevel.Error,
            Message = "Worker encoutered an error while initializing diagnostics: {0}.\r\nStack Trace: {1}.")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void DiagnosticsInitializationError(string exception, string stackTrace) { WriteEvent(12, exception, stackTrace); }
        
        [NonEvent]
        public void DiagnosticsInitializationError(Exception ex) { DiagnosticsInitializationError(ex.ToString(), ex.StackTrace); }

        [Event(
            eventId: 13,
            Level = EventLevel.Informational,
            Message = "Initializing Diagnostics for {0} job.")]
        public void DiagnosticsRegisterJob(string jobName) { WriteEvent(13, jobName); }

        [Event(
            eventId: 14,
            Level = EventLevel.Error,
            Message = "Worker encoutered an error while initializing diagnostics for the {0} job: {1}.\r\nStack Trace: {2}.")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void DiagnosticsJobRegisterError(string jobName, string exception, string stackTrace) { WriteEvent(14, jobName, exception, stackTrace); }

        [NonEvent]
        public void DiagnosticsJobRegisterError(string jobName, Exception ex) { DiagnosticsJobRegisterError(jobName, ex.ToString(), ex.StackTrace); }

        [Event(
            eventId: 15,
            Level = EventLevel.Informational,
            Message = "Initialized Diagnostics for {0} job. Table: {1}")]
        public void DiagnosticsJobRegistered(string jobName, string tableName) { WriteEvent(15, jobName, tableName); }

        [Event(
            eventId: 16,
            Level = EventLevel.Informational,
            Message = "Received Job Request Queue Message {0}. Inserted at: {1}")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void RequestReceived(string messageId, string insertedAt) { WriteEvent(16, messageId, insertedAt); }
        
        [NonEvent]
        public void RequestReceived(string messageId, DateTimeOffset? insertionTime) { RequestReceived(messageId, insertionTime.HasValue ? insertionTime.Value.ToString("s") : "<<unknown>>"); }

        [Event(
            eventId: 17,
            Level = EventLevel.Informational,
            Message = "Error dispatching {0} job.\r\nException: {1}\r\nStack Trace: {2}")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void DispatchError(string jobName, string exception, string stackTrace) { WriteEvent(17, jobName, exception, stackTrace); }

        [NonEvent]
        public void DispatchError(JobRequest req, Exception ex) { DispatchError(req.Name, ex.ToString(), ex.StackTrace); }

        [Event(
            eventId: 18,
            Level = EventLevel.Informational,
            Message = "Invocation {0} of {1} job completed at {2}. Status: {3}.\r\nException: {4}\r\nStack Trace: {5}")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void JobExecuted(string invocationId, string jobName, string completedAt, string status, string exception, string stackTrace) { WriteEvent(18, invocationId, jobName, completedAt, status, exception, stackTrace); }

        [NonEvent]
        public void JobExecuted(JobResponse response) {
            JobExecuted(
                response.Invocation.Id.ToString("N"),
                response.Invocation.Request.Name,
                response.CompletedAt.ToString("s"),
                response.Result.Status.ToString(),
                response.Result.Exception == null ? String.Empty : response.Result.Exception.ToString(),
                response.Result.Exception == null ? String.Empty : response.Result.Exception.StackTrace);
        }

        [Event(
            eventId: 19,
            Level = EventLevel.Critical,
            Message = "Job Request expired while job was executing. Job: {0}, Message ID: {1}. Elapsed: {2}")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void JobRequestExpired(string jobName, string messageId, string elapsed) { WriteEvent(19, jobName, messageId, elapsed); }

        [NonEvent]
        public void JobRequestExpired(JobRequest req, string messageId, TimeSpan elapsed) { JobRequestExpired(req.Name, messageId, elapsed.ToString()); }

        [Event(
            eventId: 20,
            Level = EventLevel.Informational,
            Message = "Worker is ready to dispatch events")]
        public void WorkerDispatching() { WriteEvent(20); }

        [Event(
            eventId: 21,
            Level = EventLevel.Verbose,
            Message = "Work Queue is empty. Sleeping for {0}")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void QueueEmpty(string sleepInterval) { WriteEvent(21, sleepInterval); }

        [NonEvent]
        public void QueueEmpty(TimeSpan timeSpan) { QueueEmpty(timeSpan.ToString()); }

        [Event(
            eventId: 22,
            Level = EventLevel.Informational,
            Message = "Exception Parsing Message: {0}\r\nException: {1}\r\nStack Trace: {2}")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void MessageParseError(string message, string exception, string stackTrace) { WriteEvent(22, message, exception, stackTrace); }

        [NonEvent]
        public void MessageParseError(string message, Exception ex) { MessageParseError(message, ex.ToString(), ex.StackTrace); }

        [Event(
            eventId: 23,
            Level = EventLevel.Informational,
            Message = "Job {0} started execution. Invocation: {1}")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void JobStarted(string jobName, string invocationId) { WriteEvent(23, jobName, invocationId); }
        [NonEvent]
        public void JobStarted(string jobName, Guid invocationId) { JobStarted(jobName, invocationId.ToString("N")); }

        [Event(
            eventId: 24,
            Level = EventLevel.Informational,
            Message = "Job {0} completed. Invocation: {1}")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void JobCompleted(string jobName, string invocationId) { WriteEvent(24, jobName, invocationId); }
        [NonEvent]
        public void JobCompleted(string jobName, Guid invocationId) { JobCompleted(jobName, invocationId.ToString("N")); }

        [Event(
            eventId: 25,
            Level = EventLevel.Error,
            Message = "Job {0} failed. Exception: {1}\r\nStack Trace: {2}\r\nInvocation: {3}")]
        [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
        public void JobFaulted(string jobName, string exception, string stackTrace, string invocationId) { WriteEvent(25, jobName, exception, stackTrace, invocationId); }
        [NonEvent]
        public void JobFaulted(string jobName, Exception ex, Guid invocationId) { JobFaulted(jobName, ex.ToString(), ex.StackTrace, invocationId.ToString("N")); }

#pragma warning restore 0618
    }
}

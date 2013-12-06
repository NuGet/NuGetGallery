using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs.Monitoring
{
    [EventSource(Name = "NuGet-Services-Jobs")]
    public class JobsServiceEventSource : EventSource
    {
        public static readonly JobsServiceEventSource Log = new JobsServiceEventSource();

        private JobsServiceEventSource() { }

        public static class Tasks
        {
            public const EventTask Startup = (EventTask)1;
            public const EventTask Shutdown = (EventTask)2;
            public const EventTask Dispatching = (EventTask)3;
        }

        [Event(
            eventId: 2,
            Level = EventLevel.Critical,
            Task = Tasks.Startup,
            Opcode = EventOpcode.Stop,
            Message = "Jobs Service encountered a fatal startup error: {0}")]
        private void StartupError(string exception) { WriteEvent(2, exception); }

        [NonEvent]
        public void StartupError(Exception ex) { StartupError(ex.ToString()); }

        [Event(
            eventId: 3,
            Message = "Jobs Service has started",
            Task = Tasks.Startup,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational)]
        public void StartupComplete() { WriteEvent(3); }

        [Event(
            eventId: 4,
            Message = "{0} job discovered. Runtime: {1}",
            Level = EventLevel.Informational)]
        private void JobDiscovered(string jobName, string runtime) { WriteEvent(4, jobName, runtime); }

        [NonEvent]
        public void JobDiscovered(JobDescription job) { JobDiscovered(job.Name, job.GetType().AssemblyQualifiedName); }

        [Event(
            eventId: 7,
            Task = Tasks.Dispatching,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Jobs Service has begun dispatching events")]
        public void DispatchLoopStarted() { WriteEvent(7); }

        [Event(
            eventId: 8,
            Task = Tasks.Dispatching,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Jobs Service has stopped dispatching events")]
        public void DispatchLoopEnded() { WriteEvent(8); }

        [Event(
            eventId: 9,
            Level = EventLevel.Critical,
            Task = Tasks.Startup,
            Opcode = EventOpcode.Stop,
            Message = "Jobs Service encountered a fatal error in the dispatch loop: {0}")]
        private void DispatchLoopError(string exception) { WriteEvent(9, exception); }

        [NonEvent]
        public void DispatchLoopError(Exception ex) { DispatchLoopError(ex.ToString()); }

        [Event(
            eventId: 10,
            Task = Tasks.Dispatching,
            Opcode = EventOpcode.Suspend,
            Level = EventLevel.Verbose,
            Message = "Work Queue is empty. Jobs Service is suspending for {0}")]
        private void DispatchLoopWaiting(string sleepInterval) { WriteEvent(10, sleepInterval); }

        [NonEvent]
        public void DispatchLoopWaiting(TimeSpan timeSpan) { DispatchLoopWaiting(timeSpan.ToString()); }

        [Event(
            eventId: 11,
            Task = Tasks.Dispatching,
            Opcode = EventOpcode.Resume,
            Level = EventLevel.Verbose,
            Message = "Jobs Service has resumed dispatching events")]
        public void DispatchLoopResumed() { WriteEvent(11); }

        [Event(
            eventId: 12,
            Level = EventLevel.Error,
            Message = "Error retrieving queue message: {0}")]
        private void ErrorRetrievingInvocation(string exception) { WriteEvent(12, exception); }

        [NonEvent]
        public void ErrorRetrievingInvocation(Exception ex) { ErrorRetrievingInvocation(ex.ToString()); }

        [Event(
            eventId: 13,
            Level = EventLevel.Informational,
            Message = "Invocation {0} of job {1} was cancelled at {2}. Reason: {3}")]
        private void Cancelled(Guid id, string job, string timestamp, string reason) { WriteEvent(13, id, job, timestamp, reason); }

        [NonEvent]
        public void Cancelled(Invocation invocation) { Cancelled(invocation.Id, invocation.Job, invocation.Timestamp.ToString("O"), invocation.StatusMessage); }
    }
}

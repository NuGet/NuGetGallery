using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Jobs;

namespace NuGetGallery.Backend.Monitoring
{
    [EventSource(Name = "NuGet-Worker")]
    public class WorkerEventSource : EventSource
    {
        public static readonly WorkerEventSource Log = new WorkerEventSource();

        private WorkerEventSource() { }

        public static class Tasks
        {
            public const EventTask Startup = (EventTask)1;
            public const EventTask Shutdown = (EventTask)2;
            public const EventTask Dispatching = (EventTask)3;
        }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Task = Tasks.Startup,
            Opcode = EventOpcode.Start,
            Message = "Worker Starting")]
        public void Starting() { WriteEvent(1); }

        [Event(
            eventId: 2,
            Level = EventLevel.Critical,
            Task = Tasks.Startup,
            Opcode = EventOpcode.Stop,
            Message = "Worker encountered a fatal startup error: {0}")]
        private void StartupError(string exception) { WriteEvent(2, exception); }

        [NonEvent]
        public void StartupError(Exception ex) { StartupError(ex.ToString()); }

        [Event(
            eventId: 3,
            Message = "Worker has started",
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
        public void JobDiscovered(JobBase instance) { JobDiscovered(instance.Name, instance.GetType().AssemblyQualifiedName); }

        [Event(
            eventId: 5,
            Task = Tasks.Shutdown,
            Opcode = EventOpcode.Start,
            Message = "Worker is stopping",
            Level = EventLevel.Informational)]
        public void Stopping() { WriteEvent(5); }

        [Event(
            eventId: 6,
            Task = Tasks.Shutdown,
            Opcode = EventOpcode.Stop,
            Message = "Worker has stopped",
            Level = EventLevel.Informational)]
        public void Stopped() { WriteEvent(6); }

        [Event(
            eventId: 7,
            Task = Tasks.Dispatching,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Worker has begun dispatching events")]
        public void DispatchLoopStarted() { WriteEvent(7); }

        [Event(
            eventId: 8,
            Task = Tasks.Dispatching,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Worker has stopped dispatching events")]
        public void DispatchLoopEnded() { WriteEvent(8); }

        [Event(
            eventId: 9,
            Level = EventLevel.Critical,
            Task = Tasks.Startup,
            Opcode = EventOpcode.Stop,
            Message = "Worker encountered a fatal error in the dispatch loop: {0}")]
        private void DispatchLoopError(string exception) { WriteEvent(9, exception); }

        [NonEvent]
        public void DispatchLoopError(Exception ex) { DispatchLoopError(ex.ToString()); }

        [Event(
            eventId: 10,
            Task = Tasks.Dispatching,
            Opcode = EventOpcode.Suspend,
            Level = EventLevel.Verbose,
            Message = "Work Queue is empty. Worker is suspending for {0}")]
        private void DispatchLoopWaiting(string sleepInterval) { WriteEvent(10, sleepInterval); }

        [NonEvent]
        public void DispatchLoopWaiting(TimeSpan timeSpan) { DispatchLoopWaiting(timeSpan.ToString()); }

        [Event(
            eventId: 11,
            Task = Tasks.Dispatching,
            Opcode = EventOpcode.Resume,
            Level = EventLevel.Verbose,
            Message = "Worker has resumed dispatching events")]
        public void DispatchLoopResumed() { WriteEvent(11); }

        [Event(
            eventId: 12,
            Level = EventLevel.Error,
            Message = "Invalid Queue Message Received: {0}. Exception: {1}")]
        private void InvalidQueueMessage(string message, string exception) { WriteEvent(12, message, exception); }

        [NonEvent]
        public void InvalidQueueMessage(string message, Exception ex) { InvalidQueueMessage(message, ex.ToString()); }
    }
}

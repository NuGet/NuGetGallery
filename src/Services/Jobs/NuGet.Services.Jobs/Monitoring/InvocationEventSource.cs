using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs.Monitoring
{
    [EventSource(Name = "NuGet-Worker-Invocation")]
    public class InvocationEventSource : EventSource
    {
        public static readonly InvocationEventSource Log = new InvocationEventSource();

        private InvocationEventSource() { }

        public static class Tasks {
            public const EventTask Invocation = (EventTask)1;
        }

        [Event(
            eventId: 1,
            Task = Tasks.Invocation,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Invocation {0} started.")]
        private void Started(Guid invocationId) { WriteEvent(1, invocationId); }

        [NonEvent]
        public void Started() { Started(InvocationContext.GetCurrentInvocationId()); }

        [Event(
            eventId: 2,
            Task = Tasks.Invocation,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Invocation {0} ended.")]
        private void Ended(Guid invocationId) { WriteEvent(2, invocationId); }

        [NonEvent]
        public void Ended() { Ended(InvocationContext.GetCurrentInvocationId()); }

        [Event(
            eventId: 3,
            Level = EventLevel.Critical,
            Message = "Invocation took too long. Adjust the visibility timeout for the job. Invocation: {0}. Job: {1}. Queued At: {3}. Expired At: {4}")]
        private void InvocationTookTooLong(Guid invocationId, string jobName, string messageId, string inserted, string expired) { WriteEvent(3, invocationId, jobName, messageId, inserted, expired); }

        [NonEvent]
        public void InvocationTookTooLong(InvocationRequest request)
        {
            if (request.Message != null) // Request is not guaranteed to have a message
            {
                InvocationTookTooLong(InvocationContext.GetCurrentInvocationId(), request.Invocation.Job, request.Message.Id, request.Message.InsertionTime.HasValue ? request.Message.InsertionTime.Value.ToString("O") : String.Empty, request.Message.NextVisibleTime.HasValue ? request.Message.NextVisibleTime.Value.ToString("O") : String.Empty);
            }
        }

        [Event(
            eventId: 4,
            Level = EventLevel.Critical,
            Message = "Error Dispatching Invocation {0}: {1}")]
        private void DispatchError(Guid invocationId, string exception) { WriteEvent(4, invocationId, exception); }

        [NonEvent]
        public void DispatchError(Exception ex) { DispatchError(InvocationContext.GetCurrentInvocationId(), ex.ToString()); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Message = "Invoking invocation {0}. Job: {1}. Runtime: {2}")]
        private void Invoking(Guid invocationId, string jobName, string jobRuntime) { WriteEvent(5, invocationId, jobName, jobRuntime); }

        [NonEvent]
        public void Invoking(JobDefinition jobdef) { Invoking(InvocationContext.GetCurrentInvocationId(), jobdef.Description.Name, jobdef.Description.Runtime); }

        [Event(
            eventId: 6,
            Level = EventLevel.Error,
            Message = "Parameter binding error during invocation {0}: {1}")]
        private void BindingError(Guid invocationId, string exception) { WriteEvent(6, invocationId, exception); }

        [NonEvent]
        public void BindingError(Exception ex) { BindingError(InvocationContext.GetCurrentInvocationId(), ex.ToString()); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Message = "Invocation {0} succeeded at {1}")]
        private void Succeeded(Guid invocationId, string completedAt) { WriteEvent(7, invocationId, completedAt); }

        [NonEvent]
        public void Succeeded(InvocationResult result) { Succeeded(InvocationContext.GetCurrentInvocationId(), DateTimeOffset.UtcNow.ToString("O")); }

        [Event(
            eventId: 8,
            Level = EventLevel.Error,
            Message = "Invocation {0} faulted at {1}: {2}")]
        private void Faulted(Guid invocationId, string completedAt, string exception) { WriteEvent(8, invocationId, completedAt, exception); }

        [NonEvent]
        public void Faulted(InvocationResult result) { Faulted(InvocationContext.GetCurrentInvocationId(), DateTimeOffset.UtcNow.ToString("O"), result.Exception != null ? result.Exception.ToString() : ""); }

        [Event(
            eventId: 9,
            Level = EventLevel.Critical,
            Message = "Invocation {0} entered unrecognized status {2} at {1}")]
        private void UnknownStatus(Guid invocationId, string completedAt, string status) { WriteEvent(9, invocationId, completedAt, status); }

        [NonEvent]
        public void UnknownStatus(InvocationResult result) { UnknownStatus(InvocationContext.GetCurrentInvocationId(), DateTimeOffset.UtcNow.ToString("O"), result.Result.ToString()); }

        [Event(
            eventId: 10,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Suspend,
            Task = Tasks.Invocation,
            Message = "Invocation {0} was suspended for {2} to wait for an async completion at {1}")]
        private void Suspended(Guid invocationId, string suspendedAt, string waitingFor) { WriteEvent(10, invocationId, suspendedAt, waitingFor); }

        [NonEvent]
        public void Suspended(InvocationResult result) { Suspended(InvocationContext.GetCurrentInvocationId(), DateTimeOffset.UtcNow.ToString("O"), result.Continuation != null ? result.Continuation.WaitPeriod.ToString() : "<UNKNOWN>"); }

        [Event(
            eventId: 11,
            Level = EventLevel.Warning,
            Message = "No event source found for {1} job. (Invocation {0})")]
        private void NoEventSource(Guid invocationId, string jobName) { WriteEvent(11, invocationId, jobName); }

        [NonEvent]
        public void NoEventSource(string jobName) { NoEventSource(InvocationContext.GetCurrentInvocationId(), jobName); }

        [Event(
            eventId: 12,
            Task = Tasks.Invocation,
            Opcode = EventOpcode.Resume,
            Level = EventLevel.Informational,
            Message = "Invocation {0} resumed.")]
        private void Resumed(Guid invocationId) { WriteEvent(12, invocationId); }

        [NonEvent]
        public void Resumed() { Resumed(InvocationContext.GetCurrentInvocationId()); }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Jobs;

namespace NuGetGallery.Backend.Monitoring
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
        public void Started() { Started(JobInvocationContext.GetCurrentInvocationId()); }

        [Event(
            eventId: 2,
            Task = Tasks.Invocation,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Invocation {0} ended.")]
        private void Ended(Guid invocationId) { WriteEvent(2, invocationId); }

        [NonEvent]
        public void Ended() { Ended(JobInvocationContext.GetCurrentInvocationId()); }

        [Event(
            eventId: 3,
            Level = EventLevel.Critical,
            Message = "Request expired while job was executing. Invocation: {0}. Job: {1}. Message ID: {2}. Inserted: {3}. Expired: {4}")]
        private void RequestExpired(Guid invocationId, string jobName, string messageId, string inserted, string expired) { WriteEvent(3, invocationId, jobName, messageId, inserted, expired); }

        [NonEvent]
        public void RequestExpired(JobRequest request) { RequestExpired(JobInvocationContext.GetCurrentInvocationId(), request.Name, request.Message.Id, request.InsertionTime.ToString("O"), request.ExpiresAt.HasValue ? request.ExpiresAt.Value.ToString("O") : ""); }

        [Event(
            eventId: 4,
            Level = EventLevel.Critical,
            Message = "Error Dispatching Invocation {0}: {1}")]
        private void DispatchError(Guid invocationId, string exception) { WriteEvent(4, invocationId, exception); }

        [NonEvent]
        public void DispatchError(Exception ex) { DispatchError(JobInvocationContext.GetCurrentInvocationId(), ex.ToString()); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Message = "Invoking invocation {0}. Job: {1}. Runtime: {2}")]
        private void Invoking(Guid invocationId, string jobName, string jobRuntime) { WriteEvent(5, invocationId, jobName, jobRuntime); }

        [NonEvent]
        public void Invoking(JobDescription job) { Invoking(JobInvocationContext.GetCurrentInvocationId(), job.Name, job.Runtime); }

        [Event(
            eventId: 6,
            Level = EventLevel.Error,
            Message = "Parameter binding error during invocation {0}: {1}")]
        private void BindingError(Guid invocationId, string exception) { WriteEvent(6, invocationId, exception); }

        [NonEvent]
        public void BindingError(Exception ex) { BindingError(JobInvocationContext.GetCurrentInvocationId(), ex.ToString()); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Message = "Invocation {0} succeeded at {1}")]
        private void Succeeded(Guid invocationId, string completedAt) { WriteEvent(7, invocationId, completedAt); }

        [NonEvent]
        public void Succeeded(JobResponse response) { Succeeded(JobInvocationContext.GetCurrentInvocationId(), response.CompletedAt.ToString("O")); }

        [Event(
            eventId: 8,
            Level = EventLevel.Error,
            Message = "Invocation {0} faulted at {1}: {2}")]
        private void Faulted(Guid invocationId, string completedAt, string exception) { WriteEvent(8, invocationId, completedAt, exception); }

        [NonEvent]
        public void Faulted(JobResponse response) { Faulted(JobInvocationContext.GetCurrentInvocationId(), response.CompletedAt.ToString("O"), response.Result.Exception.ToString()); }

        [Event(
            eventId: 9,
            Level = EventLevel.Critical,
            Message = "Invocation {0} entered unrecognized status {2} at {1}")]
        private void UnknownStatus(Guid invocationId, string completedAt, string status) { WriteEvent(9, invocationId, completedAt, status); }

        [NonEvent]
        public void UnknownStatus(JobResponse response) { UnknownStatus(JobInvocationContext.GetCurrentInvocationId(), response.CompletedAt.ToString("O"), response.Result.Status.ToString()); }

        [Event(
            eventId: 10,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Suspend,
            Task = Tasks.Invocation,
            Message = "Invocation {0} was suspended for {2} to wait for an async completion at {1}")]
        private void AwaitingContinuation(Guid invocationId, string suspendedAt, string waitingFor) { WriteEvent(10, invocationId, suspendedAt, waitingFor); }

        [NonEvent]
        public void AwaitingContinuation(JobResponse response) { AwaitingContinuation(JobInvocationContext.GetCurrentInvocationId(), response.CompletedAt.ToString("O"), response.Result.Continuation.WaitPeriod.ToString()); }

        [Event(
            eventId: 11,
            Level = EventLevel.Warning,
            Message = "No event source found for {1} job. (Invocation {0})")]
        private void NoEventSource(Guid invocationId, string jobName) { WriteEvent(11, invocationId, jobName); }

        [NonEvent]
        public void NoEventSource(string jobName) { NoEventSource(JobInvocationContext.GetCurrentInvocationId(), jobName); }

        [Event(
            eventId: 12,
            Task = Tasks.Invocation,
            Opcode = EventOpcode.Resume,
            Level = EventLevel.Informational,
            Message = "Invocation {0} resumed.")]
        private void Resumed(Guid invocationId) { WriteEvent(12, invocationId); }

        [NonEvent]
        public void Resumed() { Resumed(JobInvocationContext.GetCurrentInvocationId()); }
    }
}

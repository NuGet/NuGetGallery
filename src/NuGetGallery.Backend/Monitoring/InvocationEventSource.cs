using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Jobs;

namespace NuGetGallery.Backend.Monitoring
{
    [EventSource(Name = "NuGet-Worker-Invocation")]
    public class InvocationEventSource : EventSource
    {
        private Guid _invocationId;

        public InvocationEventSource(Guid invocationId)
        {
            _invocationId = invocationId;
        }

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
        public void Started() { Started(_invocationId); }

        [Event(
            eventId: 2,
            Task = Tasks.Invocation,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Invocation {0} ended.")]
        private void Ended(Guid invocationId) { WriteEvent(2, invocationId); }

        [NonEvent]
        public void Ended() { Ended(_invocationId); }

        [Event(
            eventId: 3,
            Level = EventLevel.Critical,
            Message = "Request expired while job was executing. Invocation: {0}. Job: {1}. Message ID: {2}. Inserted: {3}. Expired: {4}")]
        private void RequestExpired(Guid invocationId, string jobName, string messageId, string inserted, string expired) { WriteEvent(3, jobName, messageId, inserted, expired); }

        [NonEvent]
        public void RequestExpired(JobRequest request) { RequestExpired(_invocationId, request.Name, request.Message.Id, request.InsertionTime.ToString("O"), request.ExpiresAt.HasValue ? request.ExpiresAt.Value.ToString("O") : ""); }

        [Event(
            eventId: 4,
            Level = EventLevel.Critical,
            Message = "Error Dispatching Invocation {0}: {1}")]
        private void DispatchError(Guid invocationId, string exception) { WriteEvent(4, exception); }

        [NonEvent]
        public void DispatchError(Exception ex) { DispatchError(_invocationId, ex.ToString()); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Message = "Invoking invocation {0}. Job: {1}. Runtime: {2}")]
        private void Invoking(Guid invocationId, string jobName, string jobRuntime) { WriteEvent(5, invocationId, jobName, jobRuntime); }

        [NonEvent]
        public void Invoking(Job job) { Invoking(_invocationId, job.Name, job.GetType().AssemblyQualifiedName); }

        [Event(
            eventId: 6,
            Level = EventLevel.Error,
            Message = "Parameter binding error during invocation {0}: {1}")]
        private void BindingError(Guid invocationId, string exception) { WriteEvent(6, invocationId, exception); }

        [NonEvent]
        public void BindingError(Exception ex) { BindingError(_invocationId, ex.ToString()); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Message = "Invocation {0} succeeded at {1}")]
        private void Succeeded(Guid invocationId, string completedAt) { WriteEvent(7, invocationId, completedAt); }

        [NonEvent]
        public void Succeeded(JobResponse response) { Succeeded(_invocationId, response.CompletedAt.ToString("O")); }

        [Event(
            eventId: 8,
            Level = EventLevel.Error,
            Message = "Invocation {0} faulted at {1}: {2}")]
        private void Faulted(Guid invocationId, string completedAt, string exception) { WriteEvent(8, invocationId, completedAt, exception); }

        [NonEvent]
        public void Faulted(JobResponse response) { Faulted(_invocationId, response.CompletedAt.ToString("O"), response.Result.Exception.ToString()); }
    }
}

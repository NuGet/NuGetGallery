using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;

namespace NuGetGallery.Backend.Tracing
{
    /// <summary>
    /// Base class for Job event sources. Inherit from this and add ETL Events starting from the BaseId constant.
    /// </summary>
    public abstract class JobEventSource : EventSource
    {
        private string _jobName;

        public const int BaseId = 1000;

        protected JobEventSource(string jobName) { _jobName = jobName; }

#pragma warning disable 0618
        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Message = "Job {0} started execution. Invocation: {1}",
            Task = Tasks.Job)]
        [Obsolete("Do not call this method, use JobStarted(Guid). This method is here because ETL uses the method signature to build the manifest")]
        public void JobStarted(string jobName, string invocationId) { WriteEvent(1, jobName, invocationId); }
        [NonEvent]
        public void JobStarted(Guid invocationId) { JobStarted(_jobName, invocationId.ToString("N")); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Message = "Job {0} completed. Invocation: {1}",
            Task = Tasks.Job)]
        [Obsolete("Do not call this method, use JobCompleted(Guid). This method is here because ETL uses the method signature to build the manifest")]
        public void JobCompleted(string jobName, string invocationId) { WriteEvent(2, jobName, invocationId); }
        [NonEvent]
        public void JobCompleted(Guid invocationId) { JobCompleted(_jobName, invocationId.ToString("N")); }

        [Event(
            eventId: 3,
            Level = EventLevel.Error,
            Opcode = EventOpcode.Stop,
            Message = "Job {0} failed. Exception: {1}\r\nStack Trace: {2}\r\nInvocation: {3}",
            Task = Tasks.Job)]
        [Obsolete("Do not call this method, use JobFaulted(Exception,Guid). This method is here because ETL uses the method signature to build the manifest")]
        public void JobFaulted(string jobName, string exception, string stackTrace, string invocationId) { WriteEvent(3, jobName, exception, stackTrace, invocationId); }
        [NonEvent]
        public void JobFaulted(Exception ex, Guid invocationId) { JobFaulted(_jobName, ex.ToString(), ex.StackTrace, invocationId.ToString("N")); }
#pragma warning restore 0618

        public class Tasks
        {
            public const EventTask Job = (EventTask)0x01;
        }
    }

    public abstract class JobEventSource<TJob> : JobEventSource
        where TJob : Job, new()
    {
        public JobEventSource() : base(GetJobName()) { }

        private static string GetJobName()
        {
            var job = new TJob();
            return job.Name;
        }
    }
}

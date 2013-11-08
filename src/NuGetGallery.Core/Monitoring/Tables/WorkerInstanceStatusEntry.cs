using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGetGallery.Jobs;

namespace NuGetGallery.Monitoring.Tables
{
    // Partitioning Strategy:
    //  One big partition containing all instances. 
    //  Why? Because we will have one row per instance in the system, there will be on the order of 10s of instances, so this is fine
    public class WorkerInstanceStatusEntry : TableEntity
    {
        public string InstanceName { get { return RowKey; } }

        [Obsolete("Don't call this constructor directly, it is only for deserialization", error: true)]
        public WorkerInstanceStatusEntry() { }

        public WorkerInstanceStatusEntry(string instanceName, DateTimeOffset timestamp)
            : base("0", instanceName)
        {
            LastInvocation = Guid.Empty;
            Timestamp = timestamp;
        }

        public WorkerInstanceStatusEntry(string instanceName, DateTimeOffset timestamp, BackendInstanceStatus instanceStatus)
            : this(instanceName, timestamp)
        {
            Status = instanceStatus;
        }

        public WorkerInstanceStatusEntry(string instanceName, DateTimeOffset timestamp, BackendInstanceStatus instanceStatus, Guid lastInvocation, string lastJob)
            : this(instanceName, timestamp, instanceStatus)
        {
            LastInvocation = lastInvocation;
            LastJob = lastJob;
            LastInvocationStatus = JobStatus.Executing;
        }

        public WorkerInstanceStatusEntry(string instanceName, DateTimeOffset timestamp, BackendInstanceStatus instanceStatus, Guid lastInvocation, string lastJob, JobResult lastInvocationResult, DateTimeOffset lastInvocationCompletedAt)
            : this(instanceName, timestamp, instanceStatus, lastInvocation, lastJob)
        {
            LastInvocationStatus = lastInvocationResult.Status;
            LastInvocationException = lastInvocationResult.Exception == null ? null : lastInvocationResult.Exception.ToString();
            LastInvocationCompletedAt = lastInvocationCompletedAt;
        }

        public Guid LastInvocation { get; set; }
        public string LastJob { get; set; }
        public DateTimeOffset? LastInvocationCompletedAt { get; set; }
        public string LastInvocationException { get; set; }

        [Obsolete("Use the LastInvocationStatus property to read/write this value")]
        public string LastInvocationStatusName { get; set; }

        [IgnoreProperty]
        public JobStatus LastInvocationStatus {
#pragma warning disable 0618
            get { return (JobStatus)Enum.Parse(typeof(JobStatus), LastInvocationStatusName); }
            set { LastInvocationStatusName = value.ToString(); }
#pragma warning restore 0618
        }

        [Obsolete("Use the Status property to read/write this value")]
        public string StatusName { get; set; }

        [IgnoreProperty]
        public BackendInstanceStatus Status
        {
#pragma warning disable 0618
            get { return (BackendInstanceStatus)Enum.Parse(typeof(BackendInstanceStatus), StatusName); }
            set { StatusName = value.ToString(); }
#pragma warning restore 0618
        }
    }

    public enum BackendInstanceStatus
    {
        Started,
        Idle,
        Executing
    }
}

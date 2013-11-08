using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGetGallery.Jobs;

namespace NuGetGallery.Monitoring.Tables
{
    // Partitioning Strategy:
    //  One big partition containing all job entries. 
    //  Why? Because we will have one row per UNIQUE job in the system, there will be on the order of 10s of jobs, so this is fine
    public class JobStatusEntry : TableEntity
    {
        public string JobName { get { return RowKey; } }

        [Obsolete("Don't call this constructor directly, it is only for deserialization", error: true)]
        public JobStatusEntry() { }
        public JobStatusEntry(string jobName, DateTimeOffset timestamp)
            : base("0", jobName)
        {
            Timestamp = timestamp;
            LastInvocationStatus = JobStatus.Unspecified;
            LastInvocationId = Guid.Empty;
            LastInstanceName = null;
        }

        public JobStatusEntry(string jobName, DateTimeOffset timestamp, Guid lastInvocationId, string lastInstanceName)
            : this(jobName, timestamp)
        {
            LastInvocationId = lastInvocationId;
            LastInstanceName = lastInstanceName;
            LastInvocationStatus = JobStatus.Executing;
        }

        public JobStatusEntry(string jobName, DateTimeOffset timestamp, Guid lastInvocationId, JobResult lastInvocationResult, string lastInstanceName, DateTimeOffset lastInvocationCompletedAt)
            : this(jobName, timestamp, lastInvocationId, lastInstanceName)
        {
            LastInvocationStatus = lastInvocationResult.Status;
            LastInvocationException = lastInvocationResult.Exception == null ? null : lastInvocationResult.Exception.ToString();
            LastInvocationCompletedAt = lastInvocationCompletedAt;
        }

        public Guid LastInvocationId { get; set; }
        public string LastInstanceName { get; set; }
        public DateTimeOffset? LastInvocationCompletedAt { get; set; }
        public string LastInvocationException { get; set; }

        [Obsolete("Use the LastInvocationStatus property to read/write this value")]
        public string LastInvocationStatusName { get; set; }

        [IgnoreProperty]
        public JobStatus LastInvocationStatus
        {
#pragma warning disable 0618
            get { return (JobStatus)Enum.Parse(typeof(JobStatus), LastInvocationStatusName); }
            set { LastInvocationStatusName = value.ToString(); }
#pragma warning restore 0618
        }
    }
}

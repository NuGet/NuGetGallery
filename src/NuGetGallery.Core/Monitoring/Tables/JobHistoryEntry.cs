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
    //  One partition per job
    //  This table will usually be used to look up the history for a particular job, so queries will be for one partition at a time
    //  The row key is a monotonically decreasing value based on timestamp, this will cause write performance problems in the table, but we're ok with that
    public class JobHistoryEntry : ReverseChronologicalTableEntry
    {
        public string JobName { get { return PartitionKey; } }
        
        [Obsolete("Don't call this constructor directly, it is only for deserialization", error: true)]
        public JobHistoryEntry() { }
        public JobHistoryEntry(string jobName, DateTimeOffset timestamp) : base(jobName, timestamp) { }
        public JobHistoryEntry(string jobName, DateTimeOffset timestamp, Guid invocationId, string instanceName)
            : this(jobName, timestamp)
        {
            InvocationId = invocationId;
            InstanceName = instanceName;
            Status = JobStatus.Executing;
        }
        public JobHistoryEntry(string jobName, DateTimeOffset timestamp, Guid invocationId, string instanceName, JobResult result, DateTimeOffset completedAt)
            : this(jobName, timestamp, invocationId, instanceName)
        {
            Status = result.Status;
            CompletedAt = completedAt;
            Exception = result.Exception == null ? null : result.Exception.ToString();
        }

        public Guid InvocationId { get; set; }
        public string InstanceName { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string Exception { get; set; }

        [Obsolete("Use the Status property to read/write this value")]
        public string StatusName { get; set; }

        [IgnoreProperty]
        public JobStatus Status
        {
#pragma warning disable 0618
            get { return (JobStatus)Enum.Parse(typeof(JobStatus), StatusName); }
            set { StatusName = value.ToString(); }
#pragma warning restore 0618
        }
    }
}

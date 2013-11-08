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
    //  One partition per instance
    //  This table will usually be used to look up the history for a particular instance, so queries will be for one partition at a time
    //  The row key is a monotonically decreasing value based on timestamp, this will cause write performance problems in the table, but we're ok with that
    public class WorkerInstanceHistoryEntry : ReverseChronologicalTableEntry
    {
        public string InstanceName { get { return PartitionKey; } }

        [Obsolete("Don't call this constructor directly, it is only for deserialization", error: true)]
        public WorkerInstanceHistoryEntry() { }
        public WorkerInstanceHistoryEntry(string instanceName, DateTimeOffset timestamp) : base(instanceName, timestamp) { }
        public WorkerInstanceHistoryEntry(string instanceName, DateTimeOffset timestamp, Guid invocationId, string jobName)
            : this(instanceName, timestamp)
        {
            InvocationId = invocationId;
            JobName = jobName;
            Status = JobStatus.Executing;
        }

        public WorkerInstanceHistoryEntry(string instanceName, DateTimeOffset timestamp, Guid invocationId, string jobName, JobResult result, DateTimeOffset completedAt)
            : this(instanceName, timestamp, invocationId, jobName)
        {
            Status = result.Status;
            Exception = result.Exception == null ? null : result.Exception.ToString();
            CompletedAt = completedAt;
        }

        public Guid InvocationId { get; set; }
        public string JobName { get; set; }
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

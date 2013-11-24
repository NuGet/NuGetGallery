using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGetGallery.Jobs;

namespace NuGetGallery.Monitoring.Tables
{
    public class InvocationHistoryEntry : ReverseChronologicalTableEntry
    {
        public Guid InvocationId { get { return Guid.Parse(PartitionKey); } }

        [Obsolete("Don't call this constructor directly, it is only for deserialization", error: true)]
        public InvocationHistoryEntry() { }

        public InvocationHistoryEntry(string instanceName, Guid invocationId, DateTimeOffset timestamp)
            : base(invocationId.ToString("N").ToLowerInvariant(), timestamp)
        {
            InstanceName = instanceName;
            Status = JobStatus.Executing;
        }

        public InvocationHistoryEntry(string instanceName, JobInvocation invocation, DateTimeOffset timestamp)
            : this(instanceName, invocation.Id, timestamp)
        {
            JobName = invocation.Request.Name;
            Source = invocation.Request.Source;
            RequestPayload = invocation.Request.Message == null ? null : invocation.Request.Message.AsString;
        }

        public InvocationHistoryEntry(string instanceName, JobInvocation invocation, JobResult result, string logUrl, DateTimeOffset timestamp)
            : this(instanceName, invocation, timestamp)
        {
            LogUrl = logUrl;
            Status = result == null ? JobStatus.Unspecified : result.Status;
        }

        public string InstanceName { get; set; }
        public string JobName { get; set; }
        public string Source { get; set; }
        public string RequestPayload { get; set; }
        public string LogUrl { get; set; }

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

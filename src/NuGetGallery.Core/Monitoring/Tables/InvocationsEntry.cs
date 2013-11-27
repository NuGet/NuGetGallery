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
    //  Partition based on partial GUID
    public class InvocationsEntry : TableEntity
    {
        [Obsolete("Don't call this constructor directly, it is only for deserialization", error: true)]
        public InvocationsEntry() { }

        public InvocationsEntry(string instanceName, Guid invocationId)
        {
            PartitionKey = invocationId.ToString("N").Substring(0, 8);
            RowKey = invocationId.ToString("N").Substring(8);
            InvocationId = invocationId;
            InstanceName = instanceName;
            Status = JobStatus.Executing;
        }

        public InvocationsEntry(string instanceName, JobInvocation invocation)
            : this(instanceName, invocation.Id)
        {
            ReceivedAt = invocation.RecievedAt;
            JobName = invocation.Request.Name;
            Source = invocation.Request.Source;
            RequestPayload = invocation.Request.Message == null ? null : invocation.Request.Message.AsString;
        }

        public InvocationsEntry(string instanceName, JobInvocation invocation, JobResult result, string logUrl, DateTimeOffset completedAt)
            : this(instanceName, invocation)
        {
            LogUrl = logUrl;
            CompletedAt = completedAt;
            Status = result == null ? JobStatus.Unspecified : result.Status;
            Exception = result == null ? String.Empty : (result.Exception == null ? String.Empty : result.Exception.ToString());
        }

        public string InstanceName { get; set; }
        public Guid InvocationId { get; set; }
        public DateTimeOffset ReceivedAt { get; set; }
        public string JobName { get; set; }
        public string Source { get; set; }
        public string RequestPayload { get; set; }
        public string LogUrl { get; set; }
        public string Exception { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }

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

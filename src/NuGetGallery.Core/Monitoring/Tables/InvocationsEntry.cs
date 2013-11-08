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
        
        public InvocationsEntry(Guid invocationId)
        {
            PartitionKey = invocationId.ToString().Substring(0, 8);
            RowKey = invocationId.ToString().Substring(8);
            InvocationId = invocationId;
        }

        public InvocationsEntry(JobInvocation invocation) : this(invocation.Id)
        {
            ReceivedAt = invocation.RecievedAt;
            JobName = invocation.Request.Name;
            Source = invocation.Request.Source;
            RequestPayload = invocation.Request.Message == null ? null : invocation.Request.Message.AsString;
        }

        public InvocationsEntry(JobInvocation invocation, JobResult result, string logUrl) : this(invocation)
        {
            LogUrl = logUrl;
        }

        public Guid InvocationId { get; set; }
        public DateTimeOffset ReceivedAt { get; set; }
        public string JobName { get; set; }
        public string Source { get; set; }
        public string RequestPayload { get; set; }
        public string LogUrl { get; set; }
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

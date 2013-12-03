using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using NuGetGallery.Storage;

namespace NuGet.Services.Jobs
{
    /// <summary>
    /// Contains all information about the current status of an invocation, serves as the central
    /// status record of an invocation
    /// </summary>
    [Table("Invocations")]
    public class Invocation
    {
        public Guid Id { get; private set; }
        public string Job { get; private set; }
        public string Source { get; private set; }
        public string Payload { get; private set; }
        public JobStatus Status { get; set; }
        public int DequeueCount { get; set; }
        public string LastInstanceName { get; set; }
        public Exception Exception { get; set; }
        public string LogUrl { get; set; }
        public bool Continuation { get; set; }

        public DateTimeOffset? QueuedAt { get; set; }
        public DateTimeOffset? LastDequeuedAt { get; set; }
        public DateTimeOffset? LastSuspendedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset? NextVisibleAt { get; set; }
        public DateTimeOffset? EstimatedContinueAt { get; set; }
        public DateTimeOffset? EstimatedReinvokeAt { get; set; }

        public Invocation(Guid id, string job, string source, string payload)
        {
            Id = id;
            Job = job;
            Source = source;
            Payload = payload;
            Status = JobStatus.Unspecified;
        }

        public static string GetPartitionKey(Guid id)
        {
            return id.ToString("N").Substring(0, 2); // First segment of the GUID
        }

        public static string GetRowKey(Guid id)
        {
            return id.ToString("N").Substring(2); // First segment of the GUID
        }
    }
}

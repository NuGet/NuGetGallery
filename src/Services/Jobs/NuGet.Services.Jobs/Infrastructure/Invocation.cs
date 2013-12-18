using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Storage;

namespace NuGet.Services.Jobs
{
    /// <summary>
    /// Contains all information about the current status of an invocation, serves as the central
    /// status record of an invocation
    /// </summary>
    [Table("Invocations")]
    public class Invocation : AzureTableEntity, ICloneable
    {
        public Guid Id { get; private set; }

        public string Job { get; set; }
        public string Source { get; set; }

        [PropertySerializer(typeof(JsonDictionarySerializer))]
        public Dictionary<string, string> Payload { get; private set; }

        public InvocationStatus Status { get; set; }
        public ExecutionResult Result { get; set; }
        public int DequeueCount { get; set; }
        public string LastInstanceName { get; set; }
        public string ResultMessage { get; set; }
        public string LogUrl { get; set; }
        public bool Continuation { get; set; }

        public DateTimeOffset? QueuedAt { get; set; }
        public DateTimeOffset? LastDequeuedAt { get; set; }
        public DateTimeOffset? LastSuspendedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset? EstimatedContinueAt { get; set; }
        public DateTimeOffset? EstimatedNextVisibleTime { get; set; }

        [Obsolete("For serialization only")]
        public Invocation() { }

        public Invocation(Guid id, string job, string source, Dictionary<string, string> payload)
            : base(id.ToString("N").ToLowerInvariant(), String.Empty, DateTimeOffset.UtcNow)
        {
            Id = id;
            Job = job;
            Source = source;
            Payload = payload;
            Status = InvocationStatus.Unspecified;
            Result = ExecutionResult.Incomplete;
        }

        public Invocation Clone()
        {
            return CloneCore();
        }

        object ICloneable.Clone()
        {
            return CloneCore();
        }

        private Invocation CloneCore()
        {
            return new Invocation()
            {
                PartitionKey = PartitionKey,
                RowKey = RowKey,
                Timestamp = Timestamp,

                Id = Id,
                Job = Job,
                Source = Source,
                Payload = Payload,
                Status = Status,
                Result = Result,
                DequeueCount = DequeueCount,
                LastInstanceName = LastInstanceName,
                ResultMessage = ResultMessage,
                LogUrl = LogUrl,
                Continuation = Continuation,
                QueuedAt = QueuedAt,
                LastDequeuedAt = LastDequeuedAt,
                LastSuspendedAt = LastSuspendedAt,
                CompletedAt = CompletedAt,
                EstimatedContinueAt = EstimatedContinueAt,
                EstimatedNextVisibleTime = EstimatedNextVisibleTime
            };
        }
    }
}

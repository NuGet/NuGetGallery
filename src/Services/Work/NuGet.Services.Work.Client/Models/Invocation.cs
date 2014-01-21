using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Models
{
    public class Invocation
    {
        public Guid Id { get; set; }
        public string Job { get; set; }
        public string JobInstanceName { get; set; }
        public string Source { get; set; }

        public Dictionary<string, string> Payload { get; set; }
        
        public InvocationStatus Status { get; set; }
        public ExecutionResult Result { get; set; }
        public string ResultMessage { get; set; }
        public string LastUpdatedBy { get; set; }
        public string LogUrl { get; set; }

        public int DequeueCount { get; set; }
        public bool IsContinuation { get; set; }

        public DateTimeOffset? LastDequeuedAt { get; set; }
        public DateTimeOffset? LastSuspendedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset QueuedAt { get; set; }
        public DateTimeOffset NextVisibleAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// Constructs a new Invocation. You probably don't want to do this, this should just be loaded from the API.
        /// </summary>
        public Invocation()
        {
        }
    }
}

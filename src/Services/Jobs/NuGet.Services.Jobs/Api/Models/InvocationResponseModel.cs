using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs.Api.Models
{
    public class InvocationResponseModel
    {
        public Guid Id { get; set; }
        public string Job { get; set; }
        public string Source { get; set; }
        
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

        public InvocationResponseModel(Invocation invocation)
        {
            Id = invocation.Id;
            Job = invocation.Job;
            Source = invocation.Source;

            Status = invocation.Status;
            Result = invocation.Result;
            ResultMessage = invocation.ResultMessage;
            LastUpdatedBy = invocation.LastUpdatedBy;
            LogUrl = invocation.LogUrl;

            DequeueCount = invocation.DequeueCount;
            IsContinuation = invocation.IsContinuation;

            LastDequeuedAt = invocation.LastDequeuedAt;
            LastSuspendedAt = invocation.LastSuspendedAt;
            CompletedAt = invocation.CompletedAt;
            QueuedAt = invocation.QueuedAt;
            NextVisibleAt = invocation.NextVisibleAt;
            UpdatedAt = invocation.UpdatedAt;
        }
    }
}
